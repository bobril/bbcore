using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Lib.Utils.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lib.TSCompiler;

public sealed class TsrsApiException : Exception
{
    public TsrsApiException(string message) : base(message)
    {
    }
}

// Talks TypeScript 7 native API protocol to `tsrs --api --async` (JSON-RPC with Content-Length framing over stdio).
// One request at a time; the server serializes requests anyway, so parallelism comes from multiple clients.
public sealed class TsrsApiClient : IDisposable
{
    static readonly byte[] HeaderEnd = "\r\n\r\n"u8.ToArray();

    readonly Process _process;
    readonly Stream _input;
    readonly Stream _output;
    readonly ILogger _logger;
    readonly byte[] _headerBuffer = new byte[256];
    int _nextId;
    bool _disposed;

    public TsrsApiClient(string tsrsExecutable, string currentDirectory, ILogger logger)
    {
        _logger = logger;
        var start = new ProcessStartInfo(tsrsExecutable)
        {
            WorkingDirectory = currentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false
        };
        start.ArgumentList.Add("--api");
        start.ArgumentList.Add("--async");
        start.ArgumentList.Add("--cwd");
        start.ArgumentList.Add(currentDirectory);
        start.ArgumentList.Add("--useCaseSensitiveFileNames=" +
                               (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? "false" : "true"));
        _process = Process.Start(start)!;
        _process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null && _logger.Verbose)
                _logger.Info("tsrs api: " + args.Data);
        };
        _process.BeginErrorReadLine();
        _input = _process.StandardInput.BaseStream;
        _output = new BufferedStream(_process.StandardOutput.BaseStream, 65536);
        Request("initialize", null, TimeSpan.FromSeconds(30));
    }

    public bool IsAlive => !_disposed && !_process.HasExited;

    public JToken Request(string method, JToken? parameters, TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = ++_nextId;
        var request = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? JValue.CreateNull()
        };
        var body = Encoding.UTF8.GetBytes(request.ToString(Formatting.None));
        var header = Encoding.ASCII.GetBytes("Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) +
                                             "\r\n\r\n");
        // Killing the process unblocks a hanging read, so a stuck server can never hang the build
        using var watchdog = new Timer(_ => Kill(), null, timeout, Timeout.InfiniteTimeSpan);
        try
        {
            _input.Write(header);
            _input.Write(body);
            _input.Flush();
            while (true)
            {
                var message = ReadMessage();
                if (message["id"]?.Type != JTokenType.Integer || message["method"] != null ||
                    message["id"]!.Value<int>() != id)
                    continue;
                if (message["error"] is JObject error)
                    throw new TsrsApiException("tsrs " + method + " failed: " + error["message"]);
                return message["result"] ?? JValue.CreateNull();
            }
        }
        catch (IOException ex)
        {
            throw new TsrsApiException("tsrs api process exited during " + method + ": " + ex.Message);
        }
    }

    JObject ReadMessage()
    {
        var headerLength = 0;
        while (headerLength < 4 || !_headerBuffer.AsSpan(headerLength - 4, 4).SequenceEqual(HeaderEnd))
        {
            if (headerLength == _headerBuffer.Length)
                throw new IOException("tsrs api header too long");
            var b = _output.ReadByte();
            if (b < 0)
                throw new IOException("unexpected end of stream");
            _headerBuffer[headerLength++] = (byte)b;
        }

        var contentLength = -1;
        foreach (var line in Encoding.ASCII.GetString(_headerBuffer, 0, headerLength - 4).Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line.AsSpan("Content-Length:".Length).Trim(), CultureInfo.InvariantCulture);
        }

        if (contentLength < 0)
            throw new IOException("tsrs api message without Content-Length");
        var body = new byte[contentLength];
        _output.ReadExactly(body);
        return JObject.Parse(Encoding.UTF8.GetString(body));
    }

    void Kill()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(true);
        }
        catch
        {
            // ignored
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _input.Close();
            if (!_process.WaitForExit(1000))
                Kill();
        }
        catch
        {
            Kill();
        }
        finally
        {
            _process.Dispose();
        }
    }
}
