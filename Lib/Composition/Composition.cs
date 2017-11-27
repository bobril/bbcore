using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Watcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using Lib.WebServer;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using Lib.Utils.CommandLineParser.Definitions;
using Lib.Utils.CommandLineParser.Parser;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Lib.Chrome;

namespace Lib.Composition
{
    public class Composition
    {
        string _bbdir;
        ToolsDir.IToolsDir _tools;
        DiskCache.DiskCache _dc;
        CompilerPool _compilerPool;
        object _projectsLock = new object();
        List<ProjectOptions> _projects = new List<ProjectOptions>();
        WebServerHost _webServer;
        AutoResetEvent _hasBuildWork = new AutoResetEvent(true);
        ProjectOptions _currentProject;
        CommandLineCommand _command;
        TestServer _testServer;
        ILongPollingServer _testServerLongPollingHandler;
        MainServer _mainServer;
        ILongPollingServer _mainServerLongPollingHandler;
        IChromeProcessFactory _chromeProcessFactory;
        IChromeProcess _chromeProcess;

        public void ParseCommandLineArgs(string[] args)
        {
            _command = CommandLineParser.Parse
            (
                args: args,
                commands: new List<CommandLineCommand>()
                {
                    new MainCommand(),
                    new BuildCommand(),
                    new TranslationCommand(),
                    new TestCommand(),
                    new BuildInteractiveCommand(),
                    new BuildInteractiveNoUpdateCommand()
                }
            );
        }

