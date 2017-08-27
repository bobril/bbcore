using System;
using System.Collections.Generic;

namespace Lib.DiskCache
{
    public interface IDirectoryCache: IItemCache, IEnumerable<IItemCache>
    {
        IItemCache TryGetChild(string name);
        bool IsFake { get; set; }
        Func<(IDirectoryCache parent,string name,bool isDir),bool> Filter { get; set; }
        // returns true if there is change 
        bool WriteVirtualFile(string name, string data);
        object AdditionalInfo { get; set; }
    }
}