using System;
using System.Collections.Generic;

namespace Lib.DiskCache;

public interface IFsAbstraction
{
    bool IsMac { get; }
    bool IsUnixFs { get; }
    FsItemInfo GetItemInfo(ReadOnlySpan<char> path);
    IReadOnlyList<FsItemInfo> GetDirectoryContent(string path);
    byte[] ReadAllBytes(string path);
    void AddTextFile(string path, string content);
    void AddTextFile(string path, byte[] content);
}