using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Shared.Utils;
using Shared.Watcher;

namespace Shared.DiskCache;

public class InMemoryFs : IFsAbstraction, IDirectoryWatcher
{
    private readonly IDictionary<KeyValuePair<string, string>, FakeFile> _content =
        new ConcurrentDictionary<KeyValuePair<string, string>, FakeFile>();

    public string WatchedDirectory { get; set; }
    public Action<string> OnFileChange { get; set; }
    public Action OnError { get; set; }

    public void Dispose()
    {
    }

    public bool IsMac => false;
    public bool IsUnixFs => true;

    public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
    {
        var res = new List<FsItemInfo>();
        foreach (var kv in _content)
        {
            if (kv.Key.Key != path) continue;
            res.Add(kv.Value is null
                ? FsItemInfo.Directory(kv.Key.Value, false)
                : FsItemInfo.Existing(kv.Key.Value, kv.Value._length, kv.Value._lastWriteTimeUtc));
        }

        return res;
    }

    public FsItemInfo GetItemInfo(ReadOnlySpan<char> path)
    {
        GetDirectoryAndFileNames(path.ToString(), out var d, out var f);
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            return FsItemInfo.Missing();
        return file == null
            ? FsItemInfo.Directory(d, false)
            : FsItemInfo.Existing(d, file._length, file._lastWriteTimeUtc);
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
        
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            throw new Exception("Not found file " + path);
        
        if (file == null)
            throw new Exception("Cannot read directory as file " + path);

        if (file._content is string)
            return Encoding.UTF8.GetBytes(file._content.ToString());

        return file._content as byte[];

    }

    public string ReadAllUtf8(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
            throw new Exception("Not found file " + path);
        
        if (file == null)
            throw new Exception("Cannot read directory as file " + path);
        
        return file._content.ToString();
    }

    public bool FileExists(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        
        if (!_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file)) 
            return false;
        
        return file != null;
    }

    public void WriteAllUtf8(string path, string content) => AddFile(path, content);
    
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
        AddFile(path, bytes);
    }

    private void AddFile(string path, object content)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        var contentLength = GetContentLength(content);
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file == null)
                throw new Exception("Cannot add file because it is already dir " + path);

            file._lastWriteTimeUtc = DateTime.UtcNow;
            file._content = content;
            file._length = contentLength;

            return;
        }

        CreateDir(d);
        _content[new KeyValuePair<string, string>(d, f)] = new FakeFile
        {
            _content = content,
            _length = contentLength,
            _lastWriteTimeUtc = DateTime.UtcNow,
        };
        OnFileChange?.Invoke(path);
    }

    private static ulong GetContentLength(object content)
    {
        if (content is string s)
            return (ulong) s.Length;

        if (content is byte[] b)
            return (ulong) b.Length;

        throw new Exception("Unknown content type");
    }

    private void CreateDir(string path)
    {
        GetDirectoryAndFileNames(path, out var d, out var f);
        if (_content.TryGetValue(new KeyValuePair<string, string>(d, f), out var file))
        {
            if (file != null) throw new Exception("mkdir fail already file " + path);

            return;
        }

        if (d != "") CreateDir(d);

        _content[new KeyValuePair<string, string>(d, f)] = null;
    }

    private static void GetDirectoryAndFileNames(string path, out string dir, out string file)
    {
        if (path.StartsWith('/')) path = path[1..];
        var d = PathUtils.SplitDirAndFile(path, out var ff).ToString();
        var f = ff.ToString();
        dir = d;
        file = f;
    }

    private class FakeFile
    {
        public object _content;
        public DateTime _lastWriteTimeUtc;
        public ulong _length;
    }
}