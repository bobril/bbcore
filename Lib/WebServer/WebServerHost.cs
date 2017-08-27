using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Linq;

namespace Lib.WebServer
{
    public class WebServerHost : IWebServerBuilder
    {
        IWebHost _webHost;

        public RequestDelegate Handler { get; set; }
        public int Port { get; set; }
        public bool FallbackToRandomPort { get; set; }
        public bool BindToAny { get; set; }

        public void Start()
        {
            _webHost = BuildWebHost(Port);
            if (Port != 0 && FallbackToRandomPort)
            {
                try
                {
                    _webHost.Start();
                }
                catch
                {
                    _webHost.Dispose();
                    _webHost = BuildWebHost(0);
                    _webHost.Start();
                }
            }
            else
            {
                _webHost.Start();
            }
            Port = new Uri(_webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First()).Port;
        }

        IWebHost BuildWebHost(int port)
        {
            return new WebHostBuilder().UseKestrel(config =>
            {
                config.AddServerHeader = false;
            })
            .UseUrls((BindToAny ? "http://*:" : "http://127.0.0.1:") + port)
            .PreferHostingUrls(true)
            .Configure(a => a.Run(Handler))
            .Build();
        }

        public void Stop()
        {
            _webHost.StopAsync(TimeSpan.FromSeconds(1)).Wait();
            _webHost.Dispose();
        }
    }
}
