using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Lib.DiskCache;
using Lib.Utils.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lib.TSCompiler;

// Transpiles single modules with tsrs or TypeScript 7.1+ `transpileModule` API; used only for transpilation, typecheck goes through
// NativeTsCompiler.
public sealed class NativeApiTranspiler : ITSCompiler
{
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);
    static readonly ConcurrentDictionary<string, string> VersionCache = new();

    readonly ILogger _logger;
    NativeApiClient? _client;
    ITSCompilerOptions? _compilerOptions;
    JObject _apiCompilerOptions = new();

    public NativeApiTranspiler(string executable, ILogger logger)
    {
        Executable = executable;
        Name = GetName(executable);
        _logger = logger;
    }

    public string Executable { get; }

    // "tsrs" or "tsc"
    public string Name { get; }

    public static string GetName(string executable) =>
        System.IO.Path.GetFileName(executable).StartsWith("tsrs", StringComparison.OrdinalIgnoreCase) ? "tsrs" : "tsc";
    public IDiskCache DiskCache { get; set; } = null!;

    public ITSCompilerOptions CompilerOptions
    {
        get => _compilerOptions!;
        set
        {
            if (ReferenceEquals(_compilerOptions, value)) return;
            _compilerOptions = value;
            _apiCompilerOptions = ToApiCompilerOptions(value);
        }
    }

    public string GetTSVersion() => GetVersion(Executable);

    // `tsrs --version` prints "Version 7.1.0-dev.20260930.4 (tsrs 0.18.0-..., microsoft/TypeScript@...)",
    // `tsc --version` prints "Version 7.1.0-beta"
    public static string GetVersion(string executable)
    {
        return VersionCache.GetOrAdd(executable, static exe =>
        {
            var start = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false
            };
            start.ArgumentList.Add("--version");
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output.StartsWith("Version ", StringComparison.Ordinal) ? output["Version ".Length..] : output;
        });
    }

    public TranspileResult Transpile(string fileName, string content)
    {
        var parameters = new JObject
        {
            ["input"] = content,
            ["options"] = new JObject
            {
                ["compilerOptions"] = _apiCompilerOptions,
                ["fileName"] = fileName,
                ["reportDiagnostics"] = true
            }
        };
        JToken response;
        try
        {
            _client ??= new(Executable, Name, Directory.GetCurrentDirectory(), _logger);
            response = _client.Request("transpileModule", parameters, RequestTimeout);
        }
        catch (Exception ex) when (ex is NativeApiException or IOException or InvalidOperationException
                                       or System.ComponentModel.Win32Exception)
        {
            // Next request respawns the process
            _client?.Dispose();
            _client = null;
            return new()
            {
                JavaScript = "",
                Diagnostics =
                [
                    new() { IsError = true, Code = -17, Text = ex.Message, FileName = fileName }
                ]
            };
        }

        var result = ParseTranspileResponse(fileName, response);
        if (result.Diagnostics == null || !result.Diagnostics.Exists(d => d.IsError))
            (result.JavaScript, result.SourceMap) =
                NativeOutputPatcher.Patch(result.JavaScript, result.SourceMap, content);
        return result;
    }

    internal static TranspileResult ParseTranspileResponse(string fileName, JToken response)
    {
        var result = new TranspileResult
        {
            JavaScript = response["outputText"]?.Value<string>() ?? "",
            SourceMap = response["sourceMapText"]?.Value<string>()
        };
        if (response["diagnostics"] is JArray { Count: > 0 } diagnostics)
        {
            result.Diagnostics = new(diagnostics.Count);
            foreach (var diagnostic in diagnostics)
                result.Diagnostics.Add(ParseDiagnostic(fileName, diagnostic));
        }

        return result;
    }

    static Diagnostic ParseDiagnostic(string fileName, JToken diagnostic)
    {
        var text = new StringBuilder(diagnostic["text"]?.Value<string>() ?? "");
        AppendMessageChain(text, diagnostic["messageChain"], 1);
        var res = new Diagnostic
        {
            IsError = diagnostic["category"]?.Value<int>() == 1,
            Code = diagnostic["code"]?.Value<int>() ?? 0,
            Text = text.ToString()
        };
        if (diagnostic["startPosition"] is JObject startPosition)
        {
            res.FileName = fileName;
            res.StartLine = startPosition["line"]!.Value<int>();
            res.StartCol = startPosition["character"]!.Value<int>();
            var endPosition = diagnostic["endPosition"] as JObject ?? startPosition;
            res.EndLine = endPosition["line"]!.Value<int>();
            res.EndCol = endPosition["character"]!.Value<int>();
        }

        return res;
    }

    // Same layout as ts.flattenDiagnosticMessageText(messageText, "\n")
    static void AppendMessageChain(StringBuilder text, JToken? chain, int indent)
    {
        if (chain is not JArray items) return;
        foreach (var item in items)
        {
            text.Append('\n').Append(' ', indent * 2).Append(item["text"]?.Value<string>());
            AppendMessageChain(text, item["messageChain"], indent + 1);
        }
    }

    // TypeScript 7 API takes tsconfig option names with numeric enums, numbering differs from bbcore enums
    internal static JObject ToApiCompilerOptions(ITSCompilerOptions options)
    {
        var res = JObject.Parse(JsonConvert.SerializeObject(options, Formatting.None,
            TSCompilerOptions.GetSerializerSettings()));
        // Options removed in TypeScript 7 (reported as TS5102) or not affecting single file transpilation
        foreach (var name in new[] { "baseUrl", "lib", "types", "typeRoots", "paths", "project", "charset" })
            res.Remove(name);
        SetOrRemove(res, "target", options.target switch
        {
            null => null,
            ScriptTarget.Es3 or ScriptTarget.Es5 => (int)ScriptTarget.Es2015,
            var target => (int)target
        });
        SetOrRemove(res, "module", options.module switch
        {
            null => null,
            ModuleKind.Esnext => 99,
            ModuleKind.Node16 => 100,
            ModuleKind.Nodenext => 199,
            var module => (int)module
        });
        SetOrRemove(res, "moduleResolution", (int?)options.moduleResolution);
        SetOrRemove(res, "jsx", (int?)options.jsx);
        SetOrRemove(res, "newLine", options.newLine switch
        {
            null => null,
            NewLineKind.CarriageReturnLineFeed => 1,
            _ => 2
        });
        return res;
    }

    static void SetOrRemove(JObject obj, string name, int? value)
    {
        if (value == null)
            obj.Remove(name);
        else
            obj[name] = value.Value;
    }

    public void CreateProgram(string currentDirectory, string[] mainFiles) =>
        throw new NotSupportedException("Native TypeScript API transpiler is used only for transpilation.");

    public void UpdateProgram(string[] mainFiles) =>
        throw new NotSupportedException("Native TypeScript API transpiler is used only for transpilation.");

    public void TriggerUpdate() =>
        throw new NotSupportedException("Native TypeScript API transpiler is used only for transpilation.");

    public void ClearDiagnostics()
    {
    }

    public Diagnostic[] GetDiagnostics() => [];

    public void CheckProgram(string currentDirectory, string[] mainFiles) =>
        throw new NotSupportedException("Native TypeScript API transpiler is used only for transpilation.");

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }
}
