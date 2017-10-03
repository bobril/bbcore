using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using Lib.Utils;
using Lib.Watcher;

namespace Lib.DiskCache
{
    public class DiskCache : IDiskCache
    {
        readonly Func<IDirectoryWatcher> _directoryWatcherFactory;
        readonly List<string> _rootPaths = new List<string>();
        HashSet<string> _watchedPaths = new HashSet<string>();
        readonly Dictionary<string, IDirectoryWatcher> _watchers = new Dictionary<string, IDirectoryWatcher>();
        readonly IDirectoryCache _root;
        readonly object _lock = new object();

        readonly IFsAbstraction _fsAbstraction;
        readonly bool IsUnixFs;

        public IFsAbstraction FsAbstraction { get => _fsAbstraction; }

        class DirectoryCache : IDirectoryCache
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public IDirectoryCache Parent { get; set; }
            public bool IsFile => false;
            public bool IsDirectory => true;
            public bool IsInvalid { get => _isInvalid; set { _isInvalid = value; NoteChange(); } }
            public bool IsStale { get; set; }
            public bool IsFake { get; set; }
            public Func<(IDirectoryCache parent, string name, bool isDir), bool> Filter { get; set; }

            public int ChangeId => _changeId;

            public object AdditionalInfo { get; set; }

            public List<IItemCache> Items = new List<IItemCache>();
            public List<IFileCache> VirtualFiles = new List<IFileCache>();
            private int _changeId;
            private bool _isInvalid;
            private DiskCache _owner;

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

            public IItemCache TryGetChild(string name)
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
                            if (((VirtualFileCache)item).Utf8Content == data) return false;
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
        }

        public IObservable<Unit> ChangeObservable { get => _changeSubject; }

        Subject<Unit> _changeSubject = new Subject<Unit>();
            
        private void NotifyChange()
        {
            _changeSubject.OnNext(Unit.Default);
        }

