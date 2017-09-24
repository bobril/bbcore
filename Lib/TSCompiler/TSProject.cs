using Lib.Composition;
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
                        foreach (var i in parsedV.Properties())
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
                ToCheck = new OrderedHashSet<string>(),
                ToCompile = new OrderedHashSet<string>()
            };
            ITSCompiler compiler = null;
            try
            {
                compiler = buildCtx.CompilerPool.GetTs();
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
                    checkJs = false,
                    rootDir = buildModuleCtx._owner.Owner.FullPath,
                    outDir = "_virtual",
                    removeComments = false,
                    types = new string[0],
                    lib = new HashSet<string> { "es5", "dom", "es2015.core", "es2015.promise", "es2015.iterable" }
                });
                do
                {
                    buildModuleCtx.ChangedDts = false;
                    buildModuleCtx.CrawledCount = 0;
                    buildModuleCtx.TrullyCompiledCount = 0;
                    buildModuleCtx.ToCheck.Clear();
                    buildModuleCtx.ToCompile.Clear();
                    buildModuleCtx.CheckAdd(PathUtils.Join(Owner.FullPath, MainFile));
                    // TODO: Add test sources from ProjectOptions
                    buildModuleCtx.Crawl();
                    if (buildModuleCtx.ToCompile.Count != 0)
                    {
                        compiler.MeasurePerformance = true;
                        compiler.CreateProgram(Owner.FullPath, buildModuleCtx.ToCompile.ToArray());
                        if (!compiler.CompileProgram())
                            break;
                        compiler.GatherSourceInfo();
                                                
                        if (!compiler.EmitProgram())
                            break;
                        buildModuleCtx.UpdateCacheIds();
                    }
                } while (buildModuleCtx.ChangedDts || buildModuleCtx.TrullyCompiledCount < buildModuleCtx.ToCompile.Count);
                BuildResult = buildModuleCtx._result;
            }
            finally
            {
                if (compiler != null) buildCtx.CompilerPool.ReleaseTs(compiler);
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
