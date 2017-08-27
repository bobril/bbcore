using Lib.DiskCache;
using Lib.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lib.TSCompiler
{
    public class TSProject
    {
        public IDiskCache DiskCache { get; set; }
        public IDirectoryCache Owner { get; set; }
        public string MainFile { get; set; }
        public string TypesMainFile { get; set; }
        public ProjectOptions ProjectOptions { get; set; }
        public BuildResult BuildResult { get; set; }
        public int PackageJsonChangeId { get; set; }
        public int InterfaceChangeId { get; set; }
        public bool IsRootProject { get; set; }
        public HashSet<string> Dependencies { get; set; }

        public void LoadProjectJson()
        {
            DiskCache.UpdateIfNeeded(Owner);
            var packageJsonFile = Owner.TryGetChild("package.json");
            if (packageJsonFile is IFileCache)
            {
                var newChangeId = ((IFileCache)packageJsonFile).ChangeId;
                if (newChangeId != PackageJsonChangeId)
                {
                    var parsed = JObject.Parse(((IFileCache)packageJsonFile).Utf8Content);
                    var deps = new HashSet<string>();
                    var hasMain = false;
                    if (parsed.GetValue("typescript") is JObject parsedT)
                    {
                        if (parsedT.GetValue("main") is JValue mainV)
                        {
                            MainFile = PathUtils.Normalize(mainV.ToString());
                            TypesMainFile = null;
                            hasMain = true;
                        }
                    }
                    if (!hasMain)
                    {
                        var mainV2 = parsed.GetValue("main") as JValue;
                        if (mainV2 != null)
                        {
                            MainFile = PathUtils.Normalize(mainV2.ToString());
                        }
                        else
                        {
                            MainFile = "index.js";
                        }
                        if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, PathUtils.ChangeExtension(MainFile, "ts"))) is IFileCache fileAsTs)
                        {
                            MainFile = PathUtils.ChangeExtension(MainFile, "ts");
                            TypesMainFile = null;
                            hasMain = true;
                        }
                        else
                        {
                            fileAsTs = DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, PathUtils.ChangeExtension(MainFile, "tsx"))) as IFileCache;
                            if (fileAsTs != null)
                            {
                                MainFile = PathUtils.ChangeExtension(MainFile, "tsx");
                                TypesMainFile = null;
                                hasMain = true;
                            }
                        }
                        if (!hasMain)
                        {
                            if (parsed.GetValue("types") is JValue mainV)
                            {
                                TypesMainFile = PathUtils.Normalize(mainV.ToString());
                                hasMain = true;
                            }
                        }
                    }
                    if (TypesMainFile == null)
                    {
                        TypesMainFile = PathUtils.ChangeExtension(MainFile, "d.ts");
                    }
                    if (parsed.GetValue("dependencies") is JObject parsedV)
                    {
                        foreach(var i in parsedV.Properties())
                        {
                            deps.Add(i.Name);
                        }
                    }
                    if (IsRootProject && parsed.GetValue("devDependencies") is JObject parsedV2)
                    {
                        foreach (var i in parsedV2.Properties())
                        {
                            deps.Add(i.Name);
                        }
                    }
                    PackageJsonChangeId = newChangeId;
                    Dependencies = deps;
                }
            }
        }

        public void Build(BuildCtx buildCtx)
        {
            LoadProjectJson();
            var buildModuleCtx = new BuildModuleCtx()
            {
                _buildCtx = buildCtx,
                _owner = this,
                _result = new BuildResult(),
            };
            ITSCompiler compiler = null;
            try
            {
                compiler = buildCtx._compilerPool.Get();
                compiler.DiskCache = DiskCache;
                compiler.Ctx = buildModuleCtx;
                compiler.MergeCompilerOptions(new TSCompilerOptions
                {
                    declaration = true,
                    sourceMap = true,
                    skipLibCheck = true,
                    skipDefaultLibCheck = true,
                    module = ModuleKind.CommonJS,
                    target = ScriptTarget.ES5,
                    preserveConstEnums = false,
                    jsx = JsxEmit.React,
                    reactNamespace = "b",
                    experimentalDecorators = true,
                    noEmitHelpers = true,
                    allowJs = true,
                    lib = new HashSet<string> { "es5", "dom", "es2015.core", "es2015.promise", "es2015.iterable" }
                });
                do
                {
                    buildModuleCtx.ChangedDts = false;
                    var toCheck = new OrderedHashSet<string>();
                    toCheck.Add(PathUtils.Join(Owner.FullPath, MainFile));
                    var toCompile = new OrderedHashSet<string>();
                    Crawl(buildModuleCtx, toCheck, toCompile);
                    if (toCompile.Count != 0)
                    {
                        compiler.MeasurePerformance = true;
                        compiler.CreateProgram(Owner.FullPath, toCompile.ToArray());
                        compiler.CompileProgram();
                        compiler.EmitProgram();
                        buildModuleCtx.UpdateCacheIds();
                    }
                } while (buildModuleCtx.ChangedDts);
                BuildResult = buildModuleCtx._result;
            }
            finally
            {
                if (compiler != null) buildCtx._compilerPool.Release(compiler);
            }
        }

        void Crawl(BuildModuleCtx buildModuleCtx, OrderedHashSet<string> toCheck, OrderedHashSet<string> toCompile)
        {
            for (var idx = 0; idx < toCheck.Count; idx++)
            {
                var fileName = toCheck[idx];
                var fileCache = DiskCache.TryGetItem(fileName) as IFileCache;
                if (fileCache == null || fileCache.IsInvalid)
                {
                    throw new Exception("Missing " + fileName);
                }
                var fileAdditional = TSFileAdditionalInfo.Get(fileCache, DiskCache);
                buildModuleCtx.AddSource(fileAdditional);
                if (fileAdditional.NeedsCompilation())
                {
                    if (fileAdditional.DtsLink != null)
                        fileAdditional.DtsLink.Owner.IsInvalid = true;
                    fileAdditional.DtsLink = null;
                    fileAdditional.JsLink = null;
                    fileAdditional.MapLink = null;
                    toCompile.Add(fileName);
                }
                else
                {
                    foreach (var localAdditional in fileAdditional.LocalImports)
                    {
                        var localName = localAdditional.Owner.FullPath;
                        if (toCheck.Contains(localName))
                            continue;
                        if (localName.EndsWith(".d.ts")) continue;
                        toCheck.Add(localName);
                    }
                    foreach(var moduleInfo in fileAdditional.ModuleImports)
                    {
                        moduleInfo.LoadProjectJson();
                        var mainFile = PathUtils.Join(moduleInfo.Owner.FullPath, moduleInfo.MainFile);
                        if (toCheck.Contains(mainFile))
                            continue;
                        if (mainFile.EndsWith(".d.ts")) continue;
                        toCheck.Add(mainFile);
                    }
                }
            }
        }

        public static TSProject FindInfoForModule(IDirectoryCache dir, IDiskCache diskCache, string moduleName)
        {
            while (!dir.IsFake)
            {
                diskCache.UpdateIfNeeded(dir);
                var nmdir = dir.TryGetChild("node_modules") as IDirectoryCache;
                if (nmdir != null)
                {
                    diskCache.UpdateIfNeeded(nmdir);
                    var mdir = nmdir.TryGetChild(moduleName) as IDirectoryCache;
                    if (mdir != null)
                    {
                        diskCache.UpdateIfNeeded(mdir);
                        return Get(mdir, diskCache);
                    }
                }
                dir = dir.Parent;
            }
            return null;
        }

        public static TSProject Get(IDirectoryCache dir, IDiskCache diskCache)
        {
            if (dir == null) return null;
            if (dir.AdditionalInfo == null)
                dir.AdditionalInfo = new TSProject { Owner = dir, DiskCache = diskCache };
            return (TSProject)dir.AdditionalInfo;
        }
    }
}
