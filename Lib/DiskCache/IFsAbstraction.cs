using System.Collections.Generic;

namespace Lib.DiskCache
{
    public interface IFsAbstraction
    {
        bool IsUnixFs { get; }
        FsItemInfo GetItemInfo(string path);
        IReadOnlyList<FsItemInfo> GetDirectoryContent(string path);
        byte[] ReadAllBytes(string path);
        string ReadAllUtf8(string path);
    }
}
