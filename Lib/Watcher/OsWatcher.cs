using System;
using System.IO;
using Lib.Utils;

namespace Lib.Watcher
{
    public class OsWatcher : IDirectoryWatcher
    {
        public string WatchedDirectory
        {
            get => _watchedDirectory;
            set { _watchedDirectory = value; RecreateFileSystemWatcher(); }
        }

        string _watchedDirectory;

        FileSystemWatcher _fileSystemWatcher;

        readonly object _createLock = new object();

        public OsWatcher()
        {
        }

        public Action<string> OnFileChange { get; set; }

        public Action OnError { get; set; }

        void WatcherErrorHandler(object sender, ErrorEventArgs e)
        {
            RecreateFileSystemWatcher();
            OnError?.Invoke();
        }

        void WatcherRenameHandler(object sender, RenamedEventArgs e)
        {
            NotifyChange(e.OldFullPath);
            NotifyChange(e.FullPath);

            if (Directory.Exists(e.FullPath))
            {
                foreach (var newLocation in Directory.EnumerateFileSystemEntries(e.FullPath, "*", SearchOption.AllDirectories))
                {
                    // Calculated previous path of this moved item.
                    var oldLocation = Path.Combine(e.OldFullPath, newLocation.Substring(e.FullPath.Length + 1));
                    NotifyChange(oldLocation);
                    NotifyChange(newLocation);
                }
            }
        }

        void WatcherChangeHandler(object sender, FileSystemEventArgs e)
        {
            NotifyChange(e.FullPath);
        }

        void NotifyChange(string fullPath)
        {
            OnFileChange?.Invoke(PathUtils.Normalize(fullPath));
        }

        void RecreateFileSystemWatcher()
        {
            lock (_createLock)
            {
                if (_fileSystemWatcher != null)
                {
                    _fileSystemWatcher.EnableRaisingEvents = false;
                    _fileSystemWatcher.Created -= WatcherChangeHandler;
                    _fileSystemWatcher.Deleted -= WatcherChangeHandler;
                    _fileSystemWatcher.Changed -= WatcherChangeHandler;
                    _fileSystemWatcher.Renamed -= WatcherRenameHandler;
                    _fileSystemWatcher.Error -= WatcherErrorHandler;
                    _fileSystemWatcher.Dispose();
                }
                _fileSystemWatcher = new FileSystemWatcher(WatchedDirectory);
                _fileSystemWatcher.IncludeSubdirectories = true;
                _fileSystemWatcher.Created += WatcherChangeHandler;
                _fileSystemWatcher.Deleted += WatcherChangeHandler;
                _fileSystemWatcher.Changed += WatcherChangeHandler;
                _fileSystemWatcher.Renamed += WatcherRenameHandler;
                _fileSystemWatcher.Error += WatcherErrorHandler;
                _fileSystemWatcher.EnableRaisingEvents = true;
            }
        }

        public void Dispose()
        {
            _fileSystemWatcher.Dispose();
        }
    }
}
