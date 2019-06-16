using System;
using System.Collections.Generic;

namespace Lib.DiskCache
{
    public interface IFsAbstraction
    {
        bool IsMac { get; }
        bool IsUnixFs { get; }
        FsItemInfo GetItemInfo(ReadOnlySpan<char> path);
        IReadOnlyList<FsItemInfo> GetDirectoryContent(string path);
        byte[] ReadAllBytes(string path);
        string ReadAllUtf8(string path);
        bool FileExists(string path);
    }
}
