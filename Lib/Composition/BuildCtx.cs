using Lib.TSCompiler;
using Lib.Utils.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTDB.Collections;
using Lib.BuildCache;
using Lib.DiskCache;
using Lib.Utils;
using Newtonsoft.Json;

namespace Lib.Composition;

public enum RunTypeCheck
{
    Yes,
    No,
    Only
}

public class BuildCtx
{
    BuildCtx(ICompilerPool compilerPool, DiskCache.DiskCache diskCache, bool verbose, ILogger logger,
        string currentDirectory, IBuildCache buildCache, RunTypeCheck typeCheck)
    {
        Verbose = verbose;
        CompilerPool = compilerPool;
        _diskCache = diskCache;
        _logger = logger;
        _currentDirectory = currentDirectory;
        _typeCheck = typeCheck;
        BuildCache = buildCache;
    }

    public BuildCtx(ICompilerPool compilerPool, DiskCache.DiskCache diskCache, bool verbose, ILogger logger,
        string currentDirectory, IBuildCache buildCache, string typeCheckValue) : this(compilerPool, diskCache, verbose, logger,
        currentDirectory, buildCache, typeCheckValue switch
        {
            "yes" => RunTypeCheck.Yes, "no" => RunTypeCheck.No, "only" => RunTypeCheck.Only,
            _ => throw new ArgumentException("typeCheck parameter is not valid: " + typeCheckValue)
        })
    {
    }

    RefDictionary<string, BuildCtx>? _subBuildCtxs;

    readonly RunTypeCheck _typeCheck;
    string? _mainFile;
    string? _jasmineDts;
    List<string>? _exampleSources;
    List<string>? _testSources;
    string[]? _additionalSources;
    // Always assigned in Build before CompilerOptions is read
    ITSCompilerOptions _compilerOptions = null!;
    ITSCompiler? _typeChecker;
    public readonly IBuildCache BuildCache;

    bool _includeTestSourcesInBuild;
    bool _buildOnceOnly;
    bool _projectStructureChanged;
    bool _compilerOptionsChanged;

    readonly string _currentDirectory;

    public bool OnlyTypeCheck => _typeCheck == RunTypeCheck.Only;

    string? MainFile
    {
        get => _mainFile;
        set
        {
            if (!ReferenceEquals(value, _mainFile))
            {
                _mainFile = value;
                _projectStructureChanged = true;
            }
        }
    }

    string? JasmineDts
    {
        get => _jasmineDts;
        set
        {
            if (value != _jasmineDts)
            {
                _jasmineDts = value;
                _projectStructureChanged = true;
            }
        }
    }

    List<string>? ExampleSources
    {
        get => _exampleSources;
        set
        {
            if (!ReferenceEquals(value, _exampleSources))
            {
                _exampleSources = value;
                _projectStructureChanged = true;
            }
        }
    }

    List<string>? TestSources
    {
        get => _testSources;
        set
        {
            if (!ReferenceEquals(value, _testSources))
            {
                _testSources = value;
                _projectStructureChanged = true;
            }
        }
    }

    string[]? AdditionalSources
    {
        get => _additionalSources;
        set
        {
            if (!ReferenceEquals(value, _additionalSources))
            {
                _additionalSources = value;
                _projectStructureChanged = true;
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
                _compilerOptions = value;
                _projectStructureChanged = true;
                _compilerOptionsChanged = true;
            }
        }
    }

    public readonly bool Verbose;
    public readonly ICompilerPool CompilerPool;
    readonly DiskCache.DiskCache _diskCache;
    readonly ILogger _logger;
    string? _nativeTypeScriptDirectory;
    NativeTypeScriptKind _nativeTypeScriptKind;

    // tsrs or TypeScript 7.1+ tsc used for transpilation through its API
    public string? NativeTranspilerExecutable { get; private set; }

    // tsrs (or TypeScript 7.1+ tsc where tsrs is not available) compared with regular transpiler by validate: "tsrs"
    public string? ValidateTranspilerExecutable { get; private set; }

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

    void ShowNativeTranspiler(string name, string version)
    {
        var key = name + " " + version;
        if (_lastTsVersion != key)
        {
            _logger.Info("Using " + (name == "tsc" ? "TypeScript 7 (tsc)" : name) + " transpiler version " + version);
            _lastTsVersion = key;
        }
    }

