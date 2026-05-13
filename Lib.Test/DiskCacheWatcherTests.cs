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
    public void WatcherReportsNewFilesAfterNegativeCheckInParentDirectory()
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
        Assert.Null(dc.TryGetItem("/project/new.ts"));

        TriggerWatchers(watchers, "/project/new.ts");

        Assert.Equal(new[] { "/project/new.ts" }, changes);
    }

    [Fact]
    public void WatcherIgnoresExistingFilesThatWereResolvedButNotRead()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        fs.WriteAllUtf8("/project/unused.ts", "export const value = 1;");
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
        Assert.NotNull(dc.TryGetItem("/project/unused.ts"));

        TriggerWatchers(watchers, "/project/unused.ts");
        Assert.Empty(changes);
    }

    [Fact]
    public void WatcherNegativeCheckOnlyReportsDirectChildren()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        fs.WriteAllUtf8("/project/src/existing.ts", "export const value = 1;");
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
        Assert.Null(dc.TryGetItem("/project/src/missing.ts"));

        TriggerWatchers(watchers, "/project/src/deep/new.ts");
        Assert.Empty(changes);

        TriggerWatchers(watchers, "/project/src/new.ts");
        Assert.Equal(new[] { "/project/src/new.ts" }, changes);
    }

    [Fact]
    public void WatcherReportsFilteredDirectoryContentChildren()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        fs.WriteAllUtf8("/project/docs/first.mdxb", "# First");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        var docs = (IDirectoryCache)dc.TryGetItem("/project/docs")!;
        dc.WatchDirectChildren(docs, "mdxb", true, false);

        TriggerWatchers(watchers, "/project/docs/ignored.txt");
        Assert.Empty(changes);

        TriggerWatchers(watchers, "/project/docs/nested/new.mdxb");
        Assert.Empty(changes);

        TriggerWatchers(watchers, "/project/docs/second.mdxb");
        Assert.Equal(new[] { "/project/docs/second.mdxb" }, changes);
    }

    [Fact]
    public void WatcherFilteredDirectoryContentCanReportNewDirectChildDirectories()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        fs.WriteAllUtf8("/project/docs/first.mdxb", "# First");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        var docs = (IDirectoryCache)dc.TryGetItem("/project/docs")!;
        dc.WatchDirectChildren(docs, "mdxb", true, true);
        fs.WriteAllUtf8("/project/docs/nested/first.mdxb", "# Nested");
        changes.Clear();

        TriggerWatchers(watchers, "/project/docs/nested");

        Assert.Equal(new[] { "/project/docs/nested" }, changes);
    }

    [Fact]
    public void WatcherCanReportAnyDirectChildFileFromDirectoryContentScan()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("/project/package.json", "{}");
        fs.WriteAllUtf8("/project/src/existing.ts", "export const value = 1;");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        var src = (IDirectoryCache)dc.TryGetItem("/project/src")!;
        dc.WatchDirectChildren(src, null, true, true);

        TriggerWatchers(watchers, "/project/src/nested/new.spec.ts");
        Assert.Empty(changes);

        TriggerWatchers(watchers, "/project/src/new.spec.ts");
        Assert.Equal(new[] { "/project/src/new.spec.ts" }, changes);
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
