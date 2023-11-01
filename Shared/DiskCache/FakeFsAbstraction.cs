using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Shared.Utils;
using Shared.Watcher;

namespace Shared.DiskCache;

public class FakeFsAbstraction : IFsAbstraction, IDirectoryWatcher
{
    public bool IsMac => false;

    public bool IsUnixFs => true;

    public string WatchedDirectory { get; set; }
    public Action<string> OnFileChange { get; set; }
    public Action OnError { get; set; }

    public class FakeFile
    {
        public string _content;
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
        GetDirectoryAndFileNames(path.ToString(), out var d, out var f);
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file)) 
            return FsItemInfo.Missing();
        return file == null ? 
            FsItemInfo.Directory(d, false) :
            FsItemInfo.Existing(d, file._length, file._lastWriteTimeUtc);
    }

    public bool DirectoryExists(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file)) return false;
        return file == null;
    }

    public byte[] ReadAllBytes(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file == null)
                throw new Exception("Cannot read directory as file " + path);
            return Encoding.UTF8.GetBytes(file._content);
        }

        throw new Exception("Not found file " + path);
    }

    public string ReadAllUtf8(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            throw new Exception("Not found file " + path);
        if (file == null)
            throw new Exception("Cannot read directory as file " + path);
        return file._content;

    }

    public void Dispose()
    {
    }

    public void AddTextFile(string path, string content)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (_content.TryGetValue(new(d, f), out var file))
        {
            if (file == null)
                throw new Exception("Cannot add file because it is already dir " + path);
            file._lastWriteTimeUtc = DateTime.UtcNow;
            file._content = content;
            file._length = (ulong) Encoding.UTF8.GetByteCount(content);
            return;
        }

        CreateDir(d);
        _content[new KeyValuePair<string, string>(d, f)] = new FakeFile
        {
            _content = content,
            _length = (ulong) Encoding.UTF8.GetByteCount(content),
            _lastWriteTimeUtc = DateTime.UtcNow
        };
        OnFileChange?.Invoke(path);
    }

    void CreateDir(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file != null)
            {
                throw new Exception("mkdir fail already file " + path);
            }

            return;
        }

        if (d != "")
        {
            CreateDir(d);
        }

        _content[new KeyValuePair<string, string>(d, f)] = null;
    }

    public bool FileExists(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file)) return false;
        return file != null;
    }

    public void WriteAllUtf8(string path, string content)
    {
        AddTextFile(path,content);
    }

    public void Delete(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file)) return;
        if (file is null) return;
        _content.Remove(new KeyValuePair<string, string>(d, f));
        OnFileChange?.Invoke(path);
    }

    public void WriteAllBytes(string path, byte[] bytes)
    {
        WriteAllUtf8(path, Encoding.UTF8.GetString(bytes));
    }
    
    static void GetDirectoryAndFileNames(string path, out string dir, out string file)
    {
        if (path.StartsWith('/')) path = path[1..];
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        dir = d;
        file = f;
    }
}