using System;
using Lib.WebServer;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Collections.Generic;
using Lib.Utils;
using Lib.Utils.Logger;
using Njsast.SourceMap;

namespace Lib.Composition;

class TestServer
{
    public readonly ConcurrentDictionary<TestServerConnectionHandler, TestServerConnectionHandler> Clients = new();
    public readonly ConcurrentDictionary<TestServerConnectionHandler, TestResultsHolder> LastResults = new();

    int _runid;
    public readonly Subject<Unit> OnChange = new();
    public readonly Subject<Unit> OnTestingStarted = new();
    public readonly Subject<TestResultsHolder> OnTestResults = new();
    public readonly Subject<TestResultsHolder> OnCoverageResults = new();
    internal readonly Subject<Unit> OnChangeRaw = new();
    public bool Verbose;
    public ILogger Logger;

    public TestServer(bool verbose, ILogger logger)
    {
        Verbose = verbose;
        Logger = logger;
        OnChangeRaw.Throttle(TimeSpan.FromMilliseconds(500)).Subscribe(OnChange);
    }

    public string Url { get; internal set; }
    public IDictionary<string, SourceMap> SourceMaps { get; internal set; }

    public ILongPollingConnectionHandler NewConnectionHandler()
    {
        return new TestServerConnectionHandler(this);
    }

    public void StartTest(string url, IDictionary<string, SourceMap> sourceMaps, string specFilter = "")
    {
        _runid++;
        Url = url;
        SourceMaps = sourceMaps;
        LastResults.Clear();
        foreach (var client in Clients.Keys)
        {
            client.StartTest(url, _runid, specFilter);
        }
    }

    internal void NotifyFinishedResults(TestServerConnectionHandler client, TestResultsHolder newResults)
    {
        LastResults.AddOrUpdate(client, newResults, (_, _) => newResults);
        OnTestResults.OnNext(newResults);
    }

    internal void NotifySomeChange()
    {
        OnChangeRaw.OnNext(Unit.Default);
    }

    internal void NotifyTestingStarted()
    {
        OnTestingStarted.OnNext(Unit.Default);
    }

    internal TestServerState GetState()
    {
        var result = new TestServerState();
        var clients = new HashSet<TestServerConnectionHandler>();
        foreach (var client in Clients.Keys)
        {
            clients.Add(client);
            result.Agents.Add(client.GetLatestResults());
        }

        foreach (var (client, results) in LastResults)
        {
            if (clients.Add(client))
            {
                result.Agents.Add(results);
            }
        }

        return result;
    }
}
