using System;
using System.Collections.Generic;

namespace Lib.DiskCache;

public interface IDiskCache
{
    IItemCache? TryGetItem(ReadOnlySpan<char> path);
    IDirectoryCache Root();
    Func<(IDirectoryCache parent, string name, bool isDir), bool> DefaultFilter { get; set; }
    IFsAbstraction FsAbstraction { get; }
    IObservable<string> ChangeObservable { get; }
    bool CheckForTrueChange();
    void ResetChange();
    void UpdateIfNeeded(IDirectoryCache dir);
    void WatchDirectChildren(IDirectoryCache dir, string? extension, bool includeFiles, bool includeDirectories);
    void WatchDirectChildrenExcept(IDirectoryCache dir, string? extension, bool includeFiles, bool includeDirectories,
        IReadOnlyList<string>? excludedNames);
    void WatchDirectChildNames(IDirectoryCache dir, IReadOnlyList<string>? fileNames, IReadOnlyList<string>? directoryNames);
    public string? IgnoreChangesInPath { get; set; }
    public IReadOnlyList<string>? IgnoreWatcherChangesInPaths { get; set; }
    bool UpdateFile(string path, string content);
}
