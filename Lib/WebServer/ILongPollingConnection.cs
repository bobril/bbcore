namespace Lib.WebServer
{
    public interface ILongPollingConnection
    {
        string UserAgent { get; }
        void Send(string message, object data);
        void Close();
    }
}
