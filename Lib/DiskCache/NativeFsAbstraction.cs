using Lib.Utils;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lib.DiskCache
{
    public class NativeFsAbstraction : IFsAbstraction
    {
        public bool IsUnixFs => PathUtils.IsUnixFs;

        public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
        {
            var res = new List<FsItemInfo>();
            foreach (var fi in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                if ((fi.Attributes & FileAttributes.Directory) != 0)
                {
                    res.Add(FsItemInfo.Directory(fi.Name));
                }
                else
                {
                    res.Add(FsItemInfo.Existing(fi.Name, (ulong)((FileInfo)fi).Length, ((FileInfo)fi).LastWriteTimeUtc));
                }
            }
            return res;
        }

        public FsItemInfo GetItemInfo(string path)
        {
            var fi = new FileInfo(path);
            if (fi.Exists)
            {
                return FsItemInfo.Existing(fi.Name, (ulong)fi.Length, fi.LastWriteTimeUtc);
            }
            var di = new DirectoryInfo(path);
            if (di.Exists)
            {
                return FsItemInfo.Directory(di.Name);
            }
            return FsItemInfo.Missing();
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public string ReadAllUtf8(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
    }
}
