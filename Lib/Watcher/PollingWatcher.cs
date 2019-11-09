using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;

namespace Lib.Watcher
{
    public class PollingWatcher : IDirectoryWatcher
    {
        readonly TimeSpan _delay;
        string _watchedDirectory;
        Timer? _timer;
        List<(string, long, DateTime)>? _list;

        public PollingWatcher(TimeSpan delay)
        {
            _delay = delay;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public string WatchedDirectory
        {
            get => _watchedDirectory;
            set
            {
                _watchedDirectory = value;
                _timer?.Dispose();
                _list = null;
                _timer = new Timer(OnTimer, null, _delay, _delay);
            }
        }

        void OnTimer(object? state)
        {
            try
            {
                var enumerable = new FileSystemEnumerable<(string, long, DateTime)>(_watchedDirectory, Transform,
                    new EnumerationOptions {RecurseSubdirectories = true, ReturnSpecialDirectories = false});
                var newList = enumerable.OrderBy(o => o.Item1).ToList();
                if (_list == null)
                {
                    _list = newList;
                    return;
                }

                if (_list.SequenceEqual(newList))
                {
                    return;
                }

                var hashSet = _list.ToHashSet();
                hashSet.SymmetricExceptWith(newList);
                foreach (var name in hashSet.Select(i => i.Item1).Distinct())
                {
                    OnFileChange.Invoke(name);
                }

                _list = newList;
            }
            catch (Exception)
            {
                OnError?.Invoke();
            }
        }

        static (string, long, DateTime) Transform(ref FileSystemEntry entry)
        {
            if (entry.IsDirectory) return ("", -1, DateTime.MinValue);
            return (entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.UtcDateTime);
        }

        public Action<string> OnFileChange { get; set; }
        public Action OnError { get; set; }
    }
}
