using Lib.TSCompiler;
using Lib.Utils.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTDB.Collections;
using Lib.DiskCache;
using Lib.Utils;
using Newtonsoft.Json;

namespace Lib.Composition
{
    public class BuildCtx
    {
        public BuildCtx(ICompilerPool compilerPool, DiskCache.DiskCache diskCache, bool verbose, ILogger logger, string currentDirectory)
        {
            Verbose = verbose;
            CompilerPool = compilerPool;
            _diskCache = diskCache;
            _logger = logger;
            _currentDirectory = currentDirectory;
        }

        RefDictionary<string, BuildCtx?>? _subBuildCtxs;

        string _mainFile;
        string _jasmineDts;
        List<string> _exampleSources;
        List<string> _testSources;
        string[] _additionalSources;
        ITSCompilerOptions _compilerOptions;
        ITSCompiler _typeChecker;

        bool _buildOnceOnly;
        bool _projectStructureChanged;
        bool _compilerOptionsChanged;

        readonly string _currentDirectory;

        string MainFile
        {
            get => _mainFile;
            set
            {
                if (!ReferenceEquals(value, _mainFile))
                {
                    _mainFile = value; _projectStructureChanged = true;
                }
            }
        }

        string JasmineDts
        {
            get => _jasmineDts;
            set
            {
                if (!ReferenceEquals(value, _jasmineDts))
                {
                    _jasmineDts = value; _projectStructureChanged = true;
                }
            }
        }

        List<string> ExampleSources
        {
            get => _exampleSources;
            set
            {
                if (!ReferenceEquals(value, _exampleSources))
                {
                    _exampleSources = value; _projectStructureChanged = true;
                }
            }
        }

        List<string> TestSources
        {
            get => _testSources;
            set
            {
                if (!ReferenceEquals(value, _testSources))
                {
                    _testSources = value; _projectStructureChanged = true;
                }
            }
        }

        string[] AdditionalSources
        {
            get => _additionalSources;
            set
            {
                if (!ReferenceEquals(value, _additionalSources))
                {
                    _additionalSources = value; _projectStructureChanged = true;
                }
            }
        }

        public ITSCompilerOptions CompilerOptions
        {
            get => _compilerOptions;
            set
            {
                if (!ReferenceEquals(value, _compilerOptions))
                {
                    _compilerOptions = value; _projectStructureChanged = true; _compilerOptionsChanged = true;
                }
            }
        }

        public readonly bool Verbose;
        public readonly ICompilerPool CompilerPool;
        readonly DiskCache.DiskCache _diskCache;
        readonly ILogger _logger;

        string? _lastTsVersion;
        List<Diagnostic>? _lastSemantics;

        void ShowTsVersion(string version)
        {
            if (_lastTsVersion != version)
            {
                _logger.Info("Using TypeScript version " + version);
                _lastTsVersion = version;
            }
        }

        enum TypeCheckChange
        {
            None = 0,
            Small = 1,
            Input = 2,
            Options = 3,
            Once = 4
        }

        TypeCheckChange _cancelledTypeCheckType;

        void DoTypeCheck(TypeCheckChange type)
        {
            if (_typeChecker == null)
            {
                type = TypeCheckChange.Options;
            }
            if (_buildOnceOnly)
            {
                type = TypeCheckChange.Once;
            }
            switch (type)
            {
                case TypeCheckChange.None:
                    return;
                case TypeCheckChange.Small:
                    _typeChecker.ClearDiagnostics();
                    _typeChecker.TriggerUpdate();
                    break;
                case TypeCheckChange.Input:
                    _typeChecker.ClearDiagnostics();
                    _typeChecker.UpdateProgram(MakeSourceListArray());
                    _typeChecker.TriggerUpdate();
                    break;
                case TypeCheckChange.Options:
                    if (_typeChecker != null)
                    {
                        CompilerPool.ReleaseTs(_typeChecker);
                        _typeChecker = null;
                    }
                    _typeChecker = CompilerPool.GetTs(_diskCache, CompilerOptions);
                    _typeChecker.ClearDiagnostics();
                    _typeChecker.CreateProgram(_currentDirectory, MakeSourceListArray());
                    break;
                case TypeCheckChange.Once:
                    if (_typeChecker != null)
                    {
                        CompilerPool.ReleaseTs(_typeChecker);
                        _typeChecker = null;
                    }
                    _typeChecker = CompilerPool.GetTs(_diskCache, CompilerOptions);
                    _typeChecker.ClearDiagnostics();
                    _typeChecker.CheckProgram(_currentDirectory, MakeSourceListArray());
                    _lastSemantics = _typeChecker.GetDiagnostics().ToList();
                    CompilerPool.ReleaseTs(_typeChecker);
                    _typeChecker = null;
                    return;
            }
            _lastSemantics = _typeChecker.GetDiagnostics().ToList();
        }