    void ShowBuildinTranspiler()
    {
        const string version = "Buildin";
        if (_lastTsVersion != version)
        {
            _logger.Info("Using Build in transpiler");
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
    public double LastTypeCheckDurationSeconds { get; private set; }

    ITSCompiler CreateTypeChecker()
    {
        return _nativeTypeScriptDirectory != null
            ? new NativeTsCompiler(_nativeTypeScriptDirectory, _logger, _nativeTypeScriptKind)
            : CompilerPool.GetTs(_diskCache, CompilerOptions);
    }

    void ReleaseTypeChecker(ITSCompiler compiler)
    {
        if (ReferenceEquals(compiler, _typeChecker) && _nativeTypeScriptDirectory != null)
            compiler.Dispose();
        else
            CompilerPool.ReleaseTs(compiler);
    }

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
                // type is forced to Options above when _typeChecker is null
                _typeChecker!.ClearDiagnostics();
                _typeChecker.TriggerUpdate();
                break;
            case TypeCheckChange.Input:
                // type is forced to Options above when _typeChecker is null
                _typeChecker!.ClearDiagnostics();
                _typeChecker.UpdateProgram(MakeSourceListArray());
                _typeChecker.TriggerUpdate();
                break;
            case TypeCheckChange.Options:
                if (_typeChecker != null)
                {
                    ReleaseTypeChecker(_typeChecker);
                    _typeChecker = null;
                }

                _typeChecker = CreateTypeChecker();
                if (_nativeTypeScriptDirectory != null)
                    ShowTsVersion(_typeChecker.GetTSVersion());
                _typeChecker.ClearDiagnostics();
                _typeChecker.CreateProgram(_currentDirectory, MakeSourceListArray());
                break;
            case TypeCheckChange.Once:
                if (_typeChecker != null)
                {
                    ReleaseTypeChecker(_typeChecker);
                    _typeChecker = null;
                }

                _typeChecker = CreateTypeChecker();
                if (_nativeTypeScriptDirectory != null)
                    ShowTsVersion(_typeChecker.GetTSVersion());
                _typeChecker.ClearDiagnostics();
                _typeChecker.CheckProgram(_currentDirectory, MakeSourceListArray());
                _lastSemantics = _typeChecker.GetDiagnostics().ToList();
                ReleaseTypeChecker(_typeChecker);
                _typeChecker = null;
                return;
        }

        // Small, Input and Options cases all leave _typeChecker assigned
        _lastSemantics = _typeChecker!.GetDiagnostics().ToList();
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

    CancellationTokenSource? _cancellation;
    Task _typeCheckTask = Task.CompletedTask;

    public Task<List<Diagnostic>> StartTypeCheck()
    {
        _cancellation?.Cancel();
        var cancellationTokenSource = new CancellationTokenSource();
        _cancellation = cancellationTokenSource;
        if (_typeCheck == RunTypeCheck.No)
            return Task.FromResult(new List<Diagnostic>());
        var current = DetectTypeCheckChange();
        var res = _typeCheckTask.ContinueWith<List<Diagnostic>?>((_task, _state) =>
        {
            current = (TypeCheckChange) Math.Max((int) _cancelledTypeCheckType, (int) current);
            if (cancellationTokenSource.IsCancellationRequested)
            {
                _cancelledTypeCheckType = current;
                return null;
            }

            _cancelledTypeCheckType = TypeCheckChange.None;
            var typeCheckStart = DateTime.UtcNow;
            DoTypeCheck(current);
            LastTypeCheckDurationSeconds = (DateTime.UtcNow - typeCheckStart).TotalSeconds;
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return null;
            }

            return _lastSemantics;
        }, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.LongRunning);
        _typeCheckTask = res;
        // Result is null when the type check was cancelled; consumers of BuildResult.TaskForSemanticCheck handle it
        return res!;
    }

    string[] MakeSourceListArray()
    {
        var res = new List<string>();
        if (JasmineDts != null) res.Add(JasmineDts);
        if (AdditionalSources != null) res.AddRange(AdditionalSources);
        if (MainFile != null) res.Add(MainFile);
        if (ExampleSources != null) res.AddRange(ExampleSources);
        if (_includeTestSourcesInBuild && TestSources != null) res.AddRange(TestSources);
        return res.ToArray();
    }

