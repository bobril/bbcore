using System.Collections.Concurrent;
using Lib.ToolsDir;
using System.Threading.Tasks;
using Lib.TSCompiler;
using Lib.Utils.Logger;

namespace Lib.Composition;

public class CompilerPool : ICompilerPool
{
    public CompilerPool(IToolsDir toolsDir, ILogger logger, int parallelCompilations = 20)
    {
        _toolsDir = toolsDir;
        _logger = logger;
        _parallelCompilations = parallelCompilations;
        _semaphore = new(parallelCompilations);
        _semaphoreCss = new(parallelCompilations);
        _semaphoreScss = new(parallelCompilations);
    }

    readonly System.Threading.SemaphoreSlim _semaphore;
    readonly System.Threading.SemaphoreSlim _semaphoreCss;
    readonly System.Threading.SemaphoreSlim _semaphoreScss;
    readonly ConcurrentBag<ITSCompiler> _pool = new();

    readonly IToolsDir _toolsDir;
    readonly ILogger _logger;
    readonly int _parallelCompilations;

    public ITSCompiler GetTs(DiskCache.IDiskCache diskCache, ITSCompilerOptions? compilerOptions)
    {
        _semaphore.Wait();
        if (!_pool.TryTake(out var res))
            res = new TsCompiler(_toolsDir);
        res.DiskCache = diskCache;
        if (compilerOptions != null) res.CompilerOptions = compilerOptions;
        return res;
    }

    public void ReleaseTs(ITSCompiler value)
    {
        _pool.Add(value);
        _semaphore.Release();
    }

    public async Task FreeMemory()
    {
        for (var i = 0; i < _parallelCompilations; i++)
        {
            await _semaphore.WaitAsync();
            await _semaphoreCss.WaitAsync();
        }
        try
        {
            foreach (var compiler in _pool)
            {
                compiler.Dispose();
            }
            _pool.Clear();
        }
        finally
        {
            for (var i = 0; i < _parallelCompilations; i++)
            {
                _semaphore.Release();
                _semaphoreCss.Release();
            }
        }
    }
}
