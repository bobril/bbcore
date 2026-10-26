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
        var fs = new LinkFs(new InMemoryFs());
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
    public void WatcherNegativeCheckOnlyReportsExactPath()
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
        Assert.Empty(changes);

        TriggerWatchers(watchers, "/project/src/missing.ts");
        Assert.Equal(new[] { "/project/src/missing.ts" }, changes);
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

    [Fact]
    public void WatcherDirectoryContentScanCanExcludeChildNames()
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
        dc.WatchDirectChildrenExcept(project, null, true, true, new[] { "bin" });

        fs.WriteAllUtf8("/project/bin/generated.js", "export const value = 1;");
        TriggerWatchers(watchers, "/project/bin");
        Assert.Empty(changes);

        fs.WriteAllUtf8("/project/spec/new.spec.ts", "describe('x', () => {});");
        TriggerWatchers(watchers, "/project/spec");
        Assert.Equal(new[] { "/project/spec" }, changes);
    }

    [Fact]
    public void WatcherNamedDirectoryCheckOnlyReportsThatDirectory()
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
        dc.WatchDirectChildNames(project, null, new[] { "assets" });

        fs.WriteAllUtf8("/project/bin/generated.js", "export const value = 1;");
        TriggerWatchers(watchers, "/project/bin");
        Assert.Empty(changes);

        fs.WriteAllUtf8("/project/assets/image.png", "");
        TriggerWatchers(watchers, "/project/assets");
        Assert.Equal(new[] { "/project/assets" }, changes);
    }

    [Fact]
    public void PackagesInNodeModulesAreWatchedByWatcherOfProjectContainingNodeModules()
    {
        var fs = new LinkFs(new InMemoryFs());
        fs.WriteAllUtf8("/repo/package.json", "{}");
        fs.WriteAllUtf8("/repo/app/package.json", "{}");
        fs.WriteAllUtf8("/repo/node_modules/a/index.js", "export const a = 1;");
        fs.WriteAllUtf8("/repo/node_modules/@scope/b/index.js", "export const b = 1;");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        dc.UpdateIfNeeded((IDirectoryCache)dc.TryGetItem("/repo/app")!);
        _ = ((IFileCache)dc.TryGetItem("/repo/node_modules/a/index.js")!).Utf8Content;
        _ = ((IFileCache)dc.TryGetItem("/repo/node_modules/@scope/b/index.js")!).Utf8Content;

        Assert.Equal(new[] { "/repo/app", "/repo" }, watchers.Select(w => w.WatchedDirectory));

        fs.WriteAllUtf8("/repo/node_modules/@scope/b/index.js", "export const b = 2;");
        TriggerWatchers(watchers, "/repo/node_modules/@scope/b/index.js");
        Assert.Equal(new[] { "/repo/node_modules/@scope/b/index.js" }, changes);
    }

    [Fact]
    public void PackagesInNodeModulesOutsideOfProjectAreWatchedSeparately()
    {
        var fs = new LinkFs(new InMemoryFs());
        fs.WriteAllUtf8("/home/app/package.json", "{}");
        fs.WriteAllUtf8("/home/node_modules/a/index.js", "export const a = 1;");
        fs.WriteAllUtf8("/home/node_modules/b/index.js", "export const b = 1;");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });

        dc.UpdateIfNeeded((IDirectoryCache)dc.TryGetItem("/home/app")!);
        _ = ((IFileCache)dc.TryGetItem("/home/node_modules/a/index.js")!).Utf8Content;
        _ = ((IFileCache)dc.TryGetItem("/home/node_modules/b/index.js")!).Utf8Content;

        Assert.Equal(new[] { "/home/app", "/home/node_modules/a", "/home/node_modules/b" },
            watchers.Select(w => w.WatchedDirectory));
    }

    [Fact]
    public void LinkedPackageIsWatchedByWatcherOfItsRealPath()
    {
        var fs = new LinkFs(new InMemoryFs(), "/repo/node_modules/linked", "/repo/packages/linked");
        fs.WriteAllUtf8("/repo/package.json", "{}");
        fs.WriteAllUtf8("/repo/app/package.json", "{}");
        fs.WriteAllUtf8("/repo/packages/linked/index.ts", "export const a = 1;");
        var watchers = new List<TestWatcher>();
        var dc = new DiskCache.DiskCache(fs, () =>
        {
            var watcher = new TestWatcher();
            watchers.Add(watcher);
            return watcher;
        });
        var changes = new List<string>();
        dc.ChangeObservable.Subscribe(change => changes.Add(change));

        dc.UpdateIfNeeded((IDirectoryCache)dc.TryGetItem("/repo/app")!);
        _ = ((IFileCache)dc.TryGetItem("/repo/node_modules/linked/index.ts")!).Utf8Content;

        Assert.Equal(new[] { "/repo/app", "/repo" }, watchers.Select(w => w.WatchedDirectory));

        fs.WriteAllUtf8("/repo/packages/linked/index.ts", "export const a = 2;");
        TriggerWatchers(watchers, "/repo/packages/linked/index.ts");
        Assert.Equal(new[] { "/repo/node_modules/linked/index.ts" }, changes);
    }

    static void TriggerWatchers(IEnumerable<TestWatcher> watchers, string path)
    {
        foreach (var watcher in watchers)
        {
            if (path != watcher.WatchedDirectory && !path.StartsWith(watcher.WatchedDirectory + "/"))
                continue;
            watcher.Trigger(path);
        }
    }

    // Accepts directory paths with trailing slash like real file system, directory at linkPath is symlink to realPath
    class LinkFs(InMemoryFs inner, string linkPath = "/nonexistent/link", string realPath = "/nonexistent/link")
        : IFsAbstraction
    {
        string ToReal(string path) => path == linkPath || path.StartsWith(linkPath + "/", StringComparison.Ordinal)
            ? realPath + path[linkPath.Length..]
            : path;

        public bool IsMac => inner.IsMac;
        public bool IsUnixFs => inner.IsUnixFs;

        public FsItemInfo GetItemInfo(ReadOnlySpan<char> path)
        {
            var p = path.ToString().TrimEnd('/');
            if (p == linkPath)
                return FsItemInfo.Directory(p[(p.LastIndexOf('/') + 1)..], true);
            if (p == linkPath[..linkPath.LastIndexOf('/')])
                return FsItemInfo.Directory(p[(p.LastIndexOf('/') + 1)..], false);
            return inner.GetItemInfo(ToReal(p));
        }

        public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
        {
            var res = inner.GetDirectoryContent(ToReal(path)).ToList();
            if (path == linkPath[..linkPath.LastIndexOf('/')])
                res.Add(FsItemInfo.Directory(linkPath[(linkPath.LastIndexOf('/') + 1)..], true));
            return res;
        }

        public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(ToReal(path));
        public string ReadAllUtf8(string path) => inner.ReadAllUtf8(ToReal(path));
        public bool FileExists(string path) => inner.FileExists(ToReal(path));
        public bool DirectoryExists(string path) => GetItemInfo(path).IsDirectory;
        public void WriteAllUtf8(string path, string content) => inner.WriteAllUtf8(ToReal(path), content);
        public void Delete(string path) => inner.Delete(ToReal(path));
        public void WriteAllBytes(string path, byte[] bytes) => inner.WriteAllBytes(ToReal(path), bytes);
        public string RealPath(string path) => ToReal(path);
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
