using System;
using System.Collections.Generic;
using System.Linq;
using Lib.DiskCache;
using Lib.Watcher;
using Xunit;

namespace Lib.Test;

public class DiskCacheWatcherTests
{
    [Fact]
    public void WatcherIgnoresSiblingKnownOnlyFromDirectoryListing()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        fs.WriteAllUtf8("/external/imported.ts", "export const value = 1;");
        fs.WriteAllUtf8("/external/index.html", "<!doctype html>");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        Assert.NotNull(dc.TryGetItem("/project"));
        dc.UpdateIfNeeded((IDirectoryCache)dc.TryGetItem("/project")!);
        Assert.NotNull(dc.TryGetItem("/external/imported.ts"));
        _ = ((IFileCache)dc.TryGetItem("/external/imported.ts")!).Utf8Content;

        TriggerWatchers(watchers, "/external/index.html");
        Assert.Empty(changes);

        TriggerWatchers(watchers, "/external/imported.ts");
        Assert.Equal(new[] { "/external/imported.ts" }, changes);
    }

    [Fact]
    public void WatcherKeepsReportingNewFilesInExplicitlyWatchedDirectories()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        var project = (IDirectoryCache)dc.TryGetItem("/project")!;
        dc.UpdateIfNeeded(project);

        TriggerWatchers(watchers, "/project/new.ts");

        Assert.Equal(new[] { "/project/new.ts" }, changes);
    }

    static void TriggerWatchers(IEnumerable<TestWatcher> watchers, string path)
    {
        foreach (var watcher in watchers)
        {
            watcher.Trigger(path);
        }
    }

    class TestWatcher : IDirectoryWatcher
    {
        public string WatchedDirectory { get; set; } = "";
        public Action<string> OnFileChange { get; set; } = _ => { };
        public Action OnError { get; set; } = () => { };

        public void Trigger(string path)
        {
            OnFileChange(path);
        }

        public void Dispose()
        {
        }
    }
}