        TypeCheckChange DetectTypeCheckChange()
        {
            if (_typeChecker == null || _compilerOptionsChanged)
            {
                return TypeCheckChange.Options;
            }
            if (_projectStructureChanged)
            {
                return TypeCheckChange.Input;
            }
            return TypeCheckChange.Small;
        }

        CancellationTokenSource _cancellation;
        Task _typeCheckTask = Task.CompletedTask;

        public Task<List<Diagnostic>> StartTypeCheck()
        {
            _cancellation?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellation = cancellationTokenSource;
            var current = DetectTypeCheckChange();
            var res = _typeCheckTask.ContinueWith((_task, _state) => {
                current = (TypeCheckChange)Math.Max((int)_cancelledTypeCheckType, (int)current);
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    _cancelledTypeCheckType = current;
                    return null;
                }
                _cancelledTypeCheckType = TypeCheckChange.None;
                DoTypeCheck(current);
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return null;
                }
                return _lastSemantics;
            }, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.LongRunning);
            _typeCheckTask = res;
            return res;
        }

        string[] MakeSourceListArray()
        {
            var res = new List<string>();
            if (JasmineDts != null) res.Add(JasmineDts);
            if (AdditionalSources != null) res.AddRange(AdditionalSources);
            if (MainFile != null) res.Add(MainFile);
            if (ExampleSources != null) res.AddRange(ExampleSources);
            if (TestSources != null) res.AddRange(TestSources);
            return res.ToArray();
        }

        public void Build(ProjectOptions project, bool buildOnlyOnce, BuildResult buildResult,
            MainBuildResult mainBuildResult, int iterationId)
        {
            if (project.PreserveProjectRoot)
                mainBuildResult.PreserveProjectRoot = true;
            _buildOnceOnly = buildOnlyOnce;
            _compilerOptionsChanged = false;
            _projectStructureChanged = false;
            MainFile = project.MainFile;
            AdditionalSources = project.IncludeSources;
            ExampleSources = project.ExampleSources;
            TestSources = project.TestSources;
            JasmineDts = project.TestSources != null ? project.JasmineDts : null;
            CompilerOptions = project.FinalCompilerOptions;

            var tsProject = project.Owner;
            var tryDetectChanges = !_projectStructureChanged;
            buildResult.HasError = false;
            if (!buildResult.Incremental || !tryDetectChanges)
            {
                buildResult.RecompiledIncrementaly.Clear();
            }

            var buildModuleCtx = new BuildModuleCtx()
            {
                BuildCtx = this,
                Owner = tsProject,
                Result = buildResult,
                MainResult = mainBuildResult,
                ToCheck = new OrderedHashSet<string>(),
                IterationId = iterationId
            };
            try
            {
                project.BuildCache.StartTransaction();
                ITSCompiler compiler = null;
                try
                {
                    if (!tryDetectChanges)
                    {
                        if (!project.TypeScriptVersionOverride && tsProject.DevDependencies != null &&
                            tsProject.DevDependencies.Contains("typescript"))
                            project.Tools.SetTypeScriptPath(tsProject.Owner.FullPath);
                        else
                            project.Tools.SetTypeScriptVersion(project.TypeScriptVersion);
                        project.ExpandEnv();
                    }

                    compiler = CompilerPool.GetTs(tsProject.DiskCache, CompilerOptions);
                    var trueTSVersion = compiler.GetTSVersion();
                    ShowTsVersion(trueTSVersion);
                    project.ConfigurationBuildCacheId = project.BuildCache.MapConfiguration(trueTSVersion,
                        JsonConvert.SerializeObject(CompilerOptions, Formatting.None,
                            TSCompilerOptions.GetSerializerSettings()));
                }
                finally
                {
                    if (compiler != null)
                        CompilerPool.ReleaseTs(compiler);
                }

                mainBuildResult.MergeCommonSourceDirectory(tsProject.Owner.FullPath);
                buildResult.TaskForSemanticCheck = StartTypeCheck();
                if (tryDetectChanges)
                {
                    if (!buildModuleCtx.CrawlChanges())
                    {
                        buildResult.Incremental = true;
                        goto noDependencyChangeDetected;
                    }

                    _projectStructureChanged = true;
                    buildResult.Incremental = false;
                    buildResult.JavaScriptAssets.Clear();
                    foreach (var info in buildResult.Path2FileInfo)
                    {
                        info.Value.IterationId = iterationId - 1;
                    }
                }

                buildModuleCtx.CrawledCount = 0;
                buildModuleCtx.ToCheck.Clear();
                buildModuleCtx.ExpandHtmlHead(project.HtmlHead);
                if (MainFile != null)
                    buildModuleCtx.CheckAdd(PathUtils.Join(tsProject.Owner.FullPath, MainFile),
                        FileCompilationType.Unknown);
                if (ExampleSources != null)
                    foreach (var src in ExampleSources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(tsProject.Owner.FullPath, src),
                            FileCompilationType.Unknown);
                    }

                if (TestSources != null)
                    foreach (var src in TestSources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(tsProject.Owner.FullPath, src),
                            FileCompilationType.Unknown);
                    }

                if (project.IncludeSources != null)
                {
                    foreach (var src in project.IncludeSources)
                    {
                        buildModuleCtx.CheckAdd(PathUtils.Join(tsProject.Owner.FullPath, src),
                            FileCompilationType.Unknown);
                    }
                }

                buildModuleCtx.Crawl();
                noDependencyChangeDetected: ;
                if (project.SpriteGeneration) project.SpriteGenerator.ProcessNew();
                var hasError = false;
                foreach (var item in buildResult.Path2FileInfo)
                {
                    if (item.Value.HasError)
                    {
                        hasError = true;
                        break;
                    }
                }

                buildResult.HasError = hasError;
                if (project.BuildCache.IsEnabled)
                    buildModuleCtx.StoreResultToBuildCache(buildResult);
            }
            finally
            {
                project.BuildCache.EndTransaction();
            }
        }

        public void BuildSubProjects(ProjectOptions project, bool buildOnlyOnce, BuildResult buildResult, MainBuildResult mainBuildResult, int iterationId)
            {
            if (buildResult.HasError)
                return;
            var newSubProjects = new RefDictionary<string, ProjectOptions?>();
            var newSubBuildCtxs = new RefDictionary<string, BuildCtx>();
            var newSubBuildResults = new RefDictionary<string, BuildResult>();
            foreach (var item in buildResult.Path2FileInfo)
            {
                var si = item.Value.SourceInfo;
                if (si?.Assets == null) continue;
                foreach (var asset in si.Assets)
                {
                    if (asset.Name == null) continue;
                    if (asset.Name.StartsWith("project:"))
                    {
                        var projPath = asset.Name.Substring(8);
                        newSubProjects.GetOrAddValueRef(projPath);
                    }
                }
            }

            foreach (var u in newSubProjects.Index)
            {
                var projectPath = newSubProjects.KeyRef(u);
                if (newSubProjects.ValueRef(u) == null)
                {
                    if (project.SubProjects == null || !project.SubProjects.TryGetValue(projectPath, out var subProj))
                    {
                        var dirCache = _diskCache.TryGetItem(PathUtils.Join(project.Owner.Owner.FullPath, projectPath)) as IDirectoryCache;
                        var tsproj = TSProject.Create(dirCache, _diskCache, _logger, null);
                        tsproj.IsRootProject = true;
                        if (tsproj.ProjectOptions.BuildCache == null)
                            tsproj.ProjectOptions = new ProjectOptions
                                {
                                    Tools = project.Tools,
                                    BuildCache = project.BuildCache,
                                    Owner = tsproj,
                                    ForbiddenDependencyUpdate = project.ForbiddenDependencyUpdate
                                };
                        subProj = tsproj.ProjectOptions;
                    }
                    newSubProjects.ValueRef(u) = subProj;
                    subProj.UpdateFromProjectJson(false);
                    subProj.RefreshCompilerOptions();
                    subProj.RefreshMainFile();
                    if (_subBuildCtxs == null || !_subBuildCtxs.TryGetValue(projectPath, out var subBuildCtx))
                    {
                        subBuildCtx = new BuildCtx(CompilerPool, _diskCache, Verbose, _logger, subProj.Owner.Owner.FullPath);
                    }

                    newSubBuildCtxs.GetOrAddValueRef(projectPath) = subBuildCtx;

                    if (buildResult.SubBuildResults == null || !buildResult.SubBuildResults.TryGetValue(projectPath, out var subBuildResult))
                    {
                        subBuildResult = new BuildResult(mainBuildResult, subProj);
                    }

                    newSubBuildResults.GetOrAddValueRef(projectPath) = subBuildResult;
                    subBuildCtx.Build(subProj, buildOnlyOnce, subBuildResult, mainBuildResult, iterationId);
                    buildResult.HasError |= subBuildResult.HasError;
                    buildResult.TaskForSemanticCheck = Task.WhenAll(buildResult.TaskForSemanticCheck,
                        subBuildResult.TaskForSemanticCheck).ContinueWith(
                        subresults =>
                        {
                            return subresults.Result.SelectMany(d=>d).ToList();
                        });
                }
            }

            project.SubProjects = newSubProjects;
            _subBuildCtxs = newSubBuildCtxs;
            buildResult.SubBuildResults = newSubBuildResults;
        }
    }
}
