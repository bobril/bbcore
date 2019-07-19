using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils.Logger;
using System;
using System.Collections.Generic;

namespace Lib.Composition
{
    public class BuildCtx
    {
        public BuildCtx(ICompilerPool compilerPool, DiskCache.DiskCache diskCache, bool verbose, ILogger logger)
        {
            Verbose = verbose;
            CompilerPool = compilerPool;
            _diskCache = diskCache;
            Logger = logger;
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

        public void StartTypeCheck(ProjectOptions options)
        {
            if (_typeChecker != null && CompilerOptionsChanged)
            {
                CompilerPool.ReleaseTs(_typeChecker);
                _typeChecker = null;
            }
            if (_typeChecker == null)
            {
                _typeChecker = CompilerPool.GetTs(_diskCache, CompilerOptions);
                _typeChecker.ClearDiagnostics();
                _typeChecker.CreateProgram(_currentDirectory, MakeSourceListArray());
            }
            else if (ProjectStructureChanged)
            {
                _typeChecker.ClearDiagnostics();
                _typeChecker.UpdateProgram(MakeSourceListArray());
                _typeChecker.TriggerUpdate();
            }
            else
            {
                _typeChecker.ClearDiagnostics();
                _typeChecker.TriggerUpdate();
            }
            _lastSemantics = _typeChecker.GetDiagnostics();
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
