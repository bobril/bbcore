using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lib.WebServer
{
    public class LongPollingServer : ILongPollingServer
    {
        class Connection : ILongPollingConnection
        {
            readonly ILongPollingConnectionHandler _handler;
            public string UserAgent { get; set; }
            public string Id => _id;

            LongPollingServer _owner;
            readonly string _id;
            int _closed; // 0/1 = false/true - Interlocked.Exchange is not for bools :-(
            TaskCompletionSource<Unit> _responseEnder;
            HttpContext _response;
            Timer _timeOut;
            readonly List<(string, object)> _toSend;

            internal Connection(LongPollingServer owner, ILongPollingConnectionHandler handler)
            {
                _owner = owner;
                _handler = handler;
                _id = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 22);
                _closed = 0;
                _toSend = new List<(string, object)>();
                UserAgent = "";
                _timeOut = new Timer(HandleTimeOut, null, 15000, Timeout.Infinite);
            }

            void HandleTimeOut(object state)
            {
                if (_response == null)
                    Close();
            }

            void Retimeout()
            {
                _timeOut?.Change(15000, Timeout.Infinite);
            }

            public void Send(string message, object data)
            {
                HttpContext response;
                lock (_toSend)
                {
                    _toSend.Add((message, data));
                    response = _response;
                }
                if (response != null)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    PollResponse(response, false, false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }

            public void Close()
            {
                if (_timeOut != null)
                {
                    _timeOut.Dispose();
                    _timeOut = null;
                }
                if (Interlocked.Exchange(ref _closed, 1) == 0)
                {
                    _handler.OnClose(this);
                }
                HttpContext response;
                TaskCompletionSource<Unit> ender;
                lock (_toSend)
                {
                    response = _response;
                    ender = _responseEnder;
                    _response = null;
                    _responseEnder = null;
                }
                if (response != null)
                {
                    response.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { { "id", _id }, { "close", true } })).ContinueWith((t) =>
                    {
                        ender.TrySetResult(Unit.Default);
                    });
                }
            }

            internal async Task PollResponse(HttpContext response, bool waitAllowed, bool firstResponse)
            {
                if (_closed == 1)
                {
                    await response.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { { "id", _id }, { "close", true } }));
                    return;
                }
                object toSend = null;
                var ender = _responseEnder;
                if (_response == response || firstResponse) lock (_toSend)
                    {
                        if (_response != response && !firstResponse)
                            return;
                        if (_toSend.Count > 0)
                        {
                            toSend = new Dictionary<string, object> { { "id", _id }, { "m", _toSend.Select(p => new { m = p.Item1, d = p.Item2 }).ToList() } };
                            _responseEnder = null;
                            _response = null;
                        }
                        _toSend.Clear();
                    }
                if (toSend != null)
                {
                    await response.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(toSend)).ContinueWith((t) =>
                    {
                        Retimeout();
                        if (ender != null) ender.TrySetResult(Unit.Default);
                    });
                    return;
                }
                if (waitAllowed)
                {
                    if (_response != null && _response != response)
                    {
                        var resp = _response.Response;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        resp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { { "id", _id }, { "old", true } }))
                            .ContinueWith((t) => ender.TrySetResult(Unit.Default));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    _responseEnder = new TaskCompletionSource<Unit>();
                    _response = response;
                    await _responseEnder.Task;
                }
                else
                {
                    if (_response == response)
                    {
                        _responseEnder.TrySetResult(Unit.Default);
                        _response = null;
                        Retimeout();
                    }
                    await response.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { { "id", _id } }));
                }
            }

            internal void CloseResponse(HttpContext response)
            {
                if (_response == response)
                {
                    TaskCompletionSource<Unit> ender;
                    lock (_toSend)
                    {
                        if (_response != response)
                            return;
                        ender = _responseEnder;
                        _response = null;
                        _responseEnder = null;
                    }
                    ender.TrySetResult(Unit.Default);
                    Retimeout();
                }
            }

            internal void ReceivedMessage(string message, JToken data)
            {
                _handler.OnMessage(this, message, data);
            }

            internal void ConnectionCreated()
            {
                _handler.OnConnect(this);
            }
        }

        readonly Func<ILongPollingConnectionHandler> _connectionHandlerFactory;
        readonly ConcurrentDictionary<string, Connection> _connections = new ConcurrentDictionary<string, Connection>();

        public LongPollingServer(Func<ILongPollingConnectionHandler> connectionHandlerFactory)
        {
            _connectionHandlerFactory = connectionHandlerFactory;
        }

        public async Task Handle(HttpContext context)
        {
            if (context.Request.Method != "POST")
            {
                context.Response.StatusCode = 405;
                await context.Response.WriteAsync("Only POST allowed");
                return;
            }
            var jsonString = await new StreamReader(context.Request.Body, Encoding.UTF8).ReadToEndAsync();
            JObject data;
            try
            {
                data = JObject.Parse(jsonString);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("JSON parse error " + ex.Message);
                return;
            }
            Connection c = null;
            if (!string.IsNullOrEmpty((string)data["id"]))
            {
                _connections.TryGetValue(data["id"].ToString(), out c);
                if (c == null)
                {
                    await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, object> { { "id", data["id"].ToString() }, { "close", true } }));
                    return;
                }
            }
            var waitAllowed = true;
            var firstResponse = false;
            if (c == null)
            {
                c = new Connection(this, _connectionHandlerFactory());
                _connections.TryAdd(c.Id, c);
                c.ConnectionCreated();
                waitAllowed = false;
                firstResponse = true;
            }
            if (context.Request.Headers.TryGetValue("user-agent", out var ua)) c.UserAgent = ua;
            if (data["close"] != null && (bool)data["close"])
            {
                c.Close();
                waitAllowed = false;
            }
            context.RequestAborted.Register(() =>
            {
                c.CloseResponse(context);
            });
            if (data["m"] is JArray)
            {
                waitAllowed = false;
                var ms = (JArray)data["m"];
                for (var i = 0; i < ms.Count; i++)
                {
                    var msi = ms[i] as JObject;
                    c.ReceivedMessage(msi["m"].ToString(), msi.Value<JToken>("d"));
                }
            }
            await c.PollResponse(context, waitAllowed, firstResponse);
        }
    }
}
