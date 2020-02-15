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

namespace Lib.Composition
{
    class TestServer
    {
        public readonly ConcurrentDictionary<TestServerConnectionHandler, TestServerConnectionHandler> Clients =
            new ConcurrentDictionary<TestServerConnectionHandler, TestServerConnectionHandler>();

        int _runid;
        public Subject<Unit> OnChange = new Subject<Unit>();
        public Subject<Unit> OnTestingStarted = new Subject<Unit>();
        public Subject<TestResultsHolder> OnTestResults = new Subject<TestResultsHolder>();
        public Subject<TestResultsHolder> OnCoverageResults = new Subject<TestResultsHolder>();
        internal Subject<Unit> OnChangeRaw = new Subject<Unit>();
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
            foreach (var client in Clients.Keys)
            {
                client.StartTest(url, _runid, specFilter);
            }
        }

        internal void NotifyFinishedResults(TestResultsHolder oldResults)
        {
            OnTestResults.OnNext(oldResults);
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
            TestServerState result = new TestServerState();
            foreach (var client in Clients.Keys)
            {
                result.Agents.Add(client.GetLatestResults());
            }

            return result;
        }
    }
}
