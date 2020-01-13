using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using Lib.Utils;
using Lib.Watcher;

namespace Lib.DiskCache
{
    public class DiskCache : IDiskCache
    {
        readonly Func<IDirectoryWatcher> _directoryWatcherFactory;
        readonly Dictionary<string, IDirectoryWatcher> _watchers = new Dictionary<string, IDirectoryWatcher>();
        readonly IDirectoryCache _root;
        readonly object _lock = new object();
        readonly bool IsUnixFs;
        bool _changed;

        public IFsAbstraction FsAbstraction { get; }

        class DirectoryCache : IDirectoryCache
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public IDirectoryCache Parent { get; set; }
            public bool IsFile => false;
            public bool IsDirectory => true;

            public bool IsInvalid
            {
                get => _isInvalid;
                set
                {
                    if (!_isInvalid && value)
                    {
                        foreach (var i in Items) i.IsInvalid = true;
                    }

                    var wasChange = _isInvalid != value;
                    _isInvalid = value;
                    if (value) IsWatcherRoot = false;
                    if (wasChange) NoteChange(true);
                }
            }

            public bool IsStale { get; set; }

            public bool IsFake { get; set; }
            public bool IsLink { get; set; }
            public object? Project { get; set; }

            public bool IsWatcherRoot
            {
                get { return _isWatcherRoot; }
                set
                {
                    if (_isWatcherRoot == value)
                        return;
                    _isWatcherRoot = value;
                    if (value)
                    {
                        var w = _owner._directoryWatcherFactory();
                        w.OnError = _owner.WatcherError;
                        w.OnFileChange = _owner.WatcherFileChanged;
                        w.WatchedDirectory = FullPath;
                        _owner._watchers.Add(FullPath, w);
                    }
                    else
                    {
                        if (_owner._watchers.Remove(FullPath, out var w))
                        {
                            w.Dispose();
                        }
                    }
                }
            }

            public Func<(IDirectoryCache parent, string name, bool isDir), bool> Filter { get; set; }

            public int ChangeId => _changeId;

            public object AdditionalInfo { get; set; }

            public bool IsVirtual => false;

            public List<IItemCache> Items = new List<IItemCache>();

            int _changeId;
            bool _isInvalid;
            DiskCache _owner;
            bool _isWatcherRoot;

            public DirectoryCache(DiskCache owner, bool isInvalid)
            {
                _owner = owner;
                _isInvalid = isInvalid;
            }

            internal void NoteChange(bool wasChanged)
            {
                if (wasChanged)
                    _changeId++; // It is called always under lock
                if (Parent != null)
                {
                    ((DirectoryCache)Parent).NoteChange(wasChanged);
                }
                else
                {
                    _owner.NotifyChange();
                }
            }

            public IEnumerator<IItemCache> GetEnumerator()
            {
                return Items.GetEnumerator();
            }

