using Lib.WebServer;

namespace Lib.Composition
{
    internal class TestServerConnectionHandler : ILongPollingConnectionHandler
    {
        ILongPollingConnection _connection;

        public void OnConnect(ILongPollingConnection connection)
        {
            _connection = connection;
        }

        public void OnClose(ILongPollingConnection connection)
        {
        }

        public void OnMessage(ILongPollingConnection connection, string message, object data)
        {
        }
    }
}