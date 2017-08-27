using System;

namespace Lib.DiskCache
{
    public interface IFileCache: IItemCache
    {
        DateTime Modified { get; }
        long Length { get; }
        byte[] ByteContent { get; }
        string Utf8Content { get; }
        object AdditionalInfo { get; set; }
    }
}