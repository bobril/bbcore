using System.Collections.Generic;
using Lib.Composition;
using Lib.Utils.CommandLineParser.Definitions;
using Lib.Utils.CommandLineParser.Parser;
using Lib.Utils.Logger;
using Lib.WebServer;
using Newtonsoft.Json.Linq;
using Njsast.SourceMap;
using Xunit;

namespace Lib.Test;

public class TestServerSpecFilterTests
{
    [Fact]
    public void TestCommandAcceptsFilePathAlongsideSpecFilter()
    {
        var command = Assert.IsType<TestCommand>(CommandLineParser.Parse(
            ["test", "--testFilePath", "spec/a test.spec.ts", "--filter", "^suite spec$"], [new TestCommand()]));

        Assert.Equal("spec/a test.spec.ts", command.TestFilePath.Value);
        Assert.Equal("^suite spec$", command.SpecFilter.Value);
    }

    const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/153.0.0.0 Safari/537.36";

    sealed class RecordingConnection : ILongPollingConnection
    {
        public string UserAgent => "";
        public readonly List<(string Message, JToken Data)> Sent = [];

        public void Send(string message, object data) => Sent.Add((message, data == null ? JValue.CreateNull() : JToken.FromObject(data)));

        public void Close()
        {
        }
    }

    static RecordingConnection ConnectNewClient(TestServer server)
    {
        var connection = new RecordingConnection();
        var handler = server.NewConnectionHandler();
        handler.OnConnect(connection);
        handler.OnMessage(connection, "newClient", new JObject { ["userAgent"] = UserAgent });
        return connection;
    }

    [Fact]
    public void ClientConnectingAfterTestStartedGetsItsSpecFilter()
    {
        var server = new TestServer(false, new DummyLogger());
        server.StartTest("/test.html", new Dictionary<string, SourceMap>(), "^suite spec$");

        var connection = ConnectNewClient(server);

        var (message, data) = Assert.Single(connection.Sent);
        Assert.Equal("test", message);
        Assert.Equal("^suite spec$", data.Value<string>("specFilter"));
        Assert.StartsWith("/test.html#", data.Value<string>("url"));
    }

    [Fact]
    public void ClientConnectingAfterTestStartedWithoutFilterGetsNoSpecFilter()
    {
        var server = new TestServer(false, new DummyLogger());
        server.StartTest("/test.html", new Dictionary<string, SourceMap>());

        var connection = ConnectNewClient(server);

        var (_, data) = Assert.Single(connection.Sent);
        Assert.Equal("", data.Value<string>("specFilter"));
    }
}
