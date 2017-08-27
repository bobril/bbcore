using System.Threading;

namespace Lib.TSCompiler
{
    public class BuildCtx
    {
        public BuildCtx(ITSCompilerPool compilerPool)
        {
            _cts = new CancellationTokenSource();
            _cancelationToken = _cts.Token;
            _compilerPool = compilerPool;
        }

        public void Cancel()
        {
            _cts.Cancel(true);
        }

        CancellationTokenSource _cts;
        public CancellationToken _cancelationToken;
        public ITSCompilerPool _compilerPool;
    }
}
