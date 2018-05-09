using System;
using System.Reactive;

namespace Lib.DiskCache
{
    public interface IDiskCache
    {
        IItemCache TryGetItem(string path);
        IDirectoryCache Root();
        Func<(IDirectoryCache parent, string name, bool isDir), bool> DefaultFilter { get; set; }
        IFsAbstraction FsAbstraction { get; }
        IObservable<Unit> ChangeObservable { get; }
        bool CheckForTrueChange();
        void ResetChange();
        void UpdateIfNeeded(IDirectoryCache dir);
    }
}
