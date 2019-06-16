using System;

namespace Lib.DiskCache
{
    public interface IFileCache: IItemCache
    {
        DateTime Modified { get; }
        ulong Length { get; }
        byte[] ByteContent { get; }
        byte[] HashOfContent { get; }
        string Utf8Content { get; }
        void FreeCache();
    }
}
