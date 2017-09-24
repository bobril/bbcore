using Lib.CSSProcessor;
using System.Threading;

namespace Lib.Composition
{
    public class BuildCtx
    {
        public BuildCtx(ICompilerPool compilerPool)
        {
            _cts = new CancellationTokenSource();
            _cancelationToken = _cts.Token;
            CompilerPool = compilerPool;
        }

        public void Cancel()
        {
            _cts.Cancel(true);
        }

        CancellationTokenSource _cts;
        public CancellationToken _cancelationToken;
        public ICompilerPool CompilerPool;
    }
}
