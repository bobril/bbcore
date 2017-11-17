using Lib.WebServer;
using System.Collections.Generic;
using System;
using Lib.Utils;

namespace Lib.Composition
{
    internal class MainServerConnectionHandler : ILongPollingConnectionHandler
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
            _connection.Send("setLiveReload", new Dictionary<string, object>{ { "value", _mainServer.Project.LiveReloadEnabled } });
        }

        public void OnClose(ILongPollingConnection connection)
        {
            _mainServer.Clients.TryRemove(this, out var _);
        }

        internal void Send(string message, object data)
        {
            _connection.Send(message, data);
        }

        public void OnMessage(ILongPollingConnection connection, string message, dynamic data)
        {
            switch (message)
            {
                case "focusPlace":
                    {
                        _mainServer.SendToAll(message, new Dictionary<string, object> { { "fn", PathUtils.Join(_mainServer.ProjectDir, (string)data.fn) }, { "pos", data.pos } });
                        break;
                    }
                /*case "runAction":
                    {
                        actions.actionList.invokeAction(data.id);
                        break;
                    }*/
                case "setLiveReload":
                    {
                        _mainServer.Project.LiveReloadEnabled = (bool)data.value;
                        // TODO: force recompile
                        _mainServer.SendToAll("setLiveReload", data);
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
