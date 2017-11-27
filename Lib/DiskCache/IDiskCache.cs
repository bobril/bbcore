using System;
using System.Reactive;

namespace Lib.DiskCache
{
    public interface IDiskCache
    {
        IDisposable AddRoot(string path);
        void UpdateIfNeeded(IItemCache item);
        IItemCache TryGetItem(string path);
        IDirectoryCache Root();
        Func<(IDirectoryCache parent, string name, bool isDir), bool> DefaultFilter { get; set; }
        IObservable<Unit> ChangeObservable { get; }
        IFsAbstraction FsAbstraction { get; }
    }
}