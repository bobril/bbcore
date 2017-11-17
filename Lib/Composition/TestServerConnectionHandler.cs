using Lib.WebServer;
using System.Collections.Generic;

namespace Lib.Composition
{
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
                        lock (_lock)
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
            foreach (var frame in stack)
            {
                if (_testServer.SourceMaps.TryGetValue(frame.FileName, out var sm))
                {
                    var pos = sm.FindPosition(frame.LineNumber, frame.ColumnNumber);
                    if (pos.SourceName != null)
                    {
                        frame.FileName = pos.SourceName;
                        frame.LineNumber = pos.Line;
                        frame.ColumnNumber = pos.Col;
                    }
                }
            }
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
            _curResults = CreateEmptyResults();
        }

        TestResultsHolder CreateEmptyResults()
        {
            return new TestResultsHolder
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

        internal TestResultsHolder GetLatestResults()
        {
            lock (_lock)
            {
                var latestResults = _curResults ?? _oldResults;
                if (latestResults == null)
                    return CreateEmptyResults();
                return latestResults.Clone();
            }
        }
    }
}
