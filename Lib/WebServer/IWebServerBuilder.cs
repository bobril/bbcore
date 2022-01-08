using Microsoft.AspNetCore.Http;

namespace Lib.WebServer;

interface IWebServerBuilder
{
    RequestDelegate Handler { get; set; }
    int Port { get; set; }
    bool FallbackToRandomPort { get; set; }
    bool BindToAny { get; set; }
    void Start();
    void Stop();
}