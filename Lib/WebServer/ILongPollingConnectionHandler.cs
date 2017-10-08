namespace Lib.WebServer
{
    public interface ILongPollingConnectionHandler
    {
        void OnConnect(ILongPollingConnection connection);
        void OnMessage(ILongPollingConnection connection, string message, object data);
        void OnClose(ILongPollingConnection connection);
    }
}
