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

namespace Lib.Composition
{
    public class Composition
    {
        string _bbdir;
        ToolsDir.IToolsDir _tools;
        DiskCache.DiskCache _dc;
        TSCompilerPool _compilerPool;
        object _projectsLock = new object();
        List<ProjectOptions> _projects = new List<ProjectOptions>();
        WebServerHost _webServer;
        AutoResetEvent _hasBuildWork = new AutoResetEvent(true);
        private ProjectOptions _currentProject;

        public void InitTools(string version)
        {
            _bbdir = PathUtils.Join(PathUtils.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), ".bbcore");
            _tools = new ToolsDir.ToolsDir(PathUtils.Join(_bbdir, "tools"));
            if (_tools.GetTypeScriptVersion() != version)
            {
                _tools.InstallTypeScriptVersion(version);
            }
            _compilerPool = new TSCompilerPool(_tools);
        }

        public void InitDiskCache()
        {
            _dc = new DiskCache.DiskCache(new NativeFsAbstraction(), () => new ModulesLinksOsWatcher());
            _dc.AddRoot(_tools.TypeScriptLibDir);
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
                Owner = proj,
                Defines = new Dictionary<string, bool> { { "DEBUG", true } }
            };
            lock(_projectsLock)
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
            switch(path)
            {
                case "/index.html":
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(_currentProject.FastBundle.IndexHtml);
                    return;
                case "/loader.js":
                    context.Response.ContentType = "text/javascript";
                    await context.Response.WriteAsync(_tools.LoaderJs);
                    return;
                case "/bundle.js":
                    context.Response.ContentType = "text/javascript";
                    await context.Response.WriteAsync(_currentProject.FastBundle.BundleJs);
                    return;
                case "/bundle.js.map":
                    context.Response.ContentType = "text/javascript";
                    await context.Response.WriteAsync(_currentProject.FastBundle.SourceMapString);
                    return;
            }
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Not found " + path);
        }

        public void InitInteractiveMode()
        {
            _hasBuildWork.Set();
            _dc.ChangeObservable.Subscribe((_)=> {
                _hasBuildWork.Set();
            });
            Task.Run(() =>
            {
                while(_hasBuildWork.WaitOne())
                {
                    ProjectOptions[] toBuild;
                    lock(_projectsLock)
                    {
                        toBuild = _projects.ToArray();
                    }
                    foreach(var proj in toBuild)
                    {
                        var ctx = new BuildCtx(_compilerPool);
                        proj.Owner.Build(ctx);
                        var buildResult = proj.Owner.BuildResult;
                        var fastBundle = new FastBundleBundler();
                        fastBundle.Project = proj;
                        fastBundle.BuildResult = buildResult;
                        fastBundle.Build("bb/base", "bundle.js.map");
                        proj.FastBundle = fastBundle;
                    }
                }
            });
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
