using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTDB.Collections;
using Lib.BuildCache;
using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;
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
    private BuildCtx(DiskCache.DiskCache diskCache, bool verbose,
        string currentDirectory, IBuildCache buildCache, RunTypeCheck typeCheck)
    {
        Verbose = verbose;
        _diskCache = diskCache;
        _currentDirectory = currentDirectory;
        _typeCheck = typeCheck;
        BuildCache = buildCache;
    }

    public BuildCtx(DiskCache.DiskCache diskCache, bool verbose,
        string currentDirectory, IBuildCache buildCache, string typeCheckValue) : this(diskCache, verbose,
        currentDirectory, buildCache, typeCheckValue switch
        {
            "yes" => RunTypeCheck.Yes, "no" => RunTypeCheck.No, "only" => RunTypeCheck.Only,
            _ => throw new ArgumentException("typeCheck parameter is not valid: " + typeCheckValue)
        })
    {
    }

    RefDictionary<string, BuildCtx?>? _subBuildCtxs;

    readonly RunTypeCheck _typeCheck;
    string _mainFile;
    string _jasmineDts;
    List<string> _exampleSources;
    List<string> _testSources;
    string[] _additionalSources;
    ITSCompilerOptions _compilerOptions;
    ITSCompiler _typeChecker;
    public readonly IBuildCache BuildCache;

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
                    _typeChecker = null;
                }

                // _typeChecker = CompilerPool.GetTs(_diskCache, CompilerOptions);
                _typeChecker.ClearDiagnostics();
                _typeChecker.CreateProgram(_currentDirectory, MakeSourceListArray());
                break;
            case TypeCheckChange.Once:
                if (_typeChecker != null)
                {
                    // CompilerPool.ReleaseTs(_typeChecker);
                    _typeChecker = null;
                }

                // _typeChecker = CompilerPool.GetTs(_diskCache, CompilerOptions);
                _typeChecker.ClearDiagnostics();
                _typeChecker.CheckProgram(_currentDirectory, MakeSourceListArray());
                _lastSemantics = _typeChecker.GetDiagnostics().ToList();
                // CompilerPool.ReleaseTs(_typeChecker);
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
        if (_typeCheck == RunTypeCheck.No)
            return Task.FromResult(new List<Diagnostic>());
        var current = DetectTypeCheckChange();
        var res = _typeCheckTask.ContinueWith((_task, _state) =>
        {
            current = (TypeCheckChange) Math.Max((int) _cancelledTypeCheckType, (int) current);
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

    public void Build(
        ProjectOptions project,
        bool buildOnlyOnce,
        BuildResult buildResult,
        MainBuildResult mainBuildResult,
        int iterationId,
        ToolsDir.ToolsDir toolsDir)
    {
        if (project.PreserveProjectRoot)
            mainBuildResult.PreserveProjectRoot = true;
        _buildOnceOnly = buildOnlyOnce;
        _compilerOptionsChanged = false;
        _projectStructureChanged = false;
        MainFile = project.MainFile;
        AdditionalSources = project.IncludeSources; // remove
        ExampleSources = project.ExampleSources; // remove
        TestSources = project.TestSources; // remove
        JasmineDts = project.TestSources != null ? project.JasmineDts : null; // remove
        CompilerOptions = project.FinalCompilerOptions!;

        var tsProject = project.Owner;
        var tryDetectChanges = !_projectStructureChanged;
        buildResult.HasError = false;
        if (!buildResult.Incremental || !tryDetectChanges)
        {
            buildResult.RecompiledIncrementally.Clear();
        }
        var compiler = new TsCompiler(toolsDir);
        compiler.CompilerOptions = new TSCompilerOptions
        {
            allowJs = true,
            declaration = true,
            module = ModuleKind.Commonjs,
            target = ScriptTarget.Es2019,
            strict = true,
            outDir = "dist",
            moduleResolution = ModuleResolutionKind.Node,
        };
        var buildModuleCtx = new BuildModuleCtx
        {
            BuildCtx = this,
            Owner = tsProject,
            Result = buildResult,
            MainResult = mainBuildResult,
            ToCheck = new(),
            IterationId = iterationId,
            TsCompiler = compiler,
        };
        try
        {
            BuildCache.StartTransaction();
            _typeChecker = compiler;

            mainBuildResult.MergeCommonSourceDirectory(tsProject.Owner.FullPath);
            buildResult.TaskForSemanticCheck = StartTypeCheck();
            if (_typeCheck == RunTypeCheck.Only)
                return;
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
}