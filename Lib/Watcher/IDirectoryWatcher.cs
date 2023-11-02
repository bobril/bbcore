using System;

namespace Lib.Watcher;

public interface IDirectoryWatcher : IDisposable
{
    string WatchedDirectory { get; set; }
    Action<string> OnFileChange { get; set; }
    Action OnError { get; set; }
}