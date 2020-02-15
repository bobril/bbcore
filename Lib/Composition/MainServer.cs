using Lib.WebServer;
using System.Collections.Concurrent;
using Lib.TSCompiler;
using System;
using System.Collections.Generic;

namespace Lib.Composition
{
    class MainServer
    {
        public readonly ConcurrentDictionary<MainServerConnectionHandler, MainServerConnectionHandler> Clients = new ConcurrentDictionary<MainServerConnectionHandler, MainServerConnectionHandler>();

        public MainServer(Func<TestServerState> testServerStateGetter)
        {
            _testServerStateGetter = testServerStateGetter;
        }

        ProjectOptions _project;
        readonly Func<TestServerState> _testServerStateGetter;

        public ProjectOptions Project
        {
            get => _project; internal set => _project = value;
        }

        public MainBuildResult MainBuildResult { get; set; }

        public string ProjectDir { get => MainBuildResult.CommonSourceDirectory; }

        public Func<TestServerState> TestServerStateGetter => _testServerStateGetter;

        public ILongPollingConnectionHandler NewConnectionHandler()
        {
            return new MainServerConnectionHandler(this);
        }

        public void SendToAll(string message, object data)
        {
            foreach (var client in Clients.Keys)
            {
                client.Send(message, data);
            }
        }

        /*public void NotifyActionsChanged()
        {
            this.SendToAll("actionsRefresh", actions.actionList.getList());
        }*/

        public void NotifyCompilationStarted()
        {
            SendToAll("compilationStarted", null);
        }

        public void NotifyCompilationFinished(int errors, int warnings, double time, IList<Diagnostic> messages)
        {
            SendToAll("compilationFinished", new Dictionary<string, object> {
                { "errors", errors },
                { "warnings", warnings },
                { "time", (int)(time*1000) },
                { "messages", messages }
            });
        }

        public void NotifyTestServerChange()
        {
            if (Clients.IsEmpty)
                return;
            SendToAll("testUpdated", _testServerStateGetter());
        }

        public void NotifyCoverageChange()
        {
            if (Clients.IsEmpty)
                return;
            SendToAll("coverageUpdated", null);
        }
    }
}
