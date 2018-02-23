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
using System.Reflection;
using System.Text;

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
        bool _verbose;
        bool _forbiddenDependencyUpdate;

        public Composition()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void ParseCommandLine(string[] args)
        {
            _command = CommandLineParser.Parse
            (
                args: args,
                commands: new List<CommandLineCommand>()
                {
                    new BuildCommand(),
                    new TranslationCommand(),
                    new TestCommand(),
                    new BuildInteractiveCommand(),
                    new BuildInteractiveNoUpdateCommand()
                }
            );
        }

        public void RunCommand()
        {
            if (_command == null)
                return;
            if (_command is BuildInteractiveCommand iCommand)
            {
                if (iCommand.Verbose.Value)
                    _verbose = true;
                RunInteractive(iCommand.Port.Value);
            }
            else if (_command is BuildInteractiveNoUpdateCommand yCommand)
            {
                if (yCommand.Verbose.Value)
                    _verbose = true;
                _forbiddenDependencyUpdate = true;
                RunInteractive(yCommand.Port.Value);
            }
            else if (_command is BuildCommand bCommand)
            {
                RunBuild(bCommand);
            }
        }

        void IfEnabledStartVerbosive()
        {
            if (!_verbose)
                return;
            Console.WriteLine("Verbose output enabled");
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
        }

        void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            string s = e.Exception.ToString();
            if (s.Contains("KestrelConnectionReset"))
                return;
            Console.WriteLine("First chance exception: " + s);
        }

        void RunBuild(BuildCommand bCommand)
        {
            InitTools();
            InitDiskCache();
            AddProject(PathUtils.Normalize(Environment.CurrentDirectory));
            _forbiddenDependencyUpdate = bCommand.NoUpdate.Value;
            DateTime start = DateTime.UtcNow;
            int errors = 0;
            int warnings = 0;
            var messages = new List<CompilationResultMessage>();
            var messagesFromFiles = new HashSet<string>();
            var totalFiles = 0;
            foreach (var proj in _projects)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Build started " + proj.Owner.Owner.FullPath);
                Console.ForegroundColor = ConsoleColor.Gray;
                proj.Owner.LoadProjectJson(_forbiddenDependencyUpdate);
                proj.Owner.FirstInitialize();
                proj.SpriteGeneration = bCommand.Sprite.Value;
                proj.OutputSubDir = bCommand.VersionDir.Value;
                proj.CompressFileNames = !bCommand.Fast.Value;
                proj.BundleCss = !bCommand.Fast.Value;
                proj.SpriterInitialization();
                proj.RefreshMainFile();
                proj.DetectBobrilJsxDts();
                proj.RefreshExampleSources();
                var ctx = new BuildCtx(_compilerPool, _verbose);
                ctx.TSCompilerOptions = GetDefaultTSCompilerOptions(proj);
                ctx.Sources = new HashSet<string>();
                ctx.Sources.Add(proj.MainFile);
                proj.ExampleSources.ForEach(s => ctx.Sources.Add(s));
                if (proj.BobrilJsxDts != null)
                    ctx.Sources.Add(proj.BobrilJsxDts);
                proj.Owner.Build(ctx);
                var buildResult = ctx.BuildResult;
                var filesContent = new Dictionary<string, object>();
                proj.FillOutputByAdditionalResourcesDirectory(filesContent);
                IncludeMessages(buildResult, ref errors, ref warnings, messages, messagesFromFiles);
                if (errors == 0)
                {
                    if (bCommand.Fast.Value)
                    {
                        var fastBundle = new FastBundleBundler(_tools);
                        fastBundle.FilesContent = filesContent;
                        fastBundle.Project = proj;
                        fastBundle.BuildResult = buildResult;
                        fastBundle.Build("bb/base", "bundle.js.map");
                    }
                    else
                    {
                        var bundle = new BundleBundler(_tools);
                        bundle.FilesContent = filesContent;
                        bundle.Project = proj;
                        bundle.BuildResult = buildResult;
                        bundle.Build(bCommand.Compress.Value, bCommand.Mangle.Value, bCommand.Beautify.Value);
                    }
                    SaveFilesContentToDisk(filesContent, bCommand.Dir.Value);
                    totalFiles += filesContent.Count;
                }
            }
            var duration = DateTime.UtcNow - start;
            Console.ForegroundColor = errors != 0 ? ConsoleColor.Red : warnings != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.WriteLine("Build done in " + (DateTime.UtcNow - start).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " with " + Plural(errors, "error") + " and " + Plural(warnings, "warning") + " and has " + Plural(totalFiles, "file"));
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        void SaveFilesContentToDisk(Dictionary<string, object> filesContent, string dir)
        {
            dir = PathUtils.Normalize(dir);
            var utf8WithoutBom = new UTF8Encoding(false);
            foreach (var nameAndContent in filesContent)
            {
                var content = nameAndContent.Value;
                var fileName = PathUtils.Join(dir, nameAndContent.Key);
                if (content is Lazy<object>)
                {
                    content = ((Lazy<object>)content).Value;
                }
                Directory.CreateDirectory(PathUtils.Parent(fileName));
                if (content is string)
                {
                    File.WriteAllText(fileName, (string)content, utf8WithoutBom);
                }
                else
                {
                    File.WriteAllBytes(fileName, (byte[])content);
                }
            }
        }

        void RunInteractive(string portInString)
        {
            IfEnabledStartVerbosive();
            int port = 8080;
            if (int.TryParse(portInString, out var portInInt))
            {
                port = portInInt;
            }
            InitTools();
            InitDiskCache();
            InitTestServer();
            InitMainServer();
            AddProject(PathUtils.Normalize(Environment.CurrentDirectory));
            StartWebServer(port);
            InitInteractiveMode();
            WaitForStop();
        }

        public void InitTools()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            var runningFrom = PathUtils.Normalize(new FileInfo(location.AbsolutePath).Directory.FullName);
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), ".bbcore");
            if (runningFrom.StartsWith(_bbdir))
            {
                _bbdir = PathUtils.Join(_bbdir, "tools");
            }
            else
            {
                _bbdir = PathUtils.Join(_bbdir, "dev");
            }
            _tools = new ToolsDir.ToolsDir(_bbdir);
            _compilerPool = new CompilerPool(_tools);
        }

        public void InitDiskCache()
        {
            _dc = new DiskCache.DiskCache(new NativeFsAbstraction(), () => new OsWatcher());
        }

        public ProjectOptions AddProject(string path)
        {
            var projectDir = PathUtils.Normalize(new DirectoryInfo(path).FullName);
            var dirCache = _dc.TryGetItem(projectDir) as IDirectoryCache;
            var proj = TSProject.Get(dirCache, _dc);
            proj.IsRootProject = true;
            if (proj.ProjectOptions != null)
                return proj.ProjectOptions;
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
            if (_mainServer != null)
                _mainServer.Project = _currentProject;
            return proj.ProjectOptions;
        }

        public void Build(ProjectOptions project)
        {
            var ctx = new BuildCtx(_compilerPool, false);
            project.Owner.Build(ctx);
        }

        public void StartWebServer(int port)
        {
            _webServer = new WebServerHost();
            _webServer.FallbackToRandomPort = true;
            _webServer.Port = port;
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
            object content;
            if (FindInFilesContent(pathWithoutFirstSlash, filesContentFromCurrentProjectBuildResult, out content))
            {
                context.Response.ContentType = PathUtils.PathToMimeType(pathWithoutFirstSlash);
                if (content is Lazy<object>)
                {
                    content = ((Lazy<object>)content).Value;
                }
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

        static bool FindInFilesContent(string pathWithoutFirstSlash, Dictionary<string, object> filesContentFromCurrentProjectBuildResult, out object content)
        {
            content = null;
            if (filesContentFromCurrentProjectBuildResult == null)
                return false;
            if (filesContentFromCurrentProjectBuildResult.TryGetValue(pathWithoutFirstSlash, out content))
                return true;
            // This should be very rare so it could be slow linear search
            foreach (var p in filesContentFromCurrentProjectBuildResult)
            {
                if (p.Key.Equals(pathWithoutFirstSlash, StringComparison.InvariantCultureIgnoreCase))
                {
                    content = p.Value;
                    return true;
                }
            }
            return false;
        }

        public void InitTestServer()
        {
            _testServer = new TestServer(_verbose);
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
                    if (!_dc.CheckForTrueChange())
                        continue;
                    _dc.ResetChange();
                    _hasBuildWork.Set();
                    DateTime start = DateTime.UtcNow;
                    ProjectOptions[] toBuild;
                    lock (_projectsLock)
                    {
                        toBuild = _projects.ToArray();
                    }
                    if (toBuild.Length == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Change detected, but no project to build");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        continue;
                    }
                    _mainServer.NotifyCompilationStarted();
                    int errors = 0;
                    int warnings = 0;
                    var messages = new List<CompilationResultMessage>();
                    var messagesFromFiles = new HashSet<string>();
                    var totalFiles = 0;
                    foreach (var proj in toBuild)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine("Build started " + proj.Owner.Owner.FullPath);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        try
                        {
                            proj.Owner.LoadProjectJson(_forbiddenDependencyUpdate);
                            proj.Owner.FirstInitialize();
                            proj.RefreshMainFile();
                            proj.RefreshTestSources();
                            proj.SpriterInitialization();
                            proj.DetectBobrilJsxDts();
                            proj.RefreshExampleSources();
                            var ctx = new BuildCtx(_compilerPool, _verbose);
                            ctx.TSCompilerOptions = GetDefaultTSCompilerOptions(proj);
                            ctx.Sources = new HashSet<string>();
                            ctx.Sources.Add(proj.MainFile);
                            proj.ExampleSources.ForEach(s => ctx.Sources.Add(s));
                            if (proj.BobrilJsxDts != null)
                                ctx.Sources.Add(proj.BobrilJsxDts);
                            proj.Owner.Build(ctx);
                            var buildResult = ctx.BuildResult;
                            var filesContent = new Dictionary<string, object>();
                            proj.FillOutputByAdditionalResourcesDirectory(filesContent);
                            var fastBundle = new FastBundleBundler(_tools);
                            fastBundle.FilesContent = filesContent;
                            fastBundle.Project = proj;
                            fastBundle.BuildResult = buildResult;
                            fastBundle.Build("bb/base", "bundle.js.map");
                            proj.MainProjFastBundle = fastBundle;
                            IncludeMessages(proj.MainProjFastBundle, ref errors, ref warnings, messages, messagesFromFiles);
                            if (proj.TestSources != null && proj.TestSources.Count > 0)
                            {
                                ctx = new BuildCtx(_compilerPool, _verbose);
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
                            totalFiles += filesContent.Count;
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Fatal Error: " + ex);
                            Console.ForegroundColor = ConsoleColor.Gray;
                            errors++;
                        }
                    }
                    var duration = DateTime.UtcNow - start;
                    _mainServer.NotifyCompilationFinished(errors, warnings, duration.TotalSeconds, messages);
                    Console.ForegroundColor = errors != 0 ? ConsoleColor.Red : warnings != 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                    Console.WriteLine("Build done in " + (DateTime.UtcNow - start).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " with " + Plural(errors, "error") + " and " + Plural(warnings, "warning") + " and has " + Plural(totalFiles, "file"));
                    Console.ForegroundColor = ConsoleColor.Gray;
                    _dc.ResetChange();
                }
            });
        }

        string Plural(int number, string word)
        {
            if (number == 0)
                return "no " + word + "s";
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
                    if (d.isError)
                        errors++;
                    else
                        warnings++;
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
                var chromePath = ChromePathFinder.GetChromePath(new NativeFsAbstraction());
                _chromeProcessFactory = new ChromeProcessFactory(chromePath);
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
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs args) =>
            {
                ExitWithCleanUp();
                args.Cancel = true;
            };
            while (true)
            {
                var line = Console.ReadLine();
                if (line == "q" || line == "quit")
                    break;
            }
            ExitWithCleanUp();
        }

        public void ExitWithCleanUp()
        {
            StopChromeTest();
            Environment.Exit(0);
        }
    }
}
