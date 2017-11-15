using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Linq;
using System.Net;
using System.Net.Sockets;

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
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    IAsyncResult result = socket.BeginConnect(IPAddress.Loopback, Port, null, null);
                    result.AsyncWaitHandle.WaitOne(100, true);
                    if (socket.Connected)
                    {
                        socket.EndConnect(result);
                        socket.Close();
                        throw new Exception($"Port {Port} already used");
                    }
                    else
                    {
                        socket.Close();
                    }
                    _webHost.Start();
                }
                catch (Exception)
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
                config.Limits.MaxRequestBodySize = int.MaxValue;
                config.ApplicationSchedulingMode = Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal.SchedulingMode.Inline;
                config.Listen(BindToAny ? IPAddress.IPv6Any : IPAddress.IPv6Loopback, port);
            })
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
