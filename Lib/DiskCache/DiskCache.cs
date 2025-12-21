using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using BTDB.Collections;
using Lib.Utils;
using Lib.Watcher;

namespace Lib.DiskCache;

public class DiskCache : IDiskCache
{
    readonly Func<IDirectoryWatcher> _directoryWatcherFactory;
    readonly Dictionary<string, IDirectoryWatcher> _watchers = new();
    readonly IDirectoryCache _root;
    readonly object _lock = new();
    readonly bool IsUnixFs;
    bool _changed;

    public IFsAbstraction FsAbstraction { get; }
    public string? IgnoreChangesInPath { get; set; }

    class DirectoryCache : IDirectoryCache
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public IDirectoryCache? Parent { get; set; }
        public bool IsFile => false;
        public bool IsDirectory => true;

        public bool IsInvalid
        {
            get => _isInvalid;
            set
            {
                if (!_isInvalid && value)
                {
                    foreach (var i in Items.Values) i.IsInvalid = true;
                }

                var wasChange = _isInvalid != value;
                _isInvalid = value;
                if (value) IsWatcherRoot = false;
                if (wasChange) NoteChange();
            }
        }

        public bool IsStale
        {
            get => field;
            set
            {
                _caseInsensitiveItems = null;
                if (value == false) WasNonStale = true;
                field = value;
            }
        }

        public bool IsFake { get; set; }
        public bool IsLink { get; set; }
        public object? Project { get; set; }

        public bool IsWatcherRoot
        {
            get;
            set
            {
                if (field == value)
                    return;
                field = value;
                if (value)
                {
                    var w = Owner._directoryWatcherFactory();
                    w.OnError = Owner.WatcherError;
                    w.OnFileChange = Owner.WatcherFileChanged;
                    w.WatchedDirectory = FullPath;
                    Owner._watchers.Add(FullPath, w);
                }
                else
                {
                    if (Owner._watchers.Remove(FullPath, out var w))
                    {
                        w.Dispose();
                    }
                }
            }
        }

        public Func<(IDirectoryCache parent, string name, bool isDir), bool> Filter { get; set; }

        public IDirectoryCache RealPath
        {
            get
            {
                if (field == null)
                {
                    var fp = FullPath;
                    var fp2 = Owner.FsAbstraction.RealPath(fp);
                    if (fp2 == fp)
                    {
                        field = this;
                    }
                    else
                    {
                        field = Owner.TryGetItem(fp2) as IDirectoryCache ?? this;
                    }
                }

                return field;
            }
        }

        public bool WasNonStale { get; private set; }

        public int ChangeId => _changeId;

        public Dictionary<string, IItemCache> Items = new();
        Dictionary<string, IItemCache>? _caseInsensitiveItems;

        int _changeId;
        bool _isInvalid;
        internal readonly DiskCache Owner;

        public DirectoryCache(DiskCache owner, bool isInvalid)
        {
            Owner = owner;
            _isInvalid = isInvalid;
            IsStale = true;
        }

        internal void NoteChange()
        {
            _changeId++; // It is called always under lock
            Owner.NotifyChange(FullPath);
        }

        public IEnumerator<IItemCache> GetEnumerator()
        {
            var items = Items;
            foreach (var t in items)
            {
                yield return t.Value;
            }
        }

