using Lib.WebServer;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Lib.Utils.Logger;

namespace Lib.Composition
{
    class TestServerConnectionHandler : ILongPollingConnectionHandler
    {
        ILongPollingConnection? _connection;
        TestServer _testServer;
        string? _specFilter;
        string? _userAgent;
        string? _url;
        int _runid;
        TestResultsHolder? _curResults;
        int _suiteId;
        Stack<SuiteOrTest>? _suiteStack;
        TestResultsHolder? _oldResults;
        readonly object _lock = new object();
        readonly bool _verbose;
        readonly ILogger _logger;
        uint[] _coverageData;

        public TestServerConnectionHandler(TestServer testServer)
        {
            _testServer = testServer;
            _verbose = testServer.Verbose;
            _logger = testServer.Logger;
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

        public void OnMessage(ILongPollingConnection connection, string message, JToken data)
        {
            try
            {
                var hashPos = message.IndexOf('#');
                var pureMessage = message;
                if (hashPos >= 0)
                {
                    if (int.TryParse(message.Substring(hashPos + 1), out var runid) && runid != _runid)
                    {
                        if (_logger.Verbose)
                            _logger.Info("Ignoring " + message + " because current runid is " + _runid);
                        return;
                    }
                    pureMessage = message.Substring(0, hashPos);
                }
                switch (pureMessage)
                {
                    case "newClient":
                        {
                            if (_verbose)
                                _logger.Info($"New Test Client: {data.Value<string>("userAgent")}");
                            var client = UAParser.Parser.GetDefault().Parse(data.Value<string>("userAgent"));
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
                            if (_verbose) _logger.Info($"wholeStart tests:{(int)data}");
                            lock (_lock)
                            {
                                if (_curResults == null)
                                    break;
                                _curResults.TotalTests = (int)data;
                                _suiteId = 0;
                                if (_suiteStack == null)
                                {
                                    _suiteStack = new Stack<SuiteOrTest>();
                                    _suiteStack.Push(_curResults);
                                }
                            }

                            _testServer.NotifyTestingStarted();
                            _testServer.NotifySomeChange();
                            break;
                        }

                    case "wholeDone":
                        {
                            if (_verbose) _logger.Info($"wholeDone duration:{((double)data):f2}");
                            lock (_lock)
                            {
                                if (_curResults == null)
                                    break;
                                if (_suiteStack == null)
                                    break;
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
                            if (_verbose) _logger.Info($"suiteStart {(string)data}");
                            lock (_lock)
                            {
                                if (_curResults == null)
                                    break;
                                if (_suiteStack == null)
                                    break;
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
                                if (_curResults == null)
                                    break;
                                if (_suiteStack == null)
                                    break;
                                var suite = _suiteStack.Pop();
                                suite.Duration = data.Value<double>("duration");
                                if (_verbose)
                                    _logger.Info($"suiteDone {suite.Name} {suite.Duration:f2}");
                                suite.Failures.AddRange(ConvertFailures(data.Value<JArray>("failures")));
                                if (suite.Failures.Count > 0)
                                {
                                    _curResults.SuitesFailed += suite.Failures.Count;
                                    suite.Failure = true;
                                    _logger.Error(
                                        $"suite {suite.Name} in between test failures\n{string.Join('\n', suite.Failures.Select(f => f.Message + "\n  " + string.Join("\n  ", f.Stack)))}");
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
                            if (_verbose)
                                _logger.Info("testStart " + data.Value<string>("name"));
                            lock (_lock)
                            {
                                if (_curResults == null)
                                    break;
                                if (_suiteStack == null)
                                    break;
                                var test = new SuiteOrTest
                                {
                                    Id = ++_suiteId,
                                    ParentId = _suiteStack.Peek().Id,
                                    Name = data.Value<string>("name"),
                                    Stack = ConvertMessageAndStack("", data.Value<string>("stack")).Stack
                                        .Where(f => f.FileName != "bundle.js").ToList(),
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
                                if (_curResults == null)
                                    break;
                                if (_suiteStack == null)
                                    break;
                                var test = _suiteStack.Pop();
                                test.Duration = data.Value<double>("duration");
                                test.Failures.AddRange(ConvertFailures(data.Value<JArray>("failures")));
                                _curResults.TestsFinished++;
                                var status = data.Value<string>("status");
                                if (_verbose)
                                    _logger.Info("testDone " + test.Name + " " + status);
                                if (status == "passed")
                                {
                                }
                                else if (status == "skipped" || status == "pending" || status == "disabled" || status == "excluded")
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
                                if (_curResults == null)
                                    break;
                                if (_suiteStack == null)
                                    break;
                                var test = _suiteStack.Peek();
                                test.Logs.Add(ConvertMessageAndStack(data.Value<string>("message"),
                                    data.Value<string>("stack")));
                            }

                            _testServer.NotifySomeChange();
                            break;
                        }

                    case "onerror":
                    {
                        if (_verbose) _logger.Error("onerror " + data);
                        lock (_lock)
                        {
                            if (_curResults == null)
                                break;
                            _suiteId = 0;
                            _suiteStack = new Stack<SuiteOrTest>();
                            _suiteStack.Push(_curResults);
                            _curResults.Failures.Add(ConvertMessageAndStack(data.Value<string>("message"),data.Value<string>("stack")));
                            _logger.Error("Test onerror "+_curResults.Failures[^1].Message);
                            _logger.Error(string.Join("\n", _curResults.Failures[^1].Stack));
                            _curResults.Failure = true;
                            _curResults.SuitesFailed ++;
                        }

                        _testServer.NotifyTestingStarted();
                        _testServer.NotifySomeChange();
                        break;
                    }

                    case "coverageReportStarted":
                    {
                        _coverageData = new uint[data.Value<int>("length")];
                        break;
                    }

                    case "coverageReportPart":
                    {
                        var start = data.Value<int>("start");
                        var dataPart = data.Value<JArray>("data").Select(t=>t.Value<uint>()).ToList();
                        dataPart.CopyTo(_coverageData, start);
                        break;
                    }

                    case "coverageReportFinished":
                    {
                        _oldResults.CoverageData = _coverageData;
                        _testServer.OnCoverageResults.OnNext(_oldResults);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in TestServerConnectionHandler message: " + message);
                Console.WriteLine(ex.ToString());
            }
        }

        MessageAndStack ConvertMessageAndStack(string message, string rawStack)
        {
            if (rawStack == null)
            {
                return new MessageAndStack
                {
                    Message = message,
                    Stack = new List<StackFrame>()
                };
            }

            var stack = StackFrame.Parse(rawStack);
            foreach (var frame in stack)
            {
                if (frame.FileName == null)
                    continue;
                if (frame.FileName.StartsWith("http://") || frame.FileName.StartsWith("https://"))
                {
                    frame.FileName = frame.FileName.Substring(frame.FileName.IndexOf('/', 8) + 1);
                }

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

            stack = stack.Where(f =>
                f.FileName != null && f.FileName != "jasmine-core.js" && f.FileName != "jasmine-boot.js").ToList();
            return new MessageAndStack
            {
                Message = message,
                Stack = stack
            };
        }

        IEnumerable<MessageAndStack> ConvertFailures(JArray failures)
        {
            foreach (var messageAndStack in failures)
            {
                var message = messageAndStack.Value<string>("message");
                var rawStack = messageAndStack.Value<string>("stack");
                if (rawStack != null && message != null && rawStack.StartsWith(message))
                {
                    rawStack = rawStack.Substring(message.Length);
                }
                yield return ConvertMessageAndStack(message, rawStack);
            }
        }

        internal void StartTest(string url, int runid, string specFilter)
        {
            _url = url;
            _runid = runid;
            _specFilter = specFilter;
            DoStart();
        }

        void DoStart()
        {
            InitCurResults();
            _connection.Send("test", new { specFilter = _specFilter, url = _url+"#"+_runid });
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
