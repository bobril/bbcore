using System;

namespace Lib.DiskCache
{
    public struct FsItemInfo
    {
        public bool Exists { get { return Name != null; } }
        public bool IsDirectory { get { return Length == ulong.MaxValue; } }
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

        public static FsItemInfo Directory(string name)
        {
            return new FsItemInfo { Name = name, Length = ulong.MaxValue };
        }
    }
}
