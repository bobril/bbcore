using Lib.Composition;
using Lib.DiskCache;
using Lib.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BTDB.Collections;
using Lib.Registry;
using Lib.Utils.Logger;
using Njsast;

namespace Lib.TSCompiler
{
    public class TSProject
    {
        bool _wasFirstInitialize;

        public const string DefaultTypeScriptVersion = "3.4.5";

        public ILogger Logger { get; set; }
        public IDiskCache DiskCache { get; set; }
        public IDirectoryCache Owner { get; set; }
        public string MainFile { get; set; }

        public bool MainFileNeedsToBeCompiled;

        public string TypesMainFile { get; set; }
        public ProjectOptions ProjectOptions { get; set; }
        public int PackageJsonChangeId { get; set; }
        public bool IsRootProject { get; set; }

        public HashSet<string> Dependencies;
        public HashSet<string> DevDependencies;
        public HashSet<string> UsedDependencies;
        public Dictionary<string, string> Assets;
        public string Name;
        internal int IterationId;
        internal StructList<string> NegativeChecks;
        internal bool Valid;

        public void LoadProjectJson(bool forbiddenDependencyUpdate)
        {
            DiskCache.UpdateIfNeeded(Owner);
            var packageJsonFile = Owner.TryGetChild("package.json");
            if (packageJsonFile is IFileCache cache)
            {
                var newChangeId = cache.ChangeId;
                if (newChangeId == PackageJsonChangeId) return;
                ProjectOptions.FinalCompilerOptions = null;
                MainFileNeedsToBeCompiled = false;
                JObject parsed;
                try
                {
                    parsed = JObject.Parse(cache.Utf8Content);
                }
                catch (Exception)
                {
                    parsed = new JObject();
                }

                var deps = new HashSet<string>();
                var devdeps = new HashSet<string>();
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

                if (parsed.GetValue("module") is JValue moduleV)
                {
                    MainFile = PathUtils.Normalize(moduleV.ToString());
                    MainFileNeedsToBeCompiled = true;
                    if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, MainFile)) is IFileCache)
                    {
                        hasMain = true;
                        if (parsed.GetValue("typings") is JValue mainV)
                        {
                            TypesMainFile = PathUtils.Normalize(mainV.ToString());
                        }
                    }
                }

                if (!hasMain)
                {
                    if (parsed.GetValue("main") is JValue mainV2)
                    {
                        MainFile = PathUtils.Normalize(mainV2.ToString());
                    }
                    else
                    {
                        MainFile = "index.js";
                    }

                    if (DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, PathUtils.ChangeExtension(MainFile, "ts")))
                        is IFileCache fileAsTs)
                    {
                        MainFile = PathUtils.ChangeExtension(MainFile, "ts");
                        TypesMainFile = null;
                        hasMain = true;
                    }
                    else
                    {
                        fileAsTs = DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath,
                            PathUtils.ChangeExtension(MainFile, "tsx"))) as IFileCache;
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
                    if (!IsRootProject &&
                        !(DiskCache.TryGetItem(PathUtils.Join(Owner.FullPath, TypesMainFile)) is IFileCache))
                    {
                        var typesDts = PathUtils.Join(Owner.FullPath, $"../@types/{Owner.Name}/index.d.ts");
                        if (DiskCache.TryGetItem(typesDts) is IFileCache)
                        {
                            TypesMainFile = typesDts;
                        }
                    }
                }

                if (parsed.GetValue("dependencies") is JObject parsedV)
                {
                    foreach (var i in parsedV.Properties())
                    {
                        deps.Add(i.Name);
                    }
                }

                if (parsed.GetValue("devDependencies") is JObject parsedV2)
                {
                    foreach (var i in parsedV2.Properties())
                    {
                        devdeps.Add(i.Name);
                    }
                }

                PackageJsonChangeId = newChangeId;
                Dependencies = deps;
                DevDependencies = devdeps;
                Assets = ParseBobrilAssets(parsed);

                if (ProjectOptions == null) return;
                ProjectOptions.FillProjectOptionsFromPackageJson(parsed);
                if (forbiddenDependencyUpdate || ProjectOptions.DependencyUpdate == DepedencyUpdate.Disabled) return;
                var packageManager = new CurrentNodePackageManager(DiskCache, Logger);
                if (ProjectOptions.DependencyUpdate == DepedencyUpdate.Upgrade)
                {
                    packageManager.UpgradeAll(Owner);
                }
                else
                {
                    packageManager.Install(Owner);
                }

                DiskCache.CheckForTrueChange();
                DiskCache.ResetChange();
            }
            else
            {
                PackageJsonChangeId = -1;
                MainFile = "index.js";
                Dependencies = new HashSet<string>();
                DevDependencies = new HashSet<string>();
                Assets = null;
                ProjectOptions?.FillProjectOptionsFromPackageJson(null);
            }
        }

        Dictionary<string, string> ParseBobrilAssets(JObject parsed)
        {
            Dictionary<string, string> res = null;
            var bobrilSection = parsed?.GetValue("bobril") as JObject;
            if (bobrilSection == null)
                return res;
            var assetsJson = bobrilSection.GetValue("assets") as JObject;
            if (assetsJson == null)
                return res;
            foreach (var (key, value) in assetsJson)
            {
                if (value.Type == JTokenType.String)
                {
                    if (res == null)
                    {
                        res = new Dictionary<string, string>();
                    }

                    res.Add(key, value.Value<string>());
                }
            }

            return res;
        }

        public void InitializeOnce()
        {
            if (_wasFirstInitialize)
                return;
            _wasFirstInitialize = true;
            if (ProjectOptions.Localize)
            {
                ProjectOptions.InitializeTranslationDb();
            }

            var bbTslint = DevDependencies?.FirstOrDefault(s => s.StartsWith("bb-tslint"));
            if (bbTslint != null)
            {
                var srcTsLint = PathUtils.Join(Owner.FullPath, $"node_modules/{bbTslint}/tslint.json");
                var srcFile = DiskCache.TryGetItem(srcTsLint) as IFileCache;
                var dstTsLint = PathUtils.Join(Owner.FullPath, "tslint.json");
                if (srcFile != null && (!(DiskCache.TryGetItem(dstTsLint) is IFileCache dstFile) ||
                                        !dstFile.HashOfContent.SequenceEqual(srcFile.HashOfContent)))
                {
                    File.WriteAllBytes(dstTsLint, srcFile.ByteContent);
                    Console.WriteLine($"Updated tslint.json from {srcTsLint}");
                }
            }
        }

        public void Build(BuildCtx buildCtx, BuildResult buildResult, int iterationId)
        {
            var tryDetectChanges = !buildCtx.ProjectStructureChanged;
            if (!buildResult.Incremental || !tryDetectChanges)
            {
                buildResult.RecompiledIncrementaly.Clear();
            }
            var buildModuleCtx = new BuildModuleCtx()
            {
                BuildCtx = buildCtx,
                Owner = this,
                Result = buildResult,
                ToCheck = new OrderedHashSet<string>(),
                IterationId = iterationId
            };
            try
            {
                ProjectOptions.BuildCache.StartTransaction();
                ITSCompiler compiler = null;
                try
                {
                    if (!tryDetectChanges)
                    {
                        if (!ProjectOptions.TypeScriptVersionOverride && DevDependencies != null &&
                            DevDependencies.Contains("typescript"))
                            ProjectOptions.Tools.SetTypeScriptPath(Owner.FullPath);
                        else
                            ProjectOptions.Tools.SetTypeScriptVersion(ProjectOptions.TypeScriptVersion);
                    }
                    compiler = buildCtx.CompilerPool.GetTs(DiskCache, buildCtx.CompilerOptions);
                    var trueTSVersion = compiler.GetTSVersion();
                    buildCtx.ShowTsVersion(trueTSVersion);
                    ProjectOptions.ConfigurationBuildCacheId = ProjectOptions.BuildCache.MapConfiguration(trueTSVersion,
                        JsonConvert.SerializeObject(buildCtx.CompilerOptions, Formatting.None, TSCompilerOptions.GetSerializerSettings()));
                }
                finally
                {
                    if (compiler != null)
                        buildCtx.CompilerPool.ReleaseTs(compiler);
                }
                if (buildModuleCtx.Result.CommonSourceDirectory == null)
                {
                    buildModuleCtx.Result.CommonSourceDirectory = Owner.FullPath;
                }
                buildCtx.StartTypeCheck(ProjectOptions);
                if (tryDetectChanges)
                {
                    if (!buildModuleCtx.CrawlChanges())
                    {
                        buildResult.Incremental = true;
                        goto noDependencyChangeDetected;
                    }
                    buildCtx.ProjectStructureChanged = true;
                    buildResult.Incremental = false;
                    buildModuleCtx.Result.JavaScriptAssets.Clear();
                    foreach (var info in buildModuleCtx.Result.Path2FileInfo)
                    {
                        info.Value.IterationId = iterationId - 1;
                    }
                }
                buildModuleCtx.CrawledCount = 0;
                buildModuleCtx.ToCheck.Clear();
                ProjectOptions.HtmlHeadExpanded = buildModuleCtx.ExpandHtmlHead(ProjectOptions.HtmlHead);
                if (buildCtx.MainFile != null) buildModuleCtx.CheckAdd(PathUtils.Join(Owner.FullPath, buildCtx.MainFile), FileCompilationType.Unknown);
                if (buildCtx.ExampleSources != null) foreach (var src in buildCtx.ExampleSources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(Owner.FullPath, src), FileCompilationType.Unknown);
                    }
                if (buildCtx.TestSources != null) foreach (var src in buildCtx.TestSources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(Owner.FullPath, src), FileCompilationType.Unknown);
                    }

                if (ProjectOptions.IncludeSources != null)
                {
                    foreach (var src in ProjectOptions.IncludeSources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(Owner.FullPath, src), FileCompilationType.Unknown);
                    }
                }

                buildModuleCtx.Crawl();
            noDependencyChangeDetected:;
                ProjectOptions.CommonSourceDirectory = buildModuleCtx.Result.CommonSourceDirectory;
                if (ProjectOptions.SpriteGeneration)
                    ProjectOptions.SpriteGenerator.ProcessNew();
                var hasError = false;
                foreach(var item in buildModuleCtx.Result.Path2FileInfo)
                {
                    if (item.Value.HasError)
                    {
                        hasError = true;
                        break;
                    }
                }
                buildModuleCtx.Result.HasError = hasError;
                if (ProjectOptions.BuildCache.IsEnabled)
                    buildModuleCtx.StoreResultToBuildCache(buildModuleCtx.Result);
            }
            finally
            {
                ProjectOptions.BuildCache.EndTransaction();
            }
        }

        public static TSProject FindInfoForModule(IDirectoryCache projectDir, IDirectoryCache dir, IDiskCache diskCache,
            ILogger logger,
            string moduleName,
            out string diskName)
        {
            if (projectDir.TryGetChild("node_modules") is IDirectoryCache pnmdir)
            {
                diskCache.UpdateIfNeeded(pnmdir);
                if (pnmdir.TryGetChild(moduleName) is IDirectoryCache mdir)
                {
                    diskName = mdir.Name;
                    diskCache.UpdateIfNeeded(mdir);
                    return Create(mdir, diskCache, logger, diskName);
                }
            }

            while (dir != null)
            {
                if (diskCache.TryGetItem(PathUtils.Join(dir.FullPath, "node_modules")) is IDirectoryCache nmdir)
                {
                    diskCache.UpdateIfNeeded(nmdir);
                    if (nmdir.TryGetChild(moduleName) is IDirectoryCache mdir)
                    {
                        diskName = mdir.Name;
                        diskCache.UpdateIfNeeded(mdir);
                        return Create(mdir, diskCache, logger, diskName);
                    }
                }

                dir = dir.Parent;
            }

            diskName = null;
            return null;
        }

        public static TSProject Create(IDirectoryCache dir, IDiskCache diskCache, ILogger logger, string diskName)
        {
            if (dir == null)
                return null;
            var proj = new TSProject
            {
                Owner = dir,
                DiskCache = diskCache,
                Logger = logger,
                Name = diskName,
                Valid = true,
                ProjectOptions = new ProjectOptions()
            };
            proj.ProjectOptions.Owner = proj;
            return proj;
        }

        internal static TSProject CreateInvalid(string name)
        {
            var proj = new TSProject
            {
                Name = name,
                Valid = false
            };
            return proj;
        }

        public void FillOutputByAssets(RefDictionary<string, object> filesContent, BuildResult buildResult,
            string nodeModulesDir, ProjectOptions projectOptions)
        {
            if (Assets == null) return;
            foreach (var asset in Assets)
            {
                var fromModules = asset.Key.StartsWith("node_modules/");
                var fullPath = fromModules ? nodeModulesDir : Owner.FullPath;
                if (fromModules)
                {
                    if (projectOptions.Owner.UsedDependencies == null)
                        projectOptions.Owner.UsedDependencies = new HashSet<string>();
                    var pos = 0;
                    PathUtils.EnumParts(asset.Key, ref pos, out var name, out var isDir);
                    PathUtils.EnumParts(asset.Key, ref pos, out name, out isDir);
                    projectOptions.Owner.UsedDependencies.Add(name.ToString());

                }
                var item = DiskCache.TryGetItem(PathUtils.Join(fullPath, asset.Key));
                if (item == null || item.IsInvalid)
                    continue;
                if (item is IFileCache)
                {
                    buildResult.TakenNames.Add(asset.Value);
                    filesContent.GetOrAddValueRef(asset.Value) = new Lazy<object>(() =>
                    {
                        var res = ((IFileCache)item).ByteContent;
                        ((IFileCache)item).FreeCache();
                        return res;
                    });
                }
                else
                {
                    RecursiveAddFilesContent(item as IDirectoryCache, filesContent, buildResult, asset.Value);
                }
            }
        }

        void RecursiveAddFilesContent(IDirectoryCache directory, RefDictionary<string, object> filesContent,
            BuildResult buildResult, string destDir)
        {
            DiskCache.UpdateIfNeeded(directory);
            foreach (var child in directory)
            {
                if (child.IsInvalid)
                    continue;
                var outPathFileName = destDir + "/" + child.Name;
                buildResult.TakenNames.Add(outPathFileName);
                if (child is IDirectoryCache)
                {
                    RecursiveAddFilesContent(child as IDirectoryCache, filesContent, buildResult, outPathFileName);
                    continue;
                }

                if (child is IFileCache)
                {
                    filesContent.GetOrAddValueRef(outPathFileName) =
                        new Lazy<object>(() =>
                        {
                            var res = ((IFileCache)child).ByteContent;
                            ((IFileCache)child).FreeCache();
                            return res;
                        });
                }
            }
        }
    }
}
