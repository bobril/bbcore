using System;
using Lib.WebServer;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace Lib.Composition
{
    public class MessageAndStack
    {
        public string Message;
        public List<StackFrame> Stack;
    }

    public class SuiteOrTest
    {
        public int Id;
        public int ParentId;
        public bool IsSuite;
        public string Name;
        public bool Skipped;
        public bool Failure;
        public double Duration;
        public List<MessageAndStack> Failures;
        public List<SuiteOrTest> Nested;
        public List<MessageAndStack> Logs;
    }

    public class TestResultsHolder : SuiteOrTest
    {
        public string UserAgent;
        public bool Running;
        public int TestsFailed;
        public int TestsSkipped;
        public int TestsFinished;
        public int TotalTests;
    }


    internal class TestServerConnectionHandler : ILongPollingConnectionHandler
    {
        ILongPollingConnection _connection;
        TestServer _testServer;
        bool _idle;
        string _userAgent;
        string _url;
        private int _runid;
        private TestResultsHolder _curResults;
        private int _suiteId;
        private Stack<SuiteOrTest> _suiteStack;
        private TestResultsHolder _oldResults;
        object _lock = new object();

        public TestServerConnectionHandler(TestServer testServer)
        {
            _testServer = testServer;
            _idle = true;
        }

        public void OnConnect(ILongPollingConnection connection)
        {
            _testServer.Clients.TryAdd(this, this);
            _url = _testServer.Url;
            _connection = connection;
        }

        public void OnClose(ILongPollingConnection connection)
        {
            _testServer.Clients.TryRemove(this, out var _);
            _testServer.NotifySomeChange();
        }

        public void OnMessage(ILongPollingConnection connection, string message, dynamic data)
        {
            switch (message)
            {
                case "newClient":
                    {
                        var client = UAParser.Parser.GetDefault().Parse((string)data.userAgent);
                        lock (_lock)
                        {
                            _userAgent = client.ToString();
                            if (_url != null)
                            {
                                DoStart();
                            }
                            else
                            {
                                _connection.Send("wait", null);
                            }
                        }
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "wholeStart":
                    {
                        lock (_lock)
                        {
                            if (_curResults == null) break;
                            _curResults.TotalTests = (int)data;
                            _suiteId = 0;
                            _suiteStack = new Stack<SuiteOrTest>();
                            _suiteStack.Push(_curResults);
                        }
                        _testServer.NotifyTestingStarted();
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "wholeDone":
                    {
                        lock (_lock)
                        {
                            if (_curResults == null) break;
                            if (_suiteStack == null) break;
                            _curResults.Duration = (double)data;
                            _curResults.Running = false;
                            _oldResults = _curResults;
                            _curResults = null;
                            _suiteStack = null;
                        }
                        _testServer.NotifyFinishedResults(_oldResults);
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "suiteStart":
                    {
                        lock(_lock)
                        {
                            if (_curResults == null) break;
                            if (_suiteStack == null) break;
                            var suite = new SuiteOrTest
                            {
                                Id = ++_suiteId,
                                ParentId = _suiteStack.Peek().Id,
                                Name = (string)data,
                                Nested = new List<SuiteOrTest>(),
                                Duration = 0,
                                Failure = false,
                                IsSuite = true,
                                Failures = new List<MessageAndStack>(),
                                Skipped = false,
                                Logs = new List<MessageAndStack>()
                            };
                            _suiteStack.Peek().Nested.Add(suite);
                            _suiteStack.Push(suite);
                        }
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "suiteDone":
                    {
                        lock (_lock)
                        {
                            if (_curResults == null) break;
                            if (_suiteStack == null) break;
                            var suite = _suiteStack.Pop();
                            suite.Duration = data.duration;
                            suite.Failures.AddRange(ConvertFailures(data.failures));
                            if (suite.Failures.Count > 0)
                            {
                                suite.Failure = true;
                                foreach (var s in _suiteStack)
                                {
                                    s.Failure = true;
                                }
                            }
                        }
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "testStart":
                    {
                        lock (_lock)
                        {
                            if (_curResults == null) break;
                            if (_suiteStack == null) break;
                            var test = new SuiteOrTest
                            {
                                Id = ++_suiteId,
                                ParentId = _suiteStack.Peek().Id,
                                Name = (string)data,
                                Nested = null,
                                Duration = 0,
                                Failure = false,
                                IsSuite = false,
                                Failures = new List<MessageAndStack>(),
                                Skipped = false,
                                Logs = new List<MessageAndStack>()
                            };
                            _suiteStack.Peek().Nested.Add(test);
                            _suiteStack.Push(test);
                        }
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "testDone":
                    {
                        lock (_lock)
                        {
                            if (_curResults == null) break;
                            if (_suiteStack == null) break;
                            var test = _suiteStack.Pop();
                            test.Duration = data.duration;
                            test.Failures.AddRange(ConvertFailures(data.failures));
                            _curResults.TestsFinished++;
                            if (data.status == "passed")
                            {
                            }
                            else if (data.status == "skipped" || data.status == "pending" || data.status == "disabled")
                            {
                                _curResults.TestsSkipped++;
                                test.Skipped = true;
                            }
                            else
                            {
                                _curResults.TestsFailed++;
                                test.Failure = true;
                                foreach (var s in _suiteStack)
                                {
                                    s.Failure = true;
                                }
                            }
                        }
                        _testServer.NotifySomeChange();
                        break;
                    }
                case "consoleLog":
                    {
                        lock (_lock)
                        {
                            if (_curResults == null) break;
                            if (_suiteStack == null) break;
                            var test = _suiteStack.Peek();
                            test.Logs.Add(ConvertMessageAndStack((string)data.message, (string)data.stack));
                        }
                        _testServer.NotifySomeChange();
                        break;
                    }
            }
        }

        MessageAndStack ConvertMessageAndStack(string message, string rawStack)
        {
            var stack = StackFrame.Parse(rawStack);
            // TODO: resolve SourceMap
            return new MessageAndStack
            {
                Message = message,
                Stack = stack
            };
        }

        IEnumerable<MessageAndStack> ConvertFailures(dynamic failures)
        {
            foreach (var messageAndStack in failures)
            {
                var message = (string)messageAndStack.message;
                var rawStack = (string)messageAndStack.stack;
                yield return ConvertMessageAndStack(message, rawStack);
            }
        }

        internal void StartTest(string url, int runid)
        {
            _url = url;
            _runid = runid;
            DoStart();
        }

        void DoStart()
        {
            _idle = false;
            InitCurResults();
            _connection.Send("test", new { url = _url });
        }

        void InitCurResults()
        {
            _curResults = new TestResultsHolder
            {
                UserAgent = _userAgent,
                Nested = new List<SuiteOrTest>(),
                Duration = 0,
                Running = true,
                TotalTests = -1,
                TestsFinished = 0,
                TestsFailed = 0,
                TestsSkipped = 0,
                Id = 0,
                ParentId = 0,
                Name = "",
                Failure = false,
                Skipped = false,
                Failures = new List<MessageAndStack>(),
                IsSuite = true,
                Logs = new List<MessageAndStack>()
            };
        }

    }

    internal class TestServer
    {
        public readonly ConcurrentDictionary<TestServerConnectionHandler, TestServerConnectionHandler> Clients = new ConcurrentDictionary<TestServerConnectionHandler, TestServerConnectionHandler>();
        int _runid;
        public Subject<Unit> OnChange = new Subject<Unit>();
        public Subject<Unit> OnTestingStarted = new Subject<Unit>();
        public Subject<TestResultsHolder> OnTestResults = new Subject<TestResultsHolder>();
        internal Subject<Unit> OnChangeRaw = new Subject<Unit>();

        public TestServer()
        {
            OnChangeRaw.Throttle(TimeSpan.FromMilliseconds(500)).Subscribe(OnChange);
        }

        public string Url { get; internal set; }

        public ILongPollingConnectionHandler NewConnectionHandler()
        {
            return new TestServerConnectionHandler(this);
        }

        public void StartTest(string url)
        {
            _runid++;
            Url = url;
            foreach (var client in Clients.Keys)
            {
                client.StartTest(url, _runid);
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
    }
}
