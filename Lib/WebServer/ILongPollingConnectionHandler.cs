using Newtonsoft.Json.Linq;

namespace Lib.WebServer
{
    public interface ILongPollingConnectionHandler
    {
        void OnConnect(ILongPollingConnection connection);
        void OnMessage(ILongPollingConnection connection, string message, JToken data);
        void OnClose(ILongPollingConnection connection);
    }
}
