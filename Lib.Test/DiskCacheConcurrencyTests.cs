using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Lib.DiskCache;
using Xunit;

namespace Lib.Test;

public class DiskCacheConcurrencyTests
{
    [Fact]
    public async Task DirectDirectoryReadsAreSafeDuringConcurrentRefresh()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/src/a.ts", "export const a = 1;");
        var diskCache = new DiskCache.DiskCache(fs, () => fs);
        var dir = Assert.IsAssignableFrom<IDirectoryCache>(diskCache.TryGetItem("/src"));

        diskCache.UpdateIfNeeded(dir);

        var errors = new ConcurrentQueue<Exception>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var reader = Task.Run(async () =>
        {
            await start.Task;
            try
            {
                for (var i = 0; i < 5000; i++)
                {
                    _ = dir.TryGetChild("A.TS");
                    foreach (var item in dir)
                    {
                        _ = item.Name;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        var writer = Task.Run(async () =>
        {
            await start.Task;
            try
            {
                for (var i = 0; i < 1000; i++)
                {
                    var path = i % 2 == 0 ? "/src/b.ts" : "/src/c.ts";
                    fs.WriteAllUtf8(path, "export const v = " + i + ";");
                    diskCache.UpdateIfNeeded(dir);
                    fs.Delete(path);
                    diskCache.UpdateIfNeeded(dir);
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        start.SetResult();
        await Task.WhenAll(reader, writer);

        Assert.Empty(errors);
    }
}
