using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils.Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lib.Composition
{
    public class BuildCtx
    {
        public BuildCtx(ICompilerPool compilerPool, DiskCache.DiskCache diskCache, bool verbose, ILogger logger, string currentDirectory)
        {
            Verbose = verbose;
            CompilerPool = compilerPool;
            _diskCache = diskCache;
            Logger = logger;
            _currentDirectory = currentDirectory;
        }

        string _currentDirectory;
        string _mainFile;
        string _jasmineDts;
        List<string> _exampleSources;
        List<string> _testSources;
        ITSCompilerOptions _compilerOptions;
        ITSCompiler _typeChecker;

        public bool ProjectStructureChanged;
        public bool CompilerOptionsChanged;

        public string CurrentDirectory
        {
            get { return _currentDirectory; }
            set
            {
                _currentDirectory = value;
            }
        }

        public string MainFile
        {
            get { return _mainFile; }
            set
            {
                if (!ReferenceEquals(value, _mainFile))
                {
                    _mainFile = value; ProjectStructureChanged = true;
                }
            }
        }
        public string JasmineDts
        {
            get { return _jasmineDts; }
            set
            {
                if (!ReferenceEquals(value, _jasmineDts))
                {
                    _jasmineDts = value; ProjectStructureChanged = true;
                }
            }
        }

        public List<string> ExampleSources
        {
            get { return _exampleSources; }
            set
            {
                if (!ReferenceEquals(value, _exampleSources))
                {
                    _exampleSources = value; ProjectStructureChanged = true;
                }
            }
        }

        public List<string> TestSources
        {
            get { return _testSources; }
            set
            {
                if (!ReferenceEquals(value, _testSources))
                {
                    _testSources = value; ProjectStructureChanged = true;
                }
            }
        }

        public ITSCompilerOptions CompilerOptions
        {
            get { return _compilerOptions; }
            set
            {
                if (!ReferenceEquals(value, _compilerOptions))
                {
                    _compilerOptions = value; ProjectStructureChanged = true; CompilerOptionsChanged = true;
                }
            }
        }

        public bool Verbose;
        public ICompilerPool CompilerPool;
        readonly DiskCache.DiskCache _diskCache;
        public ILogger Logger;

        string _lastTsVersion = null;
        private Diagnostic[] _lastSemantics;

        internal void ShowTsVersion(string version)
        {
            if (_lastTsVersion != version)
            {
                Logger.Info("Using TypeScript version " + version);
                _lastTsVersion = version;
            }
        }

        enum TypeCheckChange
        {
            None = 0,
            Small = 1,
            Input = 2,
            Options = 3
        }

        TypeCheckChange _cancelledTypeCheckType;

        void DoTypeCheck(TypeCheckChange type)
        {
            if (_typeChecker == null)
            {
                type = TypeCheckChange.Options;
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
            }
            _lastSemantics = _typeChecker.GetDiagnostics();
        }

        TypeCheckChange DetectTypeCheckChange()
        {
            if (_typeChecker == null || CompilerOptionsChanged)
            {
                return TypeCheckChange.Options;
            }
            if (ProjectStructureChanged)
            {
                return TypeCheckChange.Input;
            }
            return TypeCheckChange.Small;
        }

        CancellationTokenSource _cancellation;
        Task _typeCheckTask = Task.CompletedTask;

        public Task<Diagnostic[]> StartTypeCheck()
        {
            _cancellation?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellation = cancellationTokenSource;
            var current = DetectTypeCheckChange();
            var res = _typeCheckTask.ContinueWith((_task, _state) => {
                current = (TypeCheckChange)Math.Max((int)_cancelledTypeCheckType, (int)current);
                _cancelledTypeCheckType = TypeCheckChange.None;
                DoTypeCheck(current);
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    _cancelledTypeCheckType = current;
                    return null;
                }
                return _lastSemantics;
            }, null, _cancellation.Token, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.LongRunning, TaskScheduler.Default);
            _typeCheckTask = res;
            return res;
        }

        string[] MakeSourceListArray()
        {
            var res = new List<string>();
            if (MainFile != null) res.Add(MainFile);
            if (ExampleSources != null) res.AddRange(ExampleSources);
            if (TestSources != null) res.AddRange(TestSources);
            if (JasmineDts != null) res.Add(JasmineDts);
            return res.ToArray();
        }
    }
}
