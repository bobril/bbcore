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
                        foreach (var i in VirtualFiles) i.IsInvalid = true;
                    }

                    var wasChange = _isInvalid != value;
                    _isInvalid = value;
                    if (value) IsWatcherRoot = false;
                    if (wasChange) NoteChange();
                }
            }

            public bool IsStale { get; set; }

            public bool IsFake { get; set; }
            public bool IsLink { get; set; }

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
            public List<IFileCache> VirtualFiles = new List<IFileCache>();
            int _changeId;
            bool _isInvalid;
            DiskCache _owner;
            bool _isWatcherRoot;

            public DirectoryCache(DiskCache owner)
            {
                _owner = owner;
            }

            internal void NoteChange()
            {
                _changeId++; // It is called always under lock
                if (Parent != null)
                {
                    ((DirectoryCache)Parent).NoteChange();
                }
                else
                {
                    _owner.NotifyChange();
                }
            }

            public IEnumerator<IItemCache> GetEnumerator()
            {
                return VirtualFiles.Concat(Items).GetEnumerator();
            }

            public IItemCache TryGetChild(string name, bool preferReal)
            {
                if (preferReal)
                {
                    foreach (var item in Items)
                    {
                        if (item.Name == name)
                        {
                            return item;
                        }
                    }

                    foreach (var item in VirtualFiles)
                    {
                        if (item.Name == name)
                        {
                            return item;
                        }
                    }

                    foreach (var item in Items)
                    {
                        if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }

                    foreach (var item in VirtualFiles)
                    {
                        if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }
                }
                else
                {
                    foreach (var item in VirtualFiles)
                    {
                        if (item.Name == name)
                        {
                            return item;
                        }
                    }

                    foreach (var item in Items)
                    {
                        if (item.Name == name)
                        {
                            return item;
                        }
                    }

                    foreach (var item in VirtualFiles)
                    {
                        if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }

                    foreach (var item in Items)
                    {
                        if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
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

            public bool WriteVirtualFile(string name, string data)
            {
                lock (_owner._lock)
                {
                    foreach (var item in VirtualFiles)
                    {
                        if (item.Name == name)
                        {
                            if (((VirtualFileCache)item).Utf8Content == data)
                                return false;
                            ((VirtualFileCache)item).SetContent(data);
                            return true;
                        }
                    }

                    var vf = new VirtualFileCache(this, name);
                    vf.SetContent(data);
                    VirtualFiles.Add(vf);
                    return true;
                }
            }

            public IItemCache TryGetChildNoVirtual(string name)
            {
                foreach (var item in Items)
                {
                    if (item.Name == name)
                    {
                        return item;
                    }
                }

                return null;
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
            _root = new DirectoryCache(this);
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
            var subDir = new DirectoryCache(this)
            {
                Name = name,
                FullPath = parent.FullPath + (parent != _root ? "/" : "") + name,
                Parent = parent,
                Filter = DefaultFilter,
                IsStale = true,
                IsLink = isLink,
                IsInvalid = isInvalid
            };
            ((DirectoryCache)parent).Add(subDir);
            if (!isInvalid)
                ((DirectoryCache)parent).NoteChange();
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
            ((DirectoryCache)directory).NoteChange();
        }

        static SHA1 _hashFunction = new SHA1Managed();

        static byte[] CalcHash(byte[] value)
        {
            return _hashFunction.ComputeHash(value);
        }

        class VirtualFileCache : IFileCache
        {
            string _name;
            string _fullName;
            DateTime _modified;
            int _changeId;
            int _length;
            string _content;
            byte[] _hash;
            IDirectoryCache _parent;

            public VirtualFileCache(IDirectoryCache parent, string name)
            {
                _parent = parent;
                _name = name;
                _fullName = _parent.FullPath + "/" + name;
            }

            public void SetContent(string content)
            {
                _length = Encoding.UTF8.GetByteCount(content);
                _content = content;
                _hash = null;
                _modified = DateTime.UtcNow;
                _changeId++; // It is called always under lock
                // don't NoteChange to parent because that's not really user modified input file
            }

            public DateTime Modified => _modified;

            public ulong Length => (ulong)_length;

            public byte[] ByteContent => Encoding.UTF8.GetBytes(_content);

            public string Utf8Content => _content;

            public object AdditionalInfo { get; set; }
            public void FreeCache()
            {
            }

            public string Name => _name;

            public string FullPath => _fullName;

            public IDirectoryCache Parent => _parent;

            public int ChangeId => _changeId;

            public bool IsFile => true;

            public bool IsDirectory => false;

            public bool IsInvalid { get; set; }

            public bool IsStale
            {
                get => IsInvalid;
                set => throw new InvalidOperationException();
            }

            public byte[] HashOfContent
            {
                get
                {
                    if (_hash == null)
                    {
                        _hash = CalcHash(ByteContent);
                    }

                    return _hash;
                }
            }

            public bool IsVirtual => true;
        }

        class FileCache : IFileCache
        {
            public string Name { get; set; }
            public string FullPath { get; set; }

            byte[] _contentBytes;
            string _contentUtf8;
            byte[] _contentHash;
            bool _isStale;
            int _changeId = 1;
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
                    if (_contentBytes != null || _contentUtf8 != null)
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
                        }
                        else
                        {
                            string newUtf8 = null;
                            try
                            {
                                newUtf8 = ((DiskCache)Owner).FsAbstraction.ReadAllUtf8(FullPath);
                            }
                            catch
                            {
                                // ignore
                            }

                            if (newUtf8 != null && newUtf8 == _contentUtf8)
                            {
                                _isStale = false;
                                return;
                            }
                        }

                        _contentBytes = null;
                        _contentUtf8 = null;
                        _contentHash = null;
                        _changeId++;
                        ((DirectoryCache)Parent)?.NoteChange();
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
                    if (_contentUtf8 == null)
                    {
                        _contentUtf8 = _contentBytes == null
                            ? ((DiskCache)Owner).FsAbstraction.ReadAllUtf8(FullPath)
                            : Encoding.UTF8.GetString(_contentBytes);
                    }

                    return _contentUtf8;
                }
            }

            public int ChangeId => _changeId;

            public object AdditionalInfo { get; set; }
            public void FreeCache()
            {
                _contentUtf8 = null;
                _contentBytes = null;
                _contentHash = null;
            }

            public byte[] HashOfContent
            {
                get
                {
                    if (_contentHash == null)
                    {
                        if (_contentUtf8 != null && _contentBytes == null)
                        {
                            _contentHash = CalcHash(Encoding.UTF8.GetBytes(_contentUtf8));
                        }
                        else
                        {
                            _contentHash = CalcHash(ByteContent);
                        }
                    }

                    return _contentHash;
                }
            }

            public bool IsVirtual => false;
        }

        public IItemCache TryGetItem(string path)
        {
            lock (_lock)
            {
                return TryGetItemNoLock(path, false);
            }
        }

        public IItemCache TryGetItemPreferReal(string path)
        {
            lock (_lock)
            {
                return TryGetItemNoLock(path, true);
            }
        }

        IItemCache TryGetItemNoLock(string path, bool preferReal)
        {
            var directory = _root;
            foreach ((string name, bool isDir) in PathUtils.EnumParts(path))
            {
                var subItem = directory.TryGetChild(name, preferReal);
                if (isDir)
                {
                    if (subItem == null || !subItem.IsDirectory)
                    {
                        if (!directory.IsFake)
                        {
                            return null;
                        }

                        subItem = AddDirectoryFromName(name, directory, false, false);
                        ((IDirectoryCache)subItem).IsFake = true;
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
                        subItem = directory.TryGetChild(name, preferReal);
                    }
                    else
                    {
                        subItem = AddDirectoryFromName(name, directory, false, !info.Exists);
                        if (info.Exists)
                            CheckUpdateIfNeededNoLock((IDirectoryCache)subItem);
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
            directory.IsFake = false;
            var fsis = FsAbstraction.GetDirectoryContent(directory.FullPath);
            var origItems = ((DirectoryCache)directory).Items;
            var items = origItems;

            var names = new HashSet<string>();
            foreach (var fsi in fsis)
            {
                if (!directory.Filter((directory, fsi.Name, fsi.IsDirectory)))
                    continue;
                names.Add(fsi.Name);
            }

            for (var i = items.Count - 1; i >= 0; i--)
            {
                var item = origItems[i];
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
                ((DirectoryCache)directory).NoteChange();
            }

            foreach (var fsi in fsis)
            {
                if (!directory.Filter((directory, fsi.Name, fsi.IsDirectory)))
                    continue;
                var item = directory.TryGetChildNoVirtual(fsi.Name);
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
