using Lib.TSCompiler;
using Lib.Utils.Logger;
using System.Collections.Generic;

namespace Lib.Composition
{
    public class BuildCtx
    {
        public BuildCtx(ICompilerPool compilerPool, bool verbose, ILogger logger)
        {
            Verbose = verbose;
            CompilerPool = compilerPool;
            Logger = logger;
        }

        string _mainFile;
        string _jasmineDts;
        List<string> _exampleSources;
        List<string> _testSources;
        ITSCompilerOptions _compilerOptions;

        public bool ProjectStructureChanged;

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
                    _compilerOptions = value; ProjectStructureChanged = true;
                }
            }
        }

        public bool Verbose;
        public ICompilerPool CompilerPool;
        public ILogger Logger;

        string _lastTsVersion = null;

        internal void ShowTsVersion(string version)
        {
            if (_lastTsVersion != version)
            {
                Logger.Info("Using TypeScript version " + version);
                _lastTsVersion = version;
            }
        }
    }
}
