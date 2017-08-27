using System.Collections.Concurrent;
using Lib.ToolsDir;
using System;

namespace Lib.TSCompiler
{
    public class TSCompilerPool : ITSCompilerPool
    {
        public TSCompilerPool(IToolsDir toolsDir, int parallelCompilations = 1)
        {
            _toolsDir = toolsDir;
            _semaphore = new System.Threading.SemaphoreSlim(parallelCompilations);
        }

        System.Threading.SemaphoreSlim _semaphore;
        ConcurrentBag<ITSCompiler> pool = new ConcurrentBag<ITSCompiler>();
        
        readonly IToolsDir _toolsDir;

        public ITSCompiler Get()
        {
            _semaphore.Wait();
            if (pool.TryTake(out var res))
                return res;
            return new TSCompiler(_toolsDir);
        }

        public void Release(ITSCompiler value)
        {
            pool.Add(value);
            _semaphore.Release();
        }
    }
}