    public void Build(
        ProjectOptions project,
        bool buildOnlyOnce,
        BuildResult buildResult,
        MainBuildResult mainBuildResult,
        int iterationId,
        bool includeTestSourcesInBuild = true)
    {
        if (project.PreserveProjectRoot)
            mainBuildResult.PreserveProjectRoot = true;
        _buildOnceOnly = buildOnlyOnce;
        _includeTestSourcesInBuild = includeTestSourcesInBuild;
        _compilerOptionsChanged = false;
        _projectStructureChanged = false;
        MainFile = project.MainFile;
        AdditionalSources = project.IncludeSources;
        ExampleSources = project.ExampleSources;
        TestSources = project.TestSources;
        JasmineDts = project.TestSources != null ? project.JasmineDts : null;
        CompilerOptions = project.FinalCompilerOptions!;
        var (tsrsExecutable, tscExecutable) = ResolveNativeCompilers(project);
        var (newNativeTypeScriptKind, newNativeTypeScriptDirectory) =
            DetectNativeTypeScriptDirectory(project, tsrsExecutable, tscExecutable);
        NativeTranspilerExecutable = project.ValidateTsrs ? null
            : tsrsExecutable != null && project.TsrsTranspile ? tsrsExecutable
            : tscExecutable != null && (!project.Tsrs || project.TsrsTranspile) ? tscExecutable
            : null;
        ValidateTranspilerExecutable = project.ValidateTsrs ? tsrsExecutable ?? tscExecutable : null;
        if (newNativeTypeScriptDirectory != _nativeTypeScriptDirectory ||
            newNativeTypeScriptKind != _nativeTypeScriptKind)
        {
            if (_typeChecker != null)
            {
                ReleaseTypeChecker(_typeChecker);
                _typeChecker = null;
            }

            _nativeTypeScriptDirectory = newNativeTypeScriptDirectory;
            _nativeTypeScriptKind = newNativeTypeScriptKind;
            _compilerOptionsChanged = true;
            if (_nativeTypeScriptDirectory != null)
            {
                project.UpdateTSConfigJson();
                _logger.Info("Using " + NativeTsCompiler.GetDisplayName(_nativeTypeScriptKind) + " typecheck from " +
                             _nativeTypeScriptDirectory);
            }
        }

        var tsProject = project.Owner;
        var tryDetectChanges = !_projectStructureChanged;
        buildResult.HasError = false;
        if (!buildResult.Incremental || !tryDetectChanges)
        {
            buildResult.RecompiledIncrementally.Clear();
        }

        var buildModuleCtx = new BuildModuleCtx
        {
            BuildCtx = this,
            Owner = tsProject,
            Result = buildResult,
            MainResult = mainBuildResult,
            ToCheck = new(),
            IterationId = iterationId,
        };
        try
        {
            BuildCache.StartTransaction();
            ITSCompiler? compiler = null;
            try
            {
                if (!tryDetectChanges)
                {
                    // TypeScript 7 has no typescript.js, regular TypeScript stays available for everything else
                    if (_projectTypeScriptIsNative ||
                        project.TypeScriptVersionOverride &&
                        TypeScriptNativeInstaller.IsNativeVersion(project.TypeScriptVersion))
                        project.Tools.SetTypeScriptVersion(ProjectOptions.DefaultTypeScriptVersion);
                    else if (!project.TypeScriptVersionOverride && 
                        ((tsProject.DevDependencies?.Contains("typescript") ?? false) 
                         || (tsProject.Dependencies?.Contains("typescript") ?? false)))
                        project.Tools.SetTypeScriptPath(tsProject.Owner.FullPath);
                    else
                        project.Tools.SetTypeScriptVersion(project.TypeScriptVersion!);
                    project.ExpandEnv();
                }

                if (NjsastTsValidator.IsBuildinEnabled(project.Future, project.Validate))
                {
                    ShowBuildinTranspiler();
                    project.ConfigurationBuildCacheId = BuildCache.MapConfiguration(NjsastTsValidator.BuildinTranspilerCacheKey,
                        JsonConvert.SerializeObject(CompilerOptions, Formatting.None,
                            TSCompilerOptions.GetSerializerSettings()));
                }
                else if (NativeTranspilerExecutable != null)
                {
                    var nativeName = NativeApiTranspiler.GetName(NativeTranspilerExecutable);
                    var nativeVersion = NativeApiTranspiler.GetVersion(NativeTranspilerExecutable);
                    ShowNativeTranspiler(nativeName, nativeVersion);
                    project.ConfigurationBuildCacheId = BuildCache.MapConfiguration(nativeName + "-" + nativeVersion,
                        JsonConvert.SerializeObject(CompilerOptions, Formatting.None,
                            TSCompilerOptions.GetSerializerSettings()));
                }
                else
                {
                    compiler = CompilerPool.GetTs(tsProject.DiskCache, CompilerOptions!);
                    var trueTsVersion = compiler.GetTSVersion();
                    ShowTsVersion(trueTsVersion);
                    project.ConfigurationBuildCacheId = BuildCache.MapConfiguration(trueTsVersion,
                        JsonConvert.SerializeObject(CompilerOptions, Formatting.None,
                            TSCompilerOptions.GetSerializerSettings()));
                }
            }
            finally
            {
                if (compiler != null)
                    CompilerPool.ReleaseTs(compiler);
            }

            mainBuildResult.MergeCommonSourceDirectory(tsProject.Owner.FullPath);
            if (_nativeTypeScriptDirectory != null)
                project.UpdateTSConfigJson();
            if (_nativeTypeScriptDirectory == null)
                buildResult.TaskForSemanticCheck = StartTypeCheck();
            if (_typeCheck == RunTypeCheck.Only)
            {
                buildResult.TaskForSemanticCheck = StartTypeCheck();
                return;
            }
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
            else
            {
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

            if (_includeTestSourcesInBuild && TestSources != null)
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
            if (iterationId > 1)
            {
                var toClear = new StructList<string>();
                foreach (var fi in buildResult.Path2FileInfo)
                {
                    switch (fi.Value.Type)
                    {
                        case FileCompilationType.MdxbList:
                        case FileCompilationType.Mdxb when !(fi.Value.Owner?.IsInvalid ?? true):
                            continue;
                    }

                    if (fi.Value.IterationId != iterationId)
                        toClear.Add(fi.Key);
                }

                foreach (var name in toClear)
                {
                    buildResult.Path2FileInfo.Remove(name);
                }
            }

            noDependencyChangeDetected: ;
            if (_nativeTypeScriptDirectory != null)
                buildResult.TaskForSemanticCheck = StartTypeCheck();
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
            if (BuildCache.IsEnabled)
                buildModuleCtx.StoreResultToBuildCache(buildResult);
        }
        finally
        {
            BuildCache.EndTransaction();
        }
    }

    (NativeTypeScriptKind, string?) DetectNativeTypeScriptDirectory(ProjectOptions project, string? tsrsExecutable,
        string? tscExecutable)
    {
        if (project.Tsrs && tsrsExecutable != null)
            return (NativeTypeScriptKind.Tsrs, tsrsExecutable);
        if (tscExecutable != null)
            return (NativeTypeScriptKind.Tsc, tscExecutable);
        if (project.Tsrs)
        {
            // Fallback like gots, without failing when tsgo is not found either
            if (TryFindNativeTypeScriptDirectory(project.Owner.Owner.FullPath, _diskCache.FsAbstraction,
                    out var fallbackDirectory))
                return (NativeTypeScriptKind.Tsgo, fallbackDirectory);
            if (NativeTsCompiler.TryFindGlobalTsgoExecutable(out var fallbackTsgo))
                return (NativeTypeScriptKind.Tsgo, fallbackTsgo);
            return (NativeTypeScriptKind.Tsgo, null);
        }

        var tsProject = project.Owner;
        if (!project.GoTs && (project.TypeScriptVersionOverride ||
                              !(tsProject.DevDependencies?.Contains("@typescript/native-preview") ?? false)))
            return (NativeTypeScriptKind.Tsgo, null);

        if (TryFindNativeTypeScriptDirectory(tsProject.Owner.FullPath, _diskCache.FsAbstraction, out var typeScriptDir))
            return (NativeTypeScriptKind.Tsgo, typeScriptDir);

        if (project.GoTs)
        {
            if (NativeTsCompiler.TryFindGlobalTsgoExecutable(out var tsgoExecutable))
                return (NativeTypeScriptKind.Tsgo, tsgoExecutable);
            throw new InvalidOperationException(
                "gots is enabled, but tsgo was not found in node_modules/@typescript/native-preview or on PATH");
        }

        return (NativeTypeScriptKind.Tsgo, null);
    }

    // tsrs (when enabled) and TypeScript 7.1+ tsc (tsVersion 7.1+, project local typescript 7.1+ or fallback when tsrs
    // is not available)
    (string? Tsrs, string? Tsc) ResolveNativeCompilers(ProjectOptions project)
    {
        var tsrs = project.Tsrs || project.ValidateTsrs ? ResolveTsrsExecutable(project) : null;
        var tsc = ResolveTscExecutable(project, (project.Tsrs || project.ValidateTsrs) && tsrs == null);
        return (tsrs, tsc);
    }

    string? _resolvedTscKey;
    string? _resolvedTscExecutable;
    bool _projectTypeScriptIsNative;

    string? ResolveTscExecutable(ProjectOptions project, bool tsrsFallback)
    {
        var tsProject = project.Owner;
        var explicitVersion = project.TypeScriptVersionOverride &&
                              TypeScriptNativeInstaller.IsNativeVersion(project.TypeScriptVersion)
            ? project.TypeScriptVersion
            : null;
        var key = tsProject.Owner.FullPath + "|" + explicitVersion + "|" + tsrsFallback;
        if (_resolvedTscKey == key)
            return _resolvedTscExecutable;
        _resolvedTscKey = key;
        _projectTypeScriptIsNative = false;
        if (!project.TypeScriptVersionOverride &&
            ((tsProject.DevDependencies?.Contains("typescript") ?? false) ||
             (tsProject.Dependencies?.Contains("typescript") ?? false)) &&
            TypeScriptNativeInstaller.TryFindInProject(tsProject.Owner.FullPath, _diskCache.FsAbstraction,
                out var projectTsc, out _))
        {
            _projectTypeScriptIsNative = true;
            return _resolvedTscExecutable = projectTsc;
        }

        var version = explicitVersion ?? (tsrsFallback ? TypeScriptNativeInstaller.DefaultVersion : null);
        if (version == null)
            return _resolvedTscExecutable = null;
        if (TypeScriptNativeInstaller.TryInstall(project.Tools.Path, version, _logger, out var tscExecutable,
                out var failureReason))
            return _resolvedTscExecutable = tscExecutable;
        if (explicitVersion != null)
            throw new InvalidOperationException("tsVersion " + explicitVersion + ": " + failureReason);
        _logger.Warn("TypeScript 7 (tsc) is not available either (" + failureReason + ")");
        return _resolvedTscExecutable = null;
    }

    string? _resolvedTsrsKey;
    string? _resolvedTsrsExecutable;

    // Finds tsrs, installs it into tools directory when missing, null means fallback to gots or regular TypeScript.
    // Result is remembered, so interactive rebuilds do not retry failed downloads.
    string? ResolveTsrsExecutable(ProjectOptions project)
    {
        var key = project.Owner.Owner.FullPath + "|" + project.TsrsVersion;
        if (_resolvedTsrsKey == key)
            return _resolvedTsrsExecutable;
        _resolvedTsrsKey = key;
        if (TsrsLocator.TryFind(project.Owner.Owner.FullPath, _diskCache.FsAbstraction, out var tsrsExecutable))
            return _resolvedTsrsExecutable = tsrsExecutable;
        if (TsrsInstaller.TryInstall(project.Tools.Path, project.TsrsVersion, _logger, out tsrsExecutable,
                out var failureReason))
            return _resolvedTsrsExecutable = tsrsExecutable;
        _logger.Warn("tsrs is not available (" + failureReason + "), falling back to TypeScript 7 (tsc " +
                     TypeScriptNativeInstaller.DefaultVersion + "), then Go TypeScript (tsgo), then regular TypeScript");
        return _resolvedTsrsExecutable = null;
    }

    internal static bool TryFindNativeTypeScriptDirectory(string projectDirectory, IFsAbstraction fsAbstraction,
        out string typeScriptDir)
    {
        var dir = PathUtils.Normalize(projectDirectory).TrimEnd('/');
        while (!string.IsNullOrEmpty(dir))
        {
            typeScriptDir = PathUtils.Join(dir, "node_modules/@typescript/native-preview");
            if (NativeTsCompiler.HasTsgoExecutable(typeScriptDir, fsAbstraction))
                return true;

            var parentSpan = PathUtils.Parent(dir);
            var parent = parentSpan.IsEmpty ? "" : parentSpan.ToString();
            if (parent == dir)
                break;
            dir = parent;
        }

        typeScriptDir = "";
        return false;
    }

    public void BuildSubProjects(ProjectOptions project, bool buildOnlyOnce, BuildResult buildResult,
        MainBuildResult mainBuildResult, int iterationId)
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
                    newSubProjects.GetOrAddValueRef(asset.Name);
                }
            }
        }

        foreach (var u in newSubProjects.Index)
        {
            var projectPath = newSubProjects.KeyRef(u);
            if (newSubProjects.ValueRef(u) == null)
            {
                if (project.SubProjects == null || !project.SubProjects.TryGetValue(projectPath, out var subProj) ||
                    subProj == null)
                {
                    TSProject? tsProject;
                    var (pref, name) = BuildModuleCtx.SplitProjectAssetName(projectPath);
                    if (pref.Length > 8)
                    {
                        var mainFile = _diskCache.TryGetItem(name);
                        if (mainFile == null)
                            continue;
                        tsProject = TSProject.Create(mainFile.Parent, _diskCache, _logger, null, true)!;
                        tsProject.MainFile = mainFile.FullPath;
                        // TSProject.Create always initializes ProjectOptions
                        var virtualProjectOptions = tsProject.ProjectOptions!;
                        virtualProjectOptions.Variant = pref.Substring(8, pref.Length - 9);
                        virtualProjectOptions.NoHtml = true;
                        virtualProjectOptions.TypeScriptVersion = project.TypeScriptVersion;
                        virtualProjectOptions.Tools = project.Tools;
                    }
                    else
                    {
                        var dirCache =
                            _diskCache.TryGetItem(PathUtils.Join(project.Owner.Owner.FullPath, name)) as
                                IDirectoryCache;
                        tsProject = TSProject.Create(dirCache, _diskCache, _logger, null);
                    }

                    if (tsProject == null)
                        continue;
                    tsProject.IsRootProject = true;
                    if (BuildCache == null)
                        if (tsProject.Virtual)
                        {
                            // TSProject.Create always initializes ProjectOptions
                            var virtualProjectOptions = tsProject.ProjectOptions!;
                            virtualProjectOptions.Tools = project.Tools;
                            virtualProjectOptions.ForbiddenDependencyUpdate = true;
                        }
                        else
                        {
                            tsProject.ProjectOptions = new ProjectOptions
                            {
                                Tools = project.Tools,
                                Owner = tsProject,
                                ForbiddenDependencyUpdate = project.ForbiddenDependencyUpdate
                            };
                        }

                    // TSProject.Create always initializes ProjectOptions
                    subProj = tsProject.ProjectOptions!;
                }

                newSubProjects.ValueRef(u) = subProj;
                subProj.UpdateFromProjectJson(false);
                subProj.RefreshCompilerOptions();
                subProj.RefreshMainFile();
                if (_subBuildCtxs == null || !_subBuildCtxs.TryGetValue(projectPath, out var subBuildCtx))
                {
                    subBuildCtx = new BuildCtx(CompilerPool, _diskCache, Verbose, _logger,
                        // BuildCache is a non-nullable readonly field, the defensive null check above only affects flow analysis
                        subProj.Owner.Owner.FullPath, BuildCache!, _typeCheck);
                }

                newSubBuildCtxs.GetOrAddValueRef(projectPath) = subBuildCtx;

                if (buildResult.SubBuildResults == null ||
                    !buildResult.SubBuildResults.TryGetValue(projectPath, out var subBuildResult))
                {
                    subBuildResult = new BuildResult(mainBuildResult, subProj);
                }

                newSubBuildResults.GetOrAddValueRef(projectPath) = subBuildResult;
                subBuildCtx.Build(subProj, buildOnlyOnce, subBuildResult, mainBuildResult, iterationId);
                buildResult.HasError |= subBuildResult.HasError;
                buildResult.TaskForSemanticCheck = Task.WhenAll(
                    buildResult.TaskForSemanticCheck ??
                    throw new InvalidOperationException("Semantic check task of project is missing"),
                    subBuildResult.TaskForSemanticCheck ??
                    throw new InvalidOperationException("Semantic check task of sub project is missing")).ContinueWith(
                    subresults => { return subresults.Result.Where(d => d != null).SelectMany(d => d).ToList(); });
            }
        }

        project.SubProjects = newSubProjects;
        _subBuildCtxs = newSubBuildCtxs;
        buildResult.SubBuildResults = newSubBuildResults;
    }
}