        public DiskCache(IFsAbstraction fsAbstraction, Func<IDirectoryWatcher> directoryWatcherFactory)
        {
            _fsAbstraction = fsAbstraction;
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
                    if (tuple.name == ".git" || tuple.name == ".hg") return false;
                }
                return true;
            };
        }

        public IDisposable AddRoot(string path)
        {
            path = PathUtils.Normalize(path);
            lock (_lock)
            {
                _rootPaths.Add(path);
                RebuildWatchers();
                return new Disposer(() => RemoveRoot(path));
            }
        }

        void RemoveRoot(string path)
        {
            lock (_lock)
            {
                _rootPaths.Remove(path);
                RebuildWatchers();
            }
        }

        void RebuildWatchers()
        {
            var trueRoots = new HashSet<string>();
            foreach (var rootPath in _rootPaths.OrderBy(s => s.Length))
            {
                var p = rootPath;
                while (p != null)
                {
                    if (trueRoots.Contains(p)) goto skip;
                    p = PathUtils.Parent(p);
                }
                trueRoots.Add(rootPath);
            skip:
                ;
            }
            if (_watchedPaths.SetEquals(trueRoots))
                return;
            foreach (var watchedPath in _watchedPaths)
            {
                if (trueRoots.Contains(watchedPath))
                    continue;
                _watchers.TryGetValue(watchedPath, out var w);
                Debug.Assert(w != null, "_watchers and _watchedPaths got unsynced with " + watchedPath);
                w.Dispose();
                _watchers.Remove(watchedPath);
                var directory = (IDirectoryCache)TryGetItemNoLock(watchedPath);
                while (directory.Parent != null)
                {
                    directory.IsInvalid = true;
                    var parent = directory.Parent;
                    ((DirectoryCache)parent).Remove(directory);
                    if (((DirectoryCache)parent).Items.Count != 0) break;
                    directory = parent;
                }
            }
            foreach (var trueRoot in trueRoots)
            {
                if (!_watchedPaths.Add(trueRoot))
                    continue;
                var w = _directoryWatcherFactory();
                w.OnError = WatcherError;
                w.OnFileChange = WatcherFileChanged;
                w.WatchedDirectory = trueRoot;
                _watchers.Add(trueRoot, w);
                var directory = _root;
                foreach ((string name, bool isDir) in PathUtils.EnumParts(trueRoot))
                {
                    var subDir = directory.TryGetChild(name);
                    if (!isDir)
                    {
                        Debug.Assert(subDir == null);
                        AddDirectoryFromName(name, directory);
                    }
                    else
                    {
                        if (subDir == null)
                        {
                            var subDir2 = AddDirectoryFromName(name, directory);
                            subDir2.IsFake = true;
                            subDir2.IsStale = false;
                            subDir = subDir2;
                        }
                        directory = (IDirectoryCache)subDir;
                    }
                }
            }
            _watchedPaths = trueRoots;
        }

        void WatcherError()
        {
            Console.WriteLine("Watcher error. Restarting.");
        }

        void WatcherFileChanged(string path)
        {
            var fi = _fsAbstraction.GetItemInfo(path);
            if (fi.Exists && !fi.IsDirectory)
            {
                lock (_lock)
                {
                    var directory = _root;
                    foreach ((string name, bool isDir) in PathUtils.EnumParts(path))
                    {
                        if (isDir)
                        {
                            var subDir = directory.TryGetChild(name);
                            if (subDir == null)
                            {
                                if (!directory.Filter((directory, name, true)))
                                    return;
                                subDir = AddDirectoryFromName(name, directory);
                            }
                            else
                            {
                                if (!subDir.IsDirectory)
                                {
                                    subDir.IsInvalid = true;
                                    ((DirectoryCache)directory).Remove(subDir);
                                    if (!directory.Filter((directory, name, true)))
                                        return;
                                    subDir = AddDirectoryFromName(name, directory);
                                }
                            }
                            directory = (IDirectoryCache)subDir;
                        }
                        else
                        {
                            var subFile = directory.TryGetChild(name);
                            if (subFile == null)
                            {
                                if (!directory.Filter((directory, name, false)))
                                    return;
                                AddFileFromFileInfo(name, directory, fi);
                            }
                            else
                            {
                                if (!subFile.IsFile)
                                {
                                    subFile.IsInvalid = true;
                                    ((DirectoryCache)directory).Remove(subFile);
                                    if (!directory.Filter((directory, name, false)))
                                        return;
                                    AddFileFromFileInfo(name, directory, fi);
                                }
                                else
                                {
                                    subFile.IsStale = true;
                                }
                            }
                        }
                    }
                }
                return;
            }
            if (fi.Exists && fi.IsDirectory)
            {
                lock (_lock)
                {
                    var directory = _root;
                    foreach ((var name, _) in PathUtils.EnumParts(path))
                    {
                        var subDir = directory.TryGetChild(name);
                        if (subDir == null)
                        {
                            if (!directory.Filter((directory, name, true)))
                                return;
                            subDir = AddDirectoryFromName(name, directory);
                        }
                        else
                        {
                            if (!subDir.IsDirectory)
                            {
                                subDir.IsInvalid = true;
                                ((DirectoryCache)directory).Remove(subDir);
                                if (!directory.Filter((directory, name, true)))
                                    return;
                                subDir = AddDirectoryFromName(name, directory);
                            }
                        }
                        directory = (IDirectoryCache)subDir;
                    }
                }
            }
            else
            {
                lock (_lock)
                {
                    var directory = _root;
                    foreach ((string name, bool isDir) in PathUtils.EnumParts(path))
                    {
                        if (isDir)
                        {
                            var subDir = directory.TryGetChild(name);
                            if (subDir == null)
                            {
                                return;
                            }
                            if (!subDir.IsDirectory)
                            {
                                ((DirectoryCache)directory).Remove(subDir);
                                directory.IsStale = true;
                                return;
                            }
                            directory = (IDirectoryCache)subDir;
                        }
                        else
                        {
                            var subDir = directory.TryGetChild(name);
                            if (subDir != null)
                            {
                                subDir.IsInvalid = true;
                                ((DirectoryCache)directory).Remove(subDir);
                            }
                        }
                    }
                }
            }
        }

        IDirectoryCache AddDirectoryFromName(string name, IDirectoryCache parent)
        {
            var subDir = new DirectoryCache(this)
            {
                Name = name,
                FullPath = parent.FullPath + (parent != _root ? "/" : "") + name,
                Parent = parent,
                Filter = DefaultFilter,
                IsStale = true
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

        class VirtualFileCache : IFileCache
        {
            string _name;
            string _fullName;
            DateTime _modified;
            int _changeId;
            bool _isInvalid;
            int _length;
            string _content;
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
                _modified = DateTime.UtcNow;
                _changeId++; // It is called always under lock
                // don't NoteChange to parent because that's not really user modified input file
            }

            public DateTime Modified => _modified;

            public long Length => _length;

            public byte[] ByteContent => Encoding.UTF8.GetBytes(_content);

            public string Utf8Content => _content;

            public object AdditionalInfo { get; set; }

            public string Name => _name;

            public string FullPath => _fullName;

            public IDirectoryCache Parent => _parent;

            public int ChangeId => _changeId;

            public bool IsFile => true;

            public bool IsDirectory => false;

            public bool IsInvalid { get => _isInvalid; set => _isInvalid = value; }
            public bool IsStale { get => _isInvalid; set => throw new InvalidOperationException(); }
        }

        class FileCache : IFileCache
        {
            public string Name { get; set; }
            public string FullPath { get; set; }

            byte[] _contentBytes;
            string _contentUtf8;
            bool _isStale;
            int _changeId = 1;
            bool _isInvalid;

            public IDiskCache Owner { get; set; }
            public IDirectoryCache Parent { get; set; }
            public bool IsFile => true;
            public bool IsDirectory => false;
            public bool IsInvalid { get => _isInvalid; set { _isInvalid = value; if (value) IsStale = true; } }

            public bool IsStale
            {
                get => _isStale;
                set
                {
                    _isStale = value;
                    _contentBytes = null;
                    _contentUtf8 = null;
                    _changeId++;
                    if (Parent != null) ((DirectoryCache)Parent).NoteChange();
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
                        _contentUtf8 = _contentBytes == null ? ((DiskCache)Owner).FsAbstraction.ReadAllUtf8(FullPath) : Encoding.UTF8.GetString(_contentBytes);
                    }
                    return _contentUtf8;
                }
            }

            public int ChangeId => _changeId;

            public object AdditionalInfo { get; set; }

            long IFileCache.Length => throw new NotImplementedException();
        }

        public IItemCache TryGetItem(string path)
        {
            lock (_lock)
            {
                return TryGetItemNoLock(path);
            }
        }

        IItemCache TryGetItemNoLock(string path)
        {
            var directory = _root;
            foreach ((string name, bool isDir) in PathUtils.EnumParts(path))
            {
                var subItem = directory.TryGetChild(name);
                if (isDir)
                {
                    if (subItem == null || !subItem.IsDirectory)
                    {
                        return null;
                    }
                    directory = (IDirectoryCache)subItem;
                    UpdateIfNeededNoLock(directory);
                }
                else
                {
                    if (subItem is IDirectoryCache)
                    {
                        UpdateIfNeededNoLock((IDirectoryCache)subItem);
                    }
                    return subItem;
                }
            }
            return directory;
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
                    if (items == origItems) items = origItems.ToList();
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
                var item = directory.TryGetChild(fsi.Name);
                if (item == null)
                {
                    if (fsi.IsDirectory)
                    {
                        AddDirectoryFromName(fsi.Name, directory);
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
                            AddDirectoryFromName(fsi.Name, directory);
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
                directory.IsStale = false;
            }
        }
    }
}
