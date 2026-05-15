using System.Text.Json.Nodes;

namespace Lib.WebServer;

public interface ILongPollingConnectionHandler
{
    void OnConnect(ILongPollingConnection connection);
    void OnMessage(ILongPollingConnection connection, string message, JsonNode data);
    void OnClose(ILongPollingConnection connection);
}