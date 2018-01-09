using Lib.CSSProcessor;
using Lib.TSCompiler;
using System.Collections.Generic;
using System.Threading;

namespace Lib.Composition
{
    public class BuildCtx
    {
        public BuildCtx(ICompilerPool compilerPool, bool verbose)
        {
            _cts = new CancellationTokenSource();
            _cancelationToken = _cts.Token;
            Verbose = verbose;
            CompilerPool = compilerPool;
        }

        public void Cancel()
        {
            _cts.Cancel(true);
        }

        public TSCompilerOptions TSCompilerOptions { get; set; }
        public HashSet<string> Sources { get; set; }

        public BuildResult BuildResult { get; set; }

        CancellationTokenSource _cts;
        public CancellationToken _cancelationToken;
        public bool Verbose;
        public ICompilerPool CompilerPool;
    }
}