            public IItemCache TryGetChild(ReadOnlySpan<char> name)
            {
                foreach (var item in Items)
                {
                    if (name.SequenceEqual(item.Name))
                    {
                        return item;
                    }
                }
                foreach (var item in Items)
                {
                    if (name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
                return null;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(IItemCache item)
            {
                var oldItems = Items;
                var newItems = new List<IItemCache>(oldItems.Count + 1);
                newItems.AddRange(oldItems);
                newItems.Add(item);
                Items = newItems;
            }

            public void Remove(IItemCache item)
            {
                var newItems = Items.ToList();
                newItems.Remove(item);
                Items = newItems;
            }
        }

        public IObservable<Unit> ChangeObservable
        {
            get => _changeSubject;
        }

        Subject<Unit> _changeSubject = new Subject<Unit>();

        void NotifyChange()
        {
            _changed = true;
        }

        public DiskCache(IFsAbstraction fsAbstraction, Func<IDirectoryWatcher> directoryWatcherFactory)
        {
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

            DefaultFilter = tuple =>
            {
                if (tuple.isDir)
                {
                    if (tuple.name == ".git" || tuple.name == ".hg")
                        return false;
                }

                return true;
            };
        }

        void WatcherError()
        {
            //Console.WriteLine("Watcher error. Restarting.");
        }

        void WatcherFileChanged(string path)
        {
            //Console.WriteLine("Change: " + path);
            _changeSubject.OnNext(Unit.Default);
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

            byte[] _contentBytes;
            byte[] _contentHash;
            bool _isStale;
            bool _isInvalid;

            public IDiskCache Owner { get; set; }
            public IDirectoryCache Parent { get; set; }
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
                    if (_contentBytes != null)
                    {
                        byte[] newBytes = null;
                        try
                        {
                            newBytes = ((DiskCache)Owner).FsAbstraction.ReadAllBytes(FullPath);
                        }
                        catch
                        {
                            // ignore
                        }

                        if (newBytes != null && newBytes.SequenceEqual(_contentBytes))
                        {
                            _isStale = false;
                            return;
                        }

                        _contentBytes = newBytes;
                        _contentHash = null;
                        ChangeId++;
                        ((DirectoryCache)Parent)?.NoteChange(false);
                    }

                    _isStale = value;
                }
            }

            public DateTime Modified { get; set; }
            public ulong Length { get; set; }

            public byte[] ByteContent
            {
                get
                {
                    if (_contentBytes == null)
                    {
                        _contentBytes = ((DiskCache)Owner).FsAbstraction.ReadAllBytes(FullPath);
                    }

                    return _contentBytes;
                }
            }

            public string Utf8Content
            {
                get
                {
                    return Encoding.UTF8.GetString(ByteContent);
                }
            }

            public int ChangeId { get; private set; } = 1;

            public void FreeCache()
            {
                _contentBytes = null;
                _contentHash = null;
            }

            public byte[] HashOfContent
            {
                get
                {
                    if (_contentHash == null)
                    {
                        _contentHash = CalcHash(ByteContent);
                    }

                    return _contentHash;
                }
            }
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

                        var info = FsAbstraction.GetItemInfo(path.Slice(0,pos));
                        subItem = AddDirectoryFromName(name.ToString(), directory, info.IsLink, !info.Exists || !info.IsDirectory);
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
                        return subItem;
                    }

                    if (subItem != null || !directory.IsFake)
                        return subItem;
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

                    return subItem;
                }
            }

            return directory;
        }

        public void ResetChange()
        {
            _changed = false;
        }

        public IDirectoryCache Root()
        {
            return _root;
        }

        public Func<(IDirectoryCache parent, string name, bool isDir), bool> DefaultFilter { get; set; }

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
                return;
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
                var items = origItems;

                var names = new HashSet<string>();
                foreach (var fsi in fsis)
                {
                    if (!directory.Filter((directory, fsi.Name, fsi.IsDirectory)))
                        continue;
                    names.Add(fsi.Name);
                }

                var realChildren = 0;
                for (var i = items.Count - 1; i >= 0; i--)
                {
                    var item = origItems[i];
                    if (item.IsInvalid) continue;
                    realChildren++;
                    if (!names.Contains(item.Name))
                    {
                        item.IsInvalid = true;
                        if (items == origItems)
                            items = origItems.ToList();
                        items.RemoveAt(i);
                    }
                }

                if (items != origItems)
                {
                    ((DirectoryCache)directory).Items = items;
                    if (!wasFake) ((DirectoryCache)directory).NoteChange(true);
                }
                else if (realChildren != names.Count)
                {
                    if (!wasFake) ((DirectoryCache)directory).NoteChange(true);
                }

                foreach (var fsi in fsis)
                {
                    if (!directory.Filter((directory, fsi.Name, fsi.IsDirectory)))
                        continue;
                    var item = directory.TryGetChild(fsi.Name);
                    if (item == null)
                    {
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
                                ((DirectoryCache)directory).Remove(item);
                                AddDirectoryFromName(fsi.Name, directory, fsi.IsLink, false);
                            }
                            else
                            {
                                ((IDirectoryCache)item).IsLink = fsi.IsLink;
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
                            }
                            else
                            {
                                item.IsInvalid = true;
                                ((DirectoryCache)directory).Remove(item);
                                AddFileFromFileInfo(fsi.Name, directory, fsi);
                            }
                        }
                    }
                }
            }

            directory.IsWatcherRoot = !directory.IsInvalid && (directory.IsLink || NotWatchedByParents(directory.Parent));
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
                            if (item is IDirectoryCache &&
                                (!((IDirectoryCache)item).IsStale || ((IDirectoryCache)item).IsFake))
                                CheckUpdateIfNeededNoLock((IDirectoryCache)item);
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
}
