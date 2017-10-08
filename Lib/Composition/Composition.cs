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
        ILongPollingServer _testServer;

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
                await _testServer.Handle(context);
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
            if (filesContentFromCurrentProjectBuildResult.TryGetValue(pathWithoutFirstSlash, out var content))
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
            _testServer = new LongPollingServer(() => new TestServerConnectionHandler());
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
                    foreach (var proj in toBuild)
                    {
                        proj.Owner.LoadProjectJson();
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
                        }
                        proj.FilesContent = filesContent;
                    }
                    Console.WriteLine("Build done in " + (DateTime.UtcNow - start).TotalSeconds.ToString("F1", CultureInfo.InvariantCulture));
                }
            });
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

        public void WaitForStop()
        {
            Console.TreatControlCAsInput = true;
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.C)
                    break;
            }
        }
    }
}
