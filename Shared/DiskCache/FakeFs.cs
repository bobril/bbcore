using System;
using System.Collections.Generic;

namespace Shared.DiskCache;

public class FakeFs : IFsAbstraction
{
    bool _isUnix;
    bool _isMac;
    string? _chromPath;
    public FakeFs(bool isUnix, string chromePath = null, bool isMac = false)
    {
        _isUnix = isUnix;
        _isMac = isMac;
        _chromPath = chromePath;
    }

    public bool IsMac => _isMac;

    public bool IsUnixFs => _isUnix;

    public bool FileExists(string path)
    {
        if (_chromPath == null) {
            return false;
        }
        return path == _chromPath;
    }

    public bool DirectoryExists(string path)
    {
        throw new NotImplementedException();
    }

    public void WriteAllUtf8(string path, string content)
    {
        throw new NotImplementedException();
    }

    public void Delete(string path)
    {
        throw new NotImplementedException();
    }

    public void WriteAllBytes(string path, byte[] bytes)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
    {
        throw new NotImplementedException();
    }

    public FsItemInfo GetItemInfo(ReadOnlySpan<char> path)
    {
        throw new NotImplementedException();
    }

    public byte[] ReadAllBytes(string path)
    {
        throw new NotImplementedException();
    }

    public string ReadAllUtf8(string path)
    {
        throw new NotImplementedException();
    }
}