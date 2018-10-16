using Lib.Composition;
using Lib.DiskCache;
using Lib.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lib.Registry;
using Lib.Utils.Logger;

namespace Lib.TSCompiler
{
    public class TSProject
    {
        bool _wasFirstInitialize;

        public const string DefaultTypeScriptVersion = "3.0.3";

        public ILogger Logger { get; set; }
        public IDiskCache DiskCache { get; set; }
        public IDirectoryCache Owner { get; set; }
        public string MainFile { get; set; }
        public string TypesMainFile { get; set; }
        public ProjectOptions ProjectOptions { get; set; }
        public int PackageJsonChangeId { get; set; }
        public int InterfaceChangeId { get; set; }
        public bool IsRootProject { get; set; }
        public HashSet<string> Dependencies;
        public HashSet<string> DevDependencies;
        public HashSet<string> UsedDependencies;
        public string Name;

        public void LoadProjectJson(bool forbiddenDependencyUpdate)
        {
            DiskCache.UpdateIfNeeded(Owner);
            var packageJsonFile = Owner.TryGetChild("package.json");
            if (packageJsonFile is IFileCache cache)
            {
                var newChangeId = cache.ChangeId;
                if (newChangeId == PackageJsonChangeId) return;
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
                    if (!this.IsRootProject &&
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
                MainFile = "index.js";
                ProjectOptions?.FillProjectOptionsFromPackageJson(null);
            }
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
                ProjectOptions.BuildCache.StartTransaction();
                compiler = buildCtx.CompilerPool.GetTs();
                compiler.DiskCache = DiskCache;
                compiler.Ctx = buildModuleCtx;
                var compOpt = buildCtx.TSCompilerOptions.Clone();
                compOpt.rootDir = Owner.FullPath;
                compOpt.outDir = "_virtual";
                compOpt.module = ModuleKind.Commonjs;
                compOpt.declaration = true;
                if (!ProjectOptions.TypeScriptVersionOverride && DevDependencies != null &&
                    DevDependencies.Contains("typescript"))
                    ProjectOptions.Tools.SetTypeScriptPath(Owner.FullPath);
                else
                    ProjectOptions.Tools.SetTypeScriptVersion(ProjectOptions.TypeScriptVersion);
                compiler.MergeCompilerOptions(compOpt);
                compiler.MergeCompilerOptions(ProjectOptions.CompilerOptions);
                var positionIndependentOptions = compiler.CompilerOptions.Clone();
                positionIndependentOptions.rootDir = null;
                var trueTSVersion = compiler.GetTSVersion();
                buildCtx.ShowTsVersion(trueTSVersion);
                ProjectOptions.ConfigurationBuildCacheId = ProjectOptions.BuildCache.MapConfiguration(trueTSVersion,
                    JsonConvert.SerializeObject(positionIndependentOptions, Formatting.None,
                        new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore}));
                var wasSomeError = false;
                do
                {
                    buildModuleCtx.ChangedDts = false;
                    buildModuleCtx.CrawledCount = 0;
                    buildModuleCtx.ToCheck.Clear();
                    buildModuleCtx.ToCompile.Clear();
                    buildModuleCtx.ToCompileDts.Clear();
                    ProjectOptions.HtmlHeadExpanded = buildModuleCtx.ExpandHtmlHead(ProjectOptions.HtmlHead);
                    foreach (var src in buildCtx.Sources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(compOpt.rootDir, src));
                    }

                    if (ProjectOptions.IncludeSources != null)
                    {
                        foreach (var src in ProjectOptions.IncludeSources)
                        {
                            buildModuleCtx.CheckAdd(PathUtils.Join(compOpt.rootDir, src));
                        }
                    }

                    buildModuleCtx.Crawl();
                    if (buildModuleCtx.ToCompile.Count != 0)
                    {
                        if (buildCtx.Verbose)
                            compiler.MeasurePerformance = true;
                        compiler.CreateProgram(Owner.FullPath,
                            buildModuleCtx.ToCompile.Concat(buildModuleCtx.ToCompileDts).ToArray());
                        if (!compiler.CompileProgram())
                        {
                            wasSomeError = true;
                            break;
                        }

                        ProjectOptions.CurrentBuildCommonSourceDirectory = compiler.CommonSourceDirectory;
                        ProjectOptions.CommonSourceDirectory = ProjectOptions.CommonSourceDirectory == null
                            ? compiler.CommonSourceDirectory
                            : PathUtils.CommonDir(ProjectOptions.CommonSourceDirectory, compiler.CommonSourceDirectory);
                        compiler.GatherSourceInfo();
                        if (ProjectOptions.SpriteGeneration)
                            ProjectOptions.SpriteGenerator.ProcessNew();
                        if (!compiler.EmitProgram())
                        {
                            wasSomeError = true;
                            break;
                        }

                        buildModuleCtx.UpdateCacheIds();
                        buildModuleCtx.ToCompile.Clear();
                        buildModuleCtx.Crawl();
                    }
                } while (buildModuleCtx.ChangedDts || 0 < buildModuleCtx.ToCompile.Count);

                if (ProjectOptions.BuildCache.IsEnabled && !wasSomeError) ProjectOptions.StoreResultToBuildCache(buildModuleCtx._result);
                buildCtx.BuildResult = buildModuleCtx._result;
            }
            finally
            {
                if (compiler != null)
                    buildCtx.CompilerPool.ReleaseTs(compiler);
                ProjectOptions.BuildCache.EndTransaction();
            }
        }

        public static TSProject FindInfoForModule(IDirectoryCache dir, IDiskCache diskCache, ILogger logger, string moduleName,
            out string diskName)
        {
            while (!dir.IsFake)
            {
                diskCache.UpdateIfNeeded(dir);
                if (dir.TryGetChild("node_modules") is IDirectoryCache nmdir)
                {
                    diskCache.UpdateIfNeeded(nmdir);
                    if (nmdir.TryGetChild(moduleName) is IDirectoryCache mdir)
                    {
                        diskName = mdir.Name;
                        diskCache.UpdateIfNeeded(mdir);
                        return Get(mdir, diskCache, logger, diskName);
                    }
                }

                dir = dir.Parent;
            }

            diskName = null;
            return null;
        }

        public static TSProject Get(IDirectoryCache dir, IDiskCache diskCache, ILogger logger, string diskName)
        {
            if (dir == null)
                return null;
            if (dir.AdditionalInfo == null)
                dir.AdditionalInfo = new TSProject {Owner = dir, DiskCache = diskCache, Logger = logger, Name = diskName };
            return (TSProject) dir.AdditionalInfo;
        }
    }
}
