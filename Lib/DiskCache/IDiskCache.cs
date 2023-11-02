using System;

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
    public string? IgnoreChangesInPath { get; set; }
    bool UpdateFile(string path, string content);
}