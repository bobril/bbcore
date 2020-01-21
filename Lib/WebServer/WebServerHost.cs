using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using ProxyKit;

namespace Lib.WebServer
{
    public class WebServerHost : IWebServerBuilder
    {
        IWebHost _webHost;

        public bool InDocker { get; set; }
        public RequestDelegate Handler { get; set; }
        public int Port { get; set; }
        public bool FallbackToRandomPort { get; set; }
        public bool BindToAny { get; set; }

        public void Start()
        {
            if (InDocker)
            {
                FallbackToRandomPort = false;
                if (Port == 0)
                    Port = 8080;
                BindToAny = true;
            }
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
                        _webHost = BuildWebHost(0);
                    }
                    else
                    {
                        socket.Close();
                    }
                    _webHost.Start();
                }
                catch (Exception)
                {
                    try
                    {
                        _webHost.Dispose();
                        _webHost = BuildWebHost(0);
                        _webHost.Start();
                    }
                    catch (Exception)
                    {
                        for (int i = 1; i < 20; i++)
                        {
                            try
                            {
                                if (_webHost != null)
                                {
                                    _webHost.Dispose();
                                    _webHost = null;
                                }
                                _webHost = BuildWebHost(Port + i);
                                _webHost.Start();
                                break;
                            }
                            catch (Exception)
                            {
                                _webHost.Dispose();
                                _webHost = null;
                            }
                        }
                    }
                }
            }
            else
            {
                _webHost.Start();
            }
            if (_webHost == null)
                throw new Exception("Cannot find empty port to be allowed to listen on. Started on " + Port);
            Port = new Uri(_webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.First()).Port;
        }

        IWebHost BuildWebHost(int port)
        {
            return new WebHostBuilder().UseKestrel(config =>
                {
                    config.AddServerHeader = false;
                    config.Limits.MaxRequestBodySize = int.MaxValue;
                    config.Listen(BindToAny ? IPAddress.Any : IPAddress.Loopback, port);
                    if (!InDocker)
                        config.Listen(BindToAny ? IPAddress.IPv6Any : IPAddress.IPv6Loopback, port);
                })
                .ConfigureServices(s => { s.AddProxy(); })
                .Configure(a =>
                {
                    a.UseWebSockets();
                    a.Run(Handler);
                })
            .Build();
        }

        public void Stop()
        {
            _webHost.StopAsync(TimeSpan.FromSeconds(1)).Wait();
            _webHost.Dispose();
        }
    }
}
