using System;

namespace Lib.Watcher;

public class DummyWatcher : IDirectoryWatcher
{
    public void Dispose()
    {
    }

    public string WatchedDirectory { get; set; } = "";
    public Action<string> OnFileChange { get; set; } = _ => { };
    public Action OnError { get; set; } = () => { };
}