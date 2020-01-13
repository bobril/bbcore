using System;
using System.Collections.Generic;

namespace Lib.DiskCache
{
    public interface IDirectoryCache: IItemCache, IEnumerable<IItemCache>
    {
        IItemCache TryGetChild(ReadOnlySpan<char> name);
        bool IsFake { get; set; }
        bool IsWatcherRoot { get; set; }
        bool IsLink { get; set; }
        object? Project { get; set; }
        Func<(IDirectoryCache parent,string name,bool isDir),bool> Filter { get; set; }
    }
}
