using Lib.Utils;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Lib.DiskCache
{
    public class NativeFsAbstraction : IFsAbstraction
    {
        public bool IsUnixFs => PathUtils.IsUnixFs;

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
        {
            var res = new List<FsItemInfo>();
            var di = new DirectoryInfo(path);
            if (!di.Exists)
                return res;
            foreach (var fi in di.EnumerateFileSystemInfos())
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
            int retry = 0;
            while (true)
            {
                try
                {
                    return File.ReadAllBytes(path);
                }
                catch (System.Exception)
                {
                    retry++;
                    if (retry > 5)
                        throw;
                }
                Thread.Sleep(50 * retry);
            }
        }

        public string ReadAllUtf8(string path)
        {
            int retry = 0;
            while (true)
            {
                try
                {
                    return File.ReadAllText(path, Encoding.UTF8);
                }
                catch (System.Exception)
                {
                    retry++;
                    if (retry > 5)
                        throw;
                }
                Thread.Sleep(50 * retry);
            }
        }
    }
}
