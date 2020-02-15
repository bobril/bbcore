using Lib.WebServer;
using System.Collections.Generic;
using System;
using Lib.Utils;
using Newtonsoft.Json.Linq;

namespace Lib.Composition
{
    class MainServerConnectionHandler : ILongPollingConnectionHandler
    {
        ILongPollingConnection _connection;
        MainServer _mainServer;

        public MainServerConnectionHandler(MainServer mainServer)
        {
            _mainServer = mainServer;
        }

        public void OnConnect(ILongPollingConnection connection)
        {
            _mainServer.Clients.TryAdd(this, this);
            _connection = connection;
            _connection.Send("testUpdated", _mainServer.TestServerStateGetter());
            // _connection.Send("actionsRefresh", actions.actionList.getList());
            _connection.Send("setLiveReload",
                new Dictionary<string, object> {{"value", _mainServer.Project.LiveReloadEnabled}});
            _connection.Send("setCoverage",
                new Dictionary<string, object> {{"value", _mainServer.Project.CoverageEnabled}});
        }

        public void OnClose(ILongPollingConnection connection)
        {
            _mainServer.Clients.TryRemove(this, out var _);
        }

        internal void Send(string message, object data)
        {
            _connection.Send(message, data);
        }

        public void OnMessage(ILongPollingConnection connection, string message, JToken data)
        {
            switch (message)
            {
                case "focusPlace":
                {
                    var position = new Dictionary<string, object>
                    {
                        {"fn", PathUtils.RealPath(PathUtils.Join(_mainServer.ProjectDir, data.Value<string>("fn")))},
                        {"pos", data.Value<JArray>("pos")}
                    };
                    _mainServer.SendToAll(message, position);
                    break;
                }
                /*case "runAction":
                    {
                        actions.actionList.invokeAction(data.id);
                        break;
                    }*/
                case "setLiveReload":
                {
                    _mainServer.Project.LiveReloadEnabled = data.Value<bool>("value");
                    // TODO: force recompile
                    _mainServer.SendToAll("setLiveReload", data);
                    break;
                }
                case "setCoverage":
                {
                    _mainServer.Project.CoverageEnabled = data.Value<bool>("value");
                    // TODO: force recompile
                    _mainServer.SendToAll("setCoverage", data);
                    break;
                }
                default:
                {
                    Console.WriteLine("Main Message " + message + " " + data);
                    break;
                }
            }
        }
    }
}
