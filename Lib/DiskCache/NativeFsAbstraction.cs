using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Lib.Utils;

namespace Lib.DiskCache;

public class FsAbstraction : IFsAbstraction
{
    public bool IsMac => false;
    public bool IsUnixFs => true;

    public class FakeFile
    {
        public byte[] _content;
        public ulong _length;
        public DateTime _lastWriteTimeUtc;
    }

    private readonly IDictionary<KeyValuePair<string, string>, FakeFile> _content =
        new ConcurrentDictionary<KeyValuePair<string, string>, FakeFile>();

    public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
    {
        var res = new List<FsItemInfo>();
        foreach (var kv in _content)
        {
            if (kv.Key.Key != path) continue;
            if (kv.Value == null)
                res.Add(FsItemInfo.Directory(kv.Key.Value, false));
            else
                res.Add(FsItemInfo.Existing(kv.Key.Value, kv.Value._length, kv.Value._lastWriteTimeUtc));
        }

        return res;
    }

    public FsItemInfo GetItemInfo(ReadOnlySpan<char> path)
    {
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file is null)
                return FsItemInfo.Directory(d, false);
            return FsItemInfo.Existing(d, file._length, file._lastWriteTimeUtc);
        }

        return FsItemInfo.Missing();
    }

    public byte[] ReadAllBytes(string path)
    {
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file == null)
                throw new Exception("Cannot read directory as file " + path);
            return file._content;
        }

        throw new Exception("Not found file " + path);
    }

    public void AddTextFile(string path, string content)
    {
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file == null)
                throw new Exception("Cannot add file because it is already dir " + path);
            file._lastWriteTimeUtc = DateTime.UtcNow;
            file._content = Encoding.UTF8.GetBytes(content);
            file._length = (ulong) Encoding.UTF8.GetByteCount(content);
            return;
        }

        CreateDir(d);
        _content[new KeyValuePair<string, string>(d, f)] = new FakeFile
        {
            _content = Encoding.UTF8.GetBytes(content),
            _length = (ulong) Encoding.UTF8.GetByteCount(content),
            _lastWriteTimeUtc = DateTime.UtcNow,
        };
    }
    
    public void AddTextFile(string path, byte[] content)
    {
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file == null)
                throw new Exception("Cannot add file because it is already dir " + path);
            file._lastWriteTimeUtc = DateTime.UtcNow;
            file._content = content;
            file._length = (ulong) content.Length;
            return;
        }

        CreateDir(d);
        _content[new KeyValuePair<string, string>(d, f)] = new FakeFile
        {
            _content = content,
            _length = (ulong) content.Length,
            _lastWriteTimeUtc = DateTime.UtcNow,
        };
    }

    private void CreateDir(string path)
    {
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file != null) throw new Exception("mkdir fail already file " + path);

            return;
        }

        if (d != "") CreateDir(d);

        _content[new KeyValuePair<string, string>(d, f)] = null;
    }

    public string ReadAllUtf8(string path)
    {
        throw new NotImplementedException();
    }

    public bool FileExists(string path)
    {
        throw new NotImplementedException();
    }

    public void WriteAllUtf8(string path, string content)
    {
        throw new NotImplementedException();
    }
}