using System;

namespace Lib.DiskCache;

public struct FsItemInfo
{
    public bool Exists { get { return Name != null; } }
    public bool IsDirectory { get { return Length >= ulong.MaxValue-1; } }
    public bool IsLink { get { return Length != ulong.MaxValue; } }
    public string Name;
    public ulong Length;
    public DateTime LastWriteTimeUtc;

    public static FsItemInfo Missing()
    {
        return new FsItemInfo();
    }

    public static FsItemInfo Existing(string name, ulong length, DateTime lastWriteTimeUtc)
    {
        return new FsItemInfo { Name = name, Length = length, LastWriteTimeUtc = lastWriteTimeUtc };
    }

    public static FsItemInfo Directory(string name, bool isLink)
    {
        return new FsItemInfo { Name = name, Length = ulong.MaxValue-(isLink?1ul:0) };
    }
}