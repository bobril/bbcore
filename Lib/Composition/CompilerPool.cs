using System.Collections.Concurrent;
using Lib.ToolsDir;
using System;
using System.Threading.Tasks;
using Lib.TSCompiler;
using Lib.CSSProcessor;
using Lib.Utils.Logger;

namespace Lib.Composition
{
    public class CompilerPool : ICompilerPool
    {
        public CompilerPool(IToolsDir toolsDir, ILogger logger, int parallelCompilations = 20)
        {
            _toolsDir = toolsDir;
            _logger = logger;
            _parallelCompilations = parallelCompilations;
            _semaphore = new System.Threading.SemaphoreSlim(parallelCompilations);
            _semaphoreCss = new System.Threading.SemaphoreSlim(parallelCompilations);
        }

        readonly System.Threading.SemaphoreSlim _semaphore;
        readonly System.Threading.SemaphoreSlim _semaphoreCss;
        readonly ConcurrentBag<ITSCompiler> _pool = new ConcurrentBag<ITSCompiler>();
        readonly ConcurrentBag<ICssProcessor> _poolCss = new ConcurrentBag<ICssProcessor>();

        readonly IToolsDir _toolsDir;
        readonly ILogger _logger;
        readonly int _parallelCompilations;

        public ITSCompiler GetTs(DiskCache.IDiskCache diskCache, ITSCompilerOptions compilerOptions)
        {
            _semaphore.Wait();
            if (!_pool.TryTake(out var res))
                res = new TsCompiler(_toolsDir, _logger);
            res.DiskCache = diskCache;
            if (compilerOptions != null) res.CompilerOptions = compilerOptions;
            return res;
        }

        public void ReleaseTs(ITSCompiler value)
        {
            _pool.Add(value);
            _semaphore.Release();
        }

        public ICssProcessor GetCss()
        {
            _semaphoreCss.Wait();
            if (_poolCss.TryTake(out var res))
                return res;
            return new CssProcessor(_toolsDir);
        }

        public void ReleaseCss(ICssProcessor value)
        {
            _poolCss.Add(value);
            _semaphoreCss.Release();
        }

        public async Task FreeMemory()
        {
            for (int i = 0; i < _parallelCompilations; i++)
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
                foreach (var processor in _poolCss)
                {
                    processor.Dispose();
                }
                _poolCss.Clear();
            }
            finally
            {
                for (int i = 0; i < _parallelCompilations; i++)
                {
                    _semaphore.Release();
                    _semaphoreCss.Release();
                }
            }
        }
    }
}