        public void InitTools(string version)
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), ".bbcore");
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"));
            if (_tools.GetTypeScriptVersion() != version)
            {
                _tools.InstallTypeScriptVersion(version);
            }
            _compilerPool = new CompilerPool(_tools);
        }

        public void InitDiskCache()
        {
            _dc = new DiskCache.DiskCache(new NativeFsAbstraction(), () => new ModulesLinksOsWatcher());
            _dc.AddRoot(_tools.Path);
        }

        public ProjectOptions AddProject(string path)
        {
            var projectDir = PathUtils.Normalize(new DirectoryInfo(path).FullName);
            _dc.AddRoot(projectDir);
            var dirCache = _dc.TryGetItem(projectDir) as IDirectoryCache;
            var proj = TSProject.Get(dirCache, _dc);
            proj.IsRootProject = true;
            if (proj.ProjectOptions != null) return proj.ProjectOptions;
            proj.ProjectOptions = new ProjectOptions
            {
                Tools = _tools,
                Owner = proj,
                Defines = new Dictionary<string, bool> { { "DEBUG", true } }
            };
            lock (_projectsLock)
            {
                _projects.Add(proj.ProjectOptions);
            }
            _currentProject = proj.ProjectOptions;
            _mainServer.Project = _currentProject;
            return proj.ProjectOptions;
        }

        public void Build(ProjectOptions project)
        {
            var ctx = new BuildCtx(_compilerPool);
            project.Owner.Build(ctx);
        }

        public void StartWebServer(int? port)
        {
            _webServer = new WebServerHost();
            if (port.HasValue)
            {
                _webServer.Port = port.Value;
                _webServer.FallbackToRandomPort = false;
            }
            else
            {
                _webServer.Port = 8080;
                _webServer.FallbackToRandomPort = true;
            }
            _webServer.Handler = Handler;
            _webServer.Start();
            Console.WriteLine($"Listening on http://localhost:{_webServer.Port}/");
        }

        async Task Handler(HttpContext context)
        {
            var path = context.Request.Path;
            if (path == "/")
                path = "/index.html";
            if (path.StartsWithSegments("/bb", out var bbweb))
            {
                if (bbweb == "")
                {
                    context.Response.Redirect("/bb/", true);
                    return;
                }
                if (bbweb == "/" || bbweb == "/index.html")
                {
                    await context.Response.WriteAsync(_tools.WebIndexHtml);
                    return;
                }
                if (bbweb == "/a.js")
                {
                    await context.Response.WriteAsync(_tools.WebAJs);
                    return;
                }
            }
            if (path.StartsWithSegments("/bb/test", out var bbtest))
            {
                if (bbtest == "")
                {
                    context.Response.Redirect("/bb/test/", true);
                    return;
                }
                if (bbtest == "/" || bbtest == "/index.html")
                {
                    await context.Response.WriteAsync(_tools.WebtIndexHtml);
                    return;
                }
                if (bbtest == "/a.js")
                {
                    await context.Response.WriteAsync(_tools.WebtAJs);
                    return;
                }
            }
            if (path == "/bb/api/test")
            {
                await _testServerLongPollingHandler.Handle(context);
                return;
            }
            if (path == "/bb/api/main")
            {
                await _mainServerLongPollingHandler.Handle(context);
                return;
            }
            if (path == "/bb/api/projectdirectory")
            {
                await context.Response.WriteAsync(_currentProject.Owner.Owner.FullPath);
                return;
            }

            if (path.StartsWithSegments("/bb/base", out var src))
            {
                var srcPath = PathUtils.Join(_currentProject.Owner.Owner.FullPath, src.Value.Substring(1));
                var srcFileCache = _dc.TryGetItem(srcPath) as IFileCache;
                if (srcFileCache != null)
                {
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(srcFileCache.Utf8Content);
                    return;
                }
            }
            var pathWithoutFirstSlash = path.Value.Substring(1);
            var filesContentFromCurrentProjectBuildResult = _currentProject.FilesContent;
            if (filesContentFromCurrentProjectBuildResult != null && filesContentFromCurrentProjectBuildResult.TryGetValue(pathWithoutFirstSlash, out var content))
            {
                context.Response.ContentType = PathUtils.PathToMimeType(pathWithoutFirstSlash);
                if (content is string)
                {
                    await context.Response.WriteAsync((string)content);
                }
                else
                {
                    await context.Response.Body.WriteAsync((byte[])content, 0, ((byte[])content).Length);
                }
                return;
            }
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Not found " + path);
        }

        public void InitTestServer()
        {
            _testServer = new TestServer();
            _testServerLongPollingHandler = new LongPollingServer(_testServer.NewConnectionHandler);
            _testServer.OnTestResults.Subscribe((results) =>
            {
                Console.ForegroundColor = results.TestsFailed != 0 ? ConsoleColor.Red : results.TestsSkipped != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.WriteLine("Tests on {0} Failed: {1} Skipped: {2} Total: {3} Duration: {4:F1}s", results.UserAgent, results.TestsFailed, results.TestsSkipped, results.TotalTests, results.Duration * 0.001);
                Console.ForegroundColor = ConsoleColor.Gray;
            });
        }

        public void InitMainServer()
        {
            _mainServer = new MainServer(() => _testServer.GetState());
            _mainServerLongPollingHandler = new LongPollingServer(_mainServer.NewConnectionHandler);
            _testServer.OnChange.Subscribe((_) => { _mainServer.NotifyTestServerChange(); });
        }

        public void InitInteractiveMode()
        {
            _hasBuildWork.Set();
            _dc.ChangeObservable.Throttle(TimeSpan.FromMilliseconds(200)).Subscribe((_) => _hasBuildWork.Set());
            Task.Run(() =>
            {
                while (_hasBuildWork.WaitOne())
                {
                    DateTime start = DateTime.UtcNow;
                    ProjectOptions[] toBuild;
                    lock (_projectsLock)
                    {
                        toBuild = _projects.ToArray();
                    }
                    _mainServer.NotifyCompilationStarted();
                    int errors = 0;
                    int warnings = 0;
                    var messages = new List<CompilationResultMessage>();
                    var messagesFromFiles = new HashSet<string>();
                    foreach (var proj in toBuild)
                    {
                        proj.Owner.LoadProjectJson();
                        proj.Owner.FirstInitialize();
                        proj.RefreshMainFile();
                        proj.RefreshTestSources();
                        proj.DetectBobrilJsxDts();
                        proj.RefreshExampleSources();
                        var ctx = new BuildCtx(_compilerPool);
                        ctx.TSCompilerOptions = GetDefaultTSCompilerOptions(proj);
                        ctx.Sources = new HashSet<string>();
                        ctx.Sources.Add(proj.MainFile);
                        proj.ExampleSources.ForEach(s => ctx.Sources.Add(s));
                        if (proj.BobrilJsxDts != null)
                            ctx.Sources.Add(proj.BobrilJsxDts);
                        proj.Owner.Build(ctx);
                        var buildResult = ctx.BuildResult;
                        var filesContent = new Dictionary<string, object>();
                        var fastBundle = new FastBundleBundler(_tools);
                        fastBundle.FilesContent = filesContent;
                        fastBundle.Project = proj;
                        fastBundle.BuildResult = buildResult;
                        fastBundle.Build("bb/base", "bundle.js.map");
                        proj.MainProjFastBundle = fastBundle;
                        IncludeMessages(proj.MainProjFastBundle, ref errors, ref warnings, messages, messagesFromFiles);
                        if (proj.TestSources != null && proj.TestSources.Count > 0)
                        {
                            ctx = new BuildCtx(_compilerPool);
                            ctx.TSCompilerOptions = GetDefaultTSCompilerOptions(proj);
                            ctx.Sources = new HashSet<string>();
                            ctx.Sources.Add(proj.JasmineDts);
                            proj.TestSources.ForEach(s => ctx.Sources.Add(s));
                            if (proj.BobrilJsxDts != null)
                                ctx.Sources.Add(proj.BobrilJsxDts);
                            proj.Owner.Build(ctx);
                            var testBuildResult = ctx.BuildResult;
                            fastBundle = new FastBundleBundler(_tools);
                            fastBundle.FilesContent = filesContent;
                            fastBundle.Project = proj;
                            fastBundle.BuildResult = testBuildResult;
                            fastBundle.Build("bb/base", "testbundle.js.map", true);
                            proj.TestProjFastBundle = fastBundle;
                            IncludeMessages(proj.TestProjFastBundle, ref errors, ref warnings, messages, messagesFromFiles);
                            if (errors == 0)
                            {
                                _testServer.StartTest("/test.html", new Dictionary<string, SourceMap> { { "testbundle.js", testBuildResult.SourceMap } });
                                StartChromeTest();
                            }
                        }
                        else
                        {
                            proj.TestProjFastBundle = null;
                        }
                        proj.FilesContent = filesContent;
                    }
                    var duration = DateTime.UtcNow - start;
                    _mainServer.NotifyCompilationFinished(errors, warnings, duration.TotalSeconds, messages);
                    Console.ForegroundColor = errors != 0 ? ConsoleColor.Red : warnings != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                    Console.WriteLine("Build done in " + (DateTime.UtcNow - start).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " with " + Plural(errors, "error") + " and " + Plural(warnings, "warning"));
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            });
        }

        string Plural(int number, string word)
        {
            if (number == 0) return "no " + word + "s";
            return number + " " + word + (number > 1 ? "s" : "");
        }

        void IncludeMessages(FastBundleBundler fastBundle, ref int errors, ref int warnings, List<CompilationResultMessage> messages, HashSet<string> messagesFromFiles)
        {
            IncludeMessages(fastBundle.BuildResult, ref errors, ref warnings, messages, messagesFromFiles);
        }

        void IncludeMessages(BuildResult buildResult, ref int errors, ref int warnings, List<CompilationResultMessage> messages, HashSet<string> messagesFromFiles)
        {
            foreach (var pathInfoPair in buildResult.Path2FileInfo)
            {
                if (messagesFromFiles.Contains(pathInfoPair.Key))
                    continue;
                messagesFromFiles.Add(pathInfoPair.Key);
                var diag = pathInfoPair.Value.Diagnostic;
                if (diag == null)
                    continue;
                foreach (var d in diag)
                {
                    if (d.isError) errors++; else warnings++;
                    messages.Add(new CompilationResultMessage
                    {
                        FileName = pathInfoPair.Key,
                        IsError = d.isError,
                        Text = d.text,
                        Pos = new int[]
                        {
                            d.startLine+1,
                            d.startCharacter+1,
                            d.endLine+1,
                            d.endCharacter+1
                        }
                    });
                }
            }
        }

        static TSCompilerOptions GetDefaultTSCompilerOptions(ProjectOptions proj)
        {
            return new TSCompilerOptions
            {
                sourceMap = true,
                skipLibCheck = true,
                skipDefaultLibCheck = true,
                target = ScriptTarget.ES5,
                preserveConstEnums = false,
                jsx = JsxEmit.React,
                reactNamespace = proj.BobrilJsx ? "b" : "React",
                experimentalDecorators = true,
                noEmitHelpers = true,
                allowJs = true,
                checkJs = false,
                removeComments = false,
                types = new string[0],
                lib = new HashSet<string> { "es5", "dom", "es2015.core", "es2015.promise", "es2015.iterable", "es2015.collection" }
            };
        }

        public void StartChromeTest()
        {
            if (_chromeProcessFactory == null)
            {
                _chromeProcessFactory = new ChromeProcessFactory();
            }
            if (_chromeProcess == null)
            {
                try
                {
                    _chromeProcess = _chromeProcessFactory.Create($"http://localhost:{_webServer.Port}/bb/test/");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed To Start Chrome Headless");
                    Console.WriteLine(ex);
                }
            }
        }

        public void StopChromeTest()
        {
            if (_chromeProcess != null)
            {
                _chromeProcess.Dispose();
                _chromeProcess = null;
            }
        }

        public void WaitForStop()
        {
            Console.TreatControlCAsInput = true;
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.C)
                    break;
            }
            StopChromeTest();
        }
    }
}
