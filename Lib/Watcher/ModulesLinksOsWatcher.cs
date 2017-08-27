using System;
using System.Collections.Generic;
using System.IO;
using Lib.Utils;

namespace Lib.Watcher
{
    public class ModulesLinksOsWatcher : IDirectoryWatcher
    {
        string _watchedDirectory;
        IDirectoryWatcher _rootWatcher;
        string _watchingModulesPrefix;
        readonly IDictionary<string, IDirectoryWatcher> _watchers = new Dictionary<string, IDirectoryWatcher>();

        public void Dispose()
        {
            if (_rootWatcher != null)
            {
                _rootWatcher.Dispose();
                _rootWatcher = null;
            }
            foreach (var watcher in _watchers)
            {
                watcher.Value.Dispose();
            }
            _watchers.Clear();
        }

        public string WatchedDirectory
        {
            get => _watchedDirectory;
            set
            {
                _watchedDirectory = value;
                RecreateWatchers();
            }
        }

        void RecreateWatchers()
        {
            Dispose();
            _rootWatcher = new OsWatcher
            {
                OnError = CallOnError,
                OnFileChange = CallOnRootChange,
                WatchedDirectory = _watchedDirectory
            };
            var modulesDir = PathUtils.Join(_watchedDirectory, "node_modules");
            _watchingModulesPrefix = modulesDir;
            if (Directory.Exists(modulesDir))
            {
                var modules = Directory.GetDirectories(modulesDir);
                foreach (var module in modules)
                {
                    NotifyRootChange(PathUtils.Normalize(module));
                }
            }
        }

        void NotifyRootChange(string path)
        {
            var isLink = false;
            try
            {
                var dirInfo = new DirectoryInfo(path);
                if (dirInfo.Exists && (dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    isLink = true;
                }
            }
            catch
            {
                // ignored
            }
            if (isLink)
            {
                if (_watchers.ContainsKey(path))
                    return;
                _watchers[path] = new OsWatcher
                {
                    OnError = CallOnError,
                    OnFileChange = CallOnFileChange,
                    WatchedDirectory = path
                };
            }
            else
            {
                if (_watchers.TryGetValue(path, out var watcher))
                {
                    _watchers.Remove(path);
                    watcher.Dispose();
                }
            }
        }

        void CallOnFileChange(string path)
        {
            OnFileChange?.Invoke(path);
        }

        void CallOnRootChange(string path)
        {
            if (PathUtils.IsChildOf(path, _watchingModulesPrefix))
                NotifyRootChange(path);
            OnFileChange?.Invoke(path);
        }

        void CallOnError()
        {
            OnError?.Invoke();
        }

        public Action<string> OnFileChange { get; set; }
        public Action OnError { get; set; }
    }
}