        public IItemCache? TryGetChild(ReadOnlySpan<char> name)
        {
            if (Items.TryGetValue(new string(name), out var value))
                return value;
            if (_caseInsensitiveItems == null)
            {
                _caseInsensitiveItems =
                    new Dictionary<string, IItemCache>(Items.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var t in Items)
                {
                    _caseInsensitiveItems.TryAdd(t.Key, t.Value);
                }
            }

            _caseInsensitiveItems.TryGetValue(new string(name), out value);
            return value;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IItemCache item)
        {
            Items.Add(item.Name, item);
            _caseInsensitiveItems = null;
        }
    }

    public IObservable<string> ChangeObservable => _changeSubject;

    readonly Subject<string> _changeSubject = new();

    public void NotifyChange(string where)
    {
        if (_ignoringChanges) return;
        LastTrueChange = where;
        _changed = true;
    }

    public DiskCache(IFsAbstraction fsAbstraction, Func<IDirectoryWatcher> directoryWatcherFactory)
    {
        LastTrueChange = "First build";
        FsAbstraction = fsAbstraction;
        _directoryWatcherFactory = directoryWatcherFactory;
        _root = new DirectoryCache(this, false);
        _root.IsFake = true;
        IsUnixFs = fsAbstraction.IsUnixFs;
        if (IsUnixFs)
        {
            ((DirectoryCache)_root).Name = "/";
            ((DirectoryCache)_root).FullPath = "/";
        }
        else
        {
            ((DirectoryCache)_root).Name = "";
            ((DirectoryCache)_root).FullPath = "";
        }

        DefaultFilter = tuple => tuple is not { isDir: true, name: ".git" or ".hg" };
        _root.Filter = DefaultFilter;
    }

    void WatcherError()
    {
        //Console.WriteLine("Watcher error. Restarting.");
    }

    void WatcherFileChanged(string path)
    {
        if (IgnoreChangesInPath != null && path.StartsWith(IgnoreChangesInPath, StringComparison.Ordinal))
            return;
        //Console.WriteLine("Change: " + path);
        lock (_lock)
        {
            var item = TryGetItemNoLock(PathUtils.Parent(path));
            if (item is IDirectoryCache { IsFake: false } dir)
            {
                //Console.WriteLine("Setting stale for " + dir.FullPath);
                dir.IsStale = true;
                _changeSubject.OnNext(path);
            }
        }
    }

    IDirectoryCache AddDirectoryFromName(string name, IDirectoryCache parent, bool isLink, bool isInvalid)
    {
        var subDir = new DirectoryCache(this, isInvalid)
        {
            Name = name,
            FullPath = parent.FullPath + (parent != _root ? "/" : "") + name,
            Parent = parent,
            Filter = DefaultFilter,
            IsStale = true,
            IsLink = isLink,
            IsFake = true
        };
        ((DirectoryCache)parent).Add(subDir);
        return subDir;
    }

    void AddFileFromFileInfo(string name, IDirectoryCache directory, FsItemInfo fi)
    {
        IItemCache subFile = new FileCache
        {
            Name = name,
            FullPath = directory.FullPath + (directory != _root ? "/" : "") + name,
            Owner = this,
            Parent = directory,
            Modified = fi.LastWriteTimeUtc,
            Length = fi.Length
        };
        ((DirectoryCache)directory).Add(subFile);
    }

    static SHA1 _hashFunction = new SHA1Managed();

    static byte[] CalcHash(byte[] value)
    {
        return _hashFunction.ComputeHash(value);
    }

    class FileCache : IFileCache
    {
        public string Name { get; set; }
        public string FullPath { get; set; }

        byte[]? _contentBytes;
        byte[]? _contentHash;
        bool _isStale;
        bool _wasLoaded;
        bool _isInvalid;

        public IDiskCache Owner { get; set; }
        public IDirectoryCache? Parent { get; set; }
        public bool IsFile => true;
        public bool IsDirectory => false;

        public bool IsInvalid
        {
            get => _isInvalid;
            set
            {
                _isInvalid = value;
                if (value) IsStale = true;
            }
        }

        public bool IsStale
        {
            get => _isStale;
            set
            {
                if (_contentBytes != null || _wasLoaded)
                {
                    byte[]? newBytes = null;
                    try
                    {
                        newBytes = ((DiskCache)Owner).FsAbstraction.ReadAllBytes(FullPath);
                    }
                    catch
                    {
                        // ignore
                    }

                    if (newBytes != null && _contentBytes != null && newBytes.SequenceEqual(_contentBytes))
                    {
                        _isStale = false;
                        _wasLoaded = false;
                        return;
                    }

                    _contentBytes = newBytes;
                    _contentHash = null;
                    ChangeId++;
                    (Parent as DirectoryCache)?.Owner.NotifyChange(FullPath);
                }

                _isStale = value;
            }
        }

        public DateTime Modified { get; set; }
        public ulong Length { get; set; }

        public byte[] ByteContent => _contentBytes ??= ((DiskCache)Owner).FsAbstraction.ReadAllBytes(FullPath);

        public string Utf8Content => Encoding.UTF8.GetString(ByteContent);

        public int ChangeId { get; private set; } = 1;

        public void FreeCache()
        {
            _contentBytes = null;
            _contentHash = null;
            _wasLoaded = true;
        }

        public byte[] HashOfContent => _contentHash ??= CalcHash(ByteContent);
    }

    public IItemCache? TryGetItem(ReadOnlySpan<char> path)
    {
        lock (_lock)
        {
            return TryGetItemNoLock(path);
        }
    }

    IItemCache? TryGetItemNoLock(ReadOnlySpan<char> path)
    {
        var directory = _root;
        var pos = 0;
        while (PathUtils.EnumParts(path, ref pos, out var name, out var isDir))
        {
            var subItem = directory.TryGetChild(name);
            if (isDir)
            {
                if (subItem == null)
                {
                    if (!directory.IsFake)
                    {
                        return null;
                    }

                    var info = FsAbstraction.GetItemInfo(path.Slice(0, pos));
                    subItem = AddDirectoryFromName(name.ToString(), directory, info.IsLink,
                        !info.Exists || !info.IsDirectory);
                }

                if (!subItem.IsDirectory)
                {
                    return null;
                }

                directory = (IDirectoryCache)subItem;
                if (!directory.IsFake)
                    UpdateIfNeededNoLock(directory);
            }
            else
            {
                if (subItem is IDirectoryCache)
                {
                    UpdateIfNeededNoLock((IDirectoryCache)subItem);
                    return (subItem?.IsInvalid ?? true) ? null : subItem;
                }

                if (subItem != null || !directory.IsFake)
                    return (subItem?.IsInvalid ?? true) ? null : subItem;
                var info = FsAbstraction.GetItemInfo(path);
                if (info.Exists && !info.IsDirectory)
                {
                    UpdateIfNeededNoLock(directory);
                    subItem = directory.TryGetChild(name);
                }
                else
                {
                    subItem = AddDirectoryFromName(name.ToString(), directory, info.IsLink, !info.Exists);
                    if (info.Exists)
                    {
                        UpdateIfNeededNoLock((IDirectoryCache)subItem);
                        foreach (var item in (IDirectoryCache)subItem)
                        {
                            if (item is IDirectoryCache &&
                                (!((IDirectoryCache)item).IsStale || ((IDirectoryCache)item).IsFake))
                                CheckUpdateIfNeededNoLock((IDirectoryCache)item);
                        }
                    }
                }

                return (subItem?.IsInvalid ?? true) ? null : subItem;
            }
        }

        return (directory?.IsInvalid ?? true) ? null : directory;
    }

    bool _ignoringChanges;

    public bool UpdateFile(string path, string content)
    {
        lock (_lock)
        {
            _ignoringChanges = true;
            try
            {
                var fi = TryGetItemNoLock(path);
                if (fi == null)
                {
                    FsAbstraction.WriteAllUtf8(path, content);
                    var dir = PathUtils.SplitDirAndFile(path, out var file);
                    AddFileFromFileInfo(file.ToString(), (IDirectoryCache)TryGetItemNoLock(dir)!,
                        FsAbstraction.GetItemInfo(path));
                    fi = TryGetItemNoLock(path);
                }

                if (fi is not { IsFile: true })
                {
                    throw new("Cannot update file " + path);
                }

                if (((IFileCache)fi).Utf8Content != content)
                {
                    FsAbstraction.WriteAllUtf8(path, content);
                    ((FileCache)fi).IsStale = false;
                    return true;
                }
            }
            finally
            {
                _ignoringChanges = false;
            }
        }

        return false;
    }

    public void ResetChange()
    {
        _changed = false;
        LastTrueChange = "";
    }

    public IDirectoryCache Root() => _root;

    public Func<(IDirectoryCache parent, string name, bool isDir), bool> DefaultFilter { get; set; }

    public string LastTrueChange { get; private set; }

    public void UpdateIfNeeded(IItemCache item)
    {
        if (item.IsDirectory)
            lock (_lock)
            {
                UpdateIfNeededNoLock((IDirectoryCache)item);
            }
    }

    void UpdateIfNeededNoLock(IDirectoryCache directory)
    {
        if (!directory.IsStale)
        {
            return;
        }

        //Console.WriteLine("Updating " + directory.FullPath);
        var wasFake = directory.IsFake;
        directory.IsFake = false;
        var fullPath = directory.FullPath;
        if (!FsAbstraction.GetItemInfo(fullPath).Exists)
        {
            directory.IsInvalid = true;
        }
        else
        {
            var fsis = FsAbstraction.GetDirectoryContent(fullPath);
            var origItems = ((DirectoryCache)directory).Items;
            ((DirectoryCache)directory).Items = new Dictionary<string, IItemCache>();

            var wasChanged = false;

            foreach (var fsi in fsis)
            {
                if (!directory.Filter((directory, fsi.Name, fsi.IsDirectory)))
                    continue;
                origItems.TryGetValue(fsi.Name, out var item);
                if (item == null)
                {
                    wasChanged = true;
                    if (fsi.IsDirectory)
                    {
                        AddDirectoryFromName(fsi.Name, directory, fsi.IsLink, false);
                    }
                    else
                    {
                        AddFileFromFileInfo(fsi.Name, directory, fsi);
                    }
                }
                else
                {
                    if (fsi.IsDirectory)
                    {
                        if (!item.IsDirectory)
                        {
                            item.IsInvalid = true;
                            AddDirectoryFromName(fsi.Name, directory, fsi.IsLink, false);
                            wasChanged = true;
                        }
                        else
                        {
                            ((IDirectoryCache)item).IsLink = fsi.IsLink;
                            origItems.Remove(fsi.Name);
                            ((DirectoryCache)directory).Items.Add(fsi.Name, item);
                        }
                    }
                    else
                    {
                        if (item.IsFile)
                        {
                            var fitem = (FileCache)item;
                            if (fitem.Modified != fsi.LastWriteTimeUtc || fitem.Length != fsi.Length)
                            {
                                fitem.Modified = fsi.LastWriteTimeUtc;
                                fitem.Length = fsi.Length;
                                fitem.IsStale = false;
                            }

                            origItems.Remove(fsi.Name);
                            ((DirectoryCache)directory).Items.Add(fsi.Name, item);
                        }
                        else
                        {
                            item.IsInvalid = true;
                            AddFileFromFileInfo(fsi.Name, directory, fsi);
                            wasChanged = true;
                        }
                    }
                }
            }

            if (wasChanged || origItems.Count != 0)
            {
                if (!wasFake) ((DirectoryCache)directory).NoteChange();
            }
        }

        directory.IsWatcherRoot =
            !directory.IsInvalid && (directory.IsLink || NotWatchedByParents(directory.Parent));
        directory.IsStale = false;
    }

    bool NotWatchedByParents(IDirectoryCache directory)
    {
        while (directory != null)
        {
            if (directory.IsWatcherRoot) return false;
            directory = directory.Parent;
        }

        return true;
    }

    public bool CheckForTrueChange()
    {
        lock (_lock)
        {
            CheckUpdateIfNeededNoLock(_root);
            return _changed;
        }
    }

    void CheckUpdateIfNeededNoLock(IDirectoryCache directory)
    {
        if (directory.IsFake)
        {
            foreach (var item in directory)
            {
                CheckUpdateIfNeededNoLock((IDirectoryCache)item);
            }
        }
        else
        {
            var info = FsAbstraction.GetItemInfo(directory.FullPath);
            if (info.Exists)
            {
                if (info.IsDirectory)
                {
                    directory.IsLink = info.IsLink;
                    directory.IsStale = true;
                    UpdateIfNeededNoLock(directory);
                    foreach (var item in directory)
                    {
                        if (item is IDirectoryCache cache &&
                            (cache.WasNonStale || cache.IsFake))
                            CheckUpdateIfNeededNoLock(cache);
                    }
                }
                else
                {
                    directory.IsInvalid = true;
                }
            }
            else
            {
                directory.IsInvalid = true;
            }
        }
    }

    public void UpdateIfNeeded(IDirectoryCache dir)
    {
        UpdateIfNeeded((IItemCache)dir);
    }
}