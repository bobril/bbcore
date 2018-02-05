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
        bool WasFirstInitialize;

        const string DefaultTypeScriptVersion = "2.7.1";

        public IDiskCache DiskCache { get; set; }
        public IDirectoryCache Owner { get; set; }
        public string MainFile { get; set; }
        public string TypesMainFile { get; set; }
        public ProjectOptions ProjectOptions { get; set; }
        public int PackageJsonChangeId { get; set; }
        public int InterfaceChangeId { get; set; }
        public bool IsRootProject { get; set; }
        public HashSet<string> Dependencies { get; set; }

        public void LoadProjectJson(bool forbiddenDependencyUpdate)
        {
            DiskCache.UpdateIfNeeded(Owner);
            var packageJsonFile = Owner.TryGetChild("package.json");
            if (packageJsonFile is IFileCache)
            {
                var newChangeId = ((IFileCache)packageJsonFile).ChangeId;
                if (newChangeId != PackageJsonChangeId)
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(((IFileCache)packageJsonFile).Utf8Content);
                    }
                    catch (Exception)
                    {
                        parsed = new JObject();
                    }
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
                    if (ProjectOptions != null)
                    {
                        FillProjectOptionsFromPackageJson(parsed);
                        if (!forbiddenDependencyUpdate)
                        {
                            ProjectOptions.Tools.UpdateDependencies(Owner.FullPath, ProjectOptions.DependencyUpdate == DepedencyUpdate.Upgrade, ProjectOptions.NpmRegistry);
                        }
                    }
                }
            }
            else
            {
                MainFile = "index.js";
                if (ProjectOptions != null)
                {
                    FillProjectOptionsFromPackageJson(new JObject());
                }
            }
        }

        void FillProjectOptionsFromPackageJson(JObject parsed)
        {
            ProjectOptions.Localize = Dependencies?.Contains("bobril-g11n") ?? false;
            ProjectOptions.TestSourcesRegExp = "^.*?(?:\\.s|S)pec\\.ts(?:x)?$";
            var publishConfigSection = parsed.GetValue("publishConfig") as JObject;
            if (publishConfigSection != null)
            {
                ProjectOptions.NpmRegistry = publishConfigSection.Value<string>("registry");
            }
            var bobrilSection = parsed.GetValue("bobril") as JObject;
            ProjectOptions.TypeScriptVersion = GetStringProperty(bobrilSection, "tsVersion", DefaultTypeScriptVersion);
            ProjectOptions.Title = GetStringProperty(bobrilSection, "title", "Bobril Application");
            ProjectOptions.HtmlHead = GetStringProperty(bobrilSection, "head", "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
            ProjectOptions.PrefixStyleNames = GetStringProperty(bobrilSection, "prefixStyleDefs", "");
            ProjectOptions.Example = GetStringProperty(bobrilSection, "example", "");
            ProjectOptions.AdditionalResourcesDirectory = GetStringProperty(bobrilSection, "additionalResourcesDirectory", null);
            ProjectOptions.BobrilJsx = true;
            ProjectOptions.CompilerOptions = bobrilSection != null ? TSCompilerOptions.Parse(bobrilSection.GetValue("compilerOptions") as JObject) : null;
            ProjectOptions.DependencyUpdate = String2DependencyUpdate(GetStringProperty(bobrilSection, "dependencies", "install"));
            if (bobrilSection == null)
            {
                return;
            }
        }

        DepedencyUpdate String2DependencyUpdate(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "disable":
                case "disabled":
                    return DepedencyUpdate.Disabled;
                case "update":
                case "upgrade":
                    return DepedencyUpdate.Upgrade;
                default:
                    return DepedencyUpdate.Install;
            }
        }

        public string GetStringProperty(JObject obj, string name, string @default)
        {
            if (obj != null && obj.TryGetValue(name, out var value) && value.Type == JTokenType.String)
                return (string)value;
            return @default;
        }

        public void FirstInitialize()
        {
            if (WasFirstInitialize)
                return;
            WasFirstInitialize = true;
            if (ProjectOptions.Localize)
            {
                ProjectOptions.TranslationDb = new Translation.TranslationDb(DiskCache.FsAbstraction);
                ProjectOptions.TranslationDb.AddLanguage(ProjectOptions.DefaultLanguage ?? "en-us");
                ProjectOptions.TranslationDb.LoadLangDbs(PathUtils.Join(Owner.FullPath, "translations"));
            }
        }

        public void Build(BuildCtx buildCtx)
        {
            var buildModuleCtx = new BuildModuleCtx()
            {
                _buildCtx = buildCtx,
                _owner = this,
                _result = new BuildResult(),
                ToCheck = new OrderedHashSet<string>(),
                ToCompile = new OrderedHashSet<string>(),
                ToCompileDts = new OrderedHashSet<string>()
            };
            ITSCompiler compiler = null;
            try
            {
                compiler = buildCtx.CompilerPool.GetTs();
                compiler.DiskCache = DiskCache;
                compiler.Ctx = buildModuleCtx;
                var compOpt = buildCtx.TSCompilerOptions.Clone();
                compOpt.rootDir = Owner.FullPath;
                compOpt.outDir = "_virtual";
                compOpt.module = ModuleKind.CommonJS;
                compOpt.declaration = true;
                ProjectOptions.Tools.SetTypeScriptVersion(ProjectOptions.TypeScriptVersion);
                compiler.MergeCompilerOptions(compOpt);
                compiler.MergeCompilerOptions(ProjectOptions.CompilerOptions);
                do
                {
                    buildModuleCtx.ChangedDts = false;
                    buildModuleCtx.CrawledCount = 0;
                    buildModuleCtx.TrullyCompiledCount = 0;
                    buildModuleCtx.ToCheck.Clear();
                    buildModuleCtx.ToCompile.Clear();
                    buildModuleCtx.ToCompileDts.Clear();
                    ProjectOptions.HtmlHeadExpanded = buildModuleCtx.ExpandHtmlHead(ProjectOptions.HtmlHead);
                    foreach (var src in buildCtx.Sources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(compOpt.rootDir, src));
                    }
                    buildModuleCtx.Crawl();
                    if (buildModuleCtx.ToCompile.Count != 0)
                    {
                        if (buildCtx.Verbose)
                            compiler.MeasurePerformance = true;
                        compiler.CreateProgram(Owner.FullPath, buildModuleCtx.ToCompile.Concat(buildModuleCtx.ToCompileDts).ToArray());
                        if (!compiler.CompileProgram())
                            break;
                        compiler.GatherSourceInfo();

                        if (!compiler.EmitProgram())
                            break;
                        buildModuleCtx.UpdateCacheIds();
                        buildModuleCtx.Crawl();
                    }
                } while (buildModuleCtx.ChangedDts || buildModuleCtx.TrullyCompiledCount < buildModuleCtx.ToCompile.Count);
                buildCtx.BuildResult = buildModuleCtx._result;
            }
            finally
            {
                if (compiler != null)
                    buildCtx.CompilerPool.ReleaseTs(compiler);
            }
        }

        public static TSProject FindInfoForModule(IDirectoryCache dir, IDiskCache diskCache, string moduleName, out string diskName)
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
                        diskName = mdir.Name;
                        diskCache.UpdateIfNeeded(mdir);
                        return Get(mdir, diskCache);
                    }
                }
                dir = dir.Parent;
            }
            diskName = null;
            return null;
        }

        public static TSProject Get(IDirectoryCache dir, IDiskCache diskCache)
        {
            if (dir == null)
                return null;
            if (dir.AdditionalInfo == null)
                dir.AdditionalInfo = new TSProject { Owner = dir, DiskCache = diskCache };
            return (TSProject)dir.AdditionalInfo;
        }
    }
}
