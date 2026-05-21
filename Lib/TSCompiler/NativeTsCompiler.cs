using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Lib.DiskCache;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json;

namespace Lib.TSCompiler;

public sealed class NativeTsCompiler : ITSCompiler
{
    public const string GlobalTsgoExecutable = "tsgo";

    static readonly Regex AnsiEscapeRegex = new(
        @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
        RegexOptions.Compiled);

    static readonly Regex FileDiagnosticRegex = new(
        @"^(?<file>.+)\((?<line>\d+),(?<col>\d+)\): (?<severity>error|warning) TS(?<code>\d+): (?<text>.*)$",
        RegexOptions.Compiled);

    static readonly Regex GlobalDiagnosticRegex = new(
        @"^(?<severity>error|warning) TS(?<code>\d+): (?<text>.*)$",
        RegexOptions.Compiled);

    static readonly Regex WatchCompleteRegex = new(
        @"Found \d+ errors?\. Watching for file changes\.",
        RegexOptions.Compiled);

    readonly string _nativePreviewDirectory;
    readonly ILogger _logger;
    readonly object _lock = new();
    readonly List<Diagnostic> _watchDiagnostics = new();
    Diagnostic[] _diagnostics = Array.Empty<Diagnostic>();
    Process? _watchProcess;
    TaskCompletionSource<int>? _watchCompletion;
    int _watchGeneration;
    bool _watchCompiling;
    string _currentDirectory = "";
    string _typeCheckProjectPath = "tsconfig.json";

    public NativeTsCompiler(string nativePreviewDirectory, ILogger logger)
    {
        _nativePreviewDirectory = nativePreviewDirectory;
        _logger = logger;
    }

    public IDiskCache DiskCache { get; set; } = null!;
    public ITSCompilerOptions CompilerOptions { get; set; } = null!;

    public string GetTSVersion()
    {
        var version = RunTypeScript("--version").Trim();
        return version.StartsWith("Version ", StringComparison.Ordinal) ? version["Version ".Length..] : version;
    }

    public TranspileResult Transpile(string fileName, string content)
    {
        throw new NotSupportedException("Native TypeScript compiler is used only for typechecking.");
    }

    public void CreateProgram(string currentDirectory, string[] mainFiles)
    {
        _currentDirectory = currentDirectory;
        WriteTypeCheckProject(mainFiles);
        StopWatchProcess();
        StartWatchProcess();
        WaitForWatchCompilation(0, true);
    }

    public void UpdateProgram(string[] mainFiles)
    {
        WriteTypeCheckProject(mainFiles);
    }

    public void TriggerUpdate()
    {
        WaitForWatchCompilation(_watchGeneration, false);
    }

    public void ClearDiagnostics()
    {
    }

    public Diagnostic[] GetDiagnostics()
    {
        lock (_lock)
            return (Diagnostic[])_diagnostics.Clone();
    }

    public void CheckProgram(string currentDirectory, string[] mainFiles)
    {
        _currentDirectory = currentDirectory;
        WriteTypeCheckProject(mainFiles);
        StopWatchProcess();
        var sw = Stopwatch.StartNew();
        var output = RunTypeScript("--project", _typeCheckProjectPath, "--noEmit", "--pretty", "false");
        sw.Stop();
        Diagnostic[] diagnostics;
        lock (_lock)
        {
            _diagnostics = ParseDiagnostics(output).ToArray();
            diagnostics = _diagnostics;
        }

        LogTypeCheckResult(diagnostics, sw.Elapsed);
    }

    string RunTypeScript(params string[] arguments)
    {
        var start = CreateStartInfo();
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (error.Length > 0 && _logger.Verbose)
            _logger.Info(error.TrimEnd());
        return output + error;
    }

    ProcessStartInfo CreateStartInfo()
    {
        if (IsTsgoExecutablePath(_nativePreviewDirectory))
        {
            return new ProcessStartInfo(_nativePreviewDirectory)
            {
                WorkingDirectory = _currentDirectory.Length == 0 ? Directory.GetCurrentDirectory() : _currentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false
            };
        }

        var tsgoPath = PathUtils.Join(_nativePreviewDirectory, "bin/tsgo.js");
        var start = File.Exists(tsgoPath)
            ? new ProcessStartInfo("node")
            : new ProcessStartInfo(PathUtils.Join(_nativePreviewDirectory, File.Exists(PathUtils.Join(_nativePreviewDirectory, "bin/tsgo")) ? "bin/tsgo" : "bin/tsgo.exe"));
        if (File.Exists(tsgoPath))
            start.ArgumentList.Add(tsgoPath);
        start.WorkingDirectory = _currentDirectory.Length == 0 ? Directory.GetCurrentDirectory() : _currentDirectory;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.StandardOutputEncoding = Encoding.UTF8;
        start.StandardErrorEncoding = Encoding.UTF8;
        start.UseShellExecute = false;
        return start;
    }

    void StartWatchProcess()
    {
        var start = CreateStartInfo();
        start.ArgumentList.Add("--watch");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(_typeCheckProjectPath);
        start.ArgumentList.Add("--noEmit");
        start.ArgumentList.Add("--pretty");
        start.ArgumentList.Add("false");

        lock (_lock)
        {
            _watchDiagnostics.Clear();
            _watchGeneration = 0;
            _watchCompiling = true;
            _watchCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _watchStart = Stopwatch.StartNew();
        }

        _watchProcess = Process.Start(start)!;
        _watchProcess.EnableRaisingEvents = true;
        _watchProcess.Exited += (_, _) =>
        {
            lock (_lock)
                _watchCompletion?.TrySetResult(_watchGeneration);
        };
        _watchProcess.OutputDataReceived += (_, args) => HandleWatchLine(args.Data);
        _watchProcess.ErrorDataReceived += (_, args) => HandleWatchLine(args.Data);
        _watchProcess.BeginOutputReadLine();
        _watchProcess.BeginErrorReadLine();
    }

    void StopWatchProcess()
    {
        if (_watchProcess == null)
            return;
        try
        {
            if (!_watchProcess.HasExited)
                _watchProcess.Kill(true);
        }
        catch
        {
        }
        finally
        {
            _watchProcess.Dispose();
            _watchProcess = null;
        }
    }

    void WriteTypeCheckProject(string[] mainFiles)
    {
        var files = new List<string>(mainFiles.Length);
        foreach (var file in mainFiles)
            files.Add(Path.IsPathRooted(file) ? PathUtils.Subtract(file, _currentDirectory) : file);
        var project = new NativeTsTypeCheckProject
        {
            extends = "./tsconfig.json",
            files = files,
            include = Array.Empty<string>()
        };
        _typeCheckProjectPath = ".bbcore-tsgo-typecheck.json";
        File.WriteAllText(PathUtils.Join(_currentDirectory, _typeCheckProjectPath),
            JsonConvert.SerializeObject(project, Formatting.Indented));
    }

    sealed class NativeTsTypeCheckProject
    {
        public string extends = "";
        public List<string> files = new();
        public string[] include = Array.Empty<string>();
    }

    void WaitForWatchCompilation(int previousGeneration, bool initial)
    {
        TaskCompletionSource<int>? completion = null;
        var waitUntil = DateTime.UtcNow + (initial ? TimeSpan.Zero : TimeSpan.FromMilliseconds(250));
        lock (_lock)
        {
            if (_watchGeneration > previousGeneration)
                return;
            if (_watchCompiling || initial)
                completion = _watchCompletion;
        }

        while (!initial && completion == null && DateTime.UtcNow < waitUntil)
        {
            Thread.Sleep(10);
            lock (_lock)
            {
                if (_watchGeneration > previousGeneration)
                    return;
                if (_watchCompiling)
                    completion = _watchCompletion;
            }
        }

        completion?.Task.Wait(TimeSpan.FromSeconds(30));
    }

    void HandleWatchLine(string? rawLine)
    {
        if (rawLine == null)
            return;
        var line = AnsiEscapeRegex.Replace(rawLine, "").TrimEnd();
        if (line.Length == 0)
            return;
        if (line.Contains("Starting compilation", StringComparison.Ordinal) ||
            line.Contains("Starting incremental compilation", StringComparison.Ordinal))
        {
            lock (_lock)
            {
                _watchDiagnostics.Clear();
                _watchCompiling = true;
                _watchStart = Stopwatch.StartNew();
            }
        }

        if (TryParseDiagnosticLine(line, _currentDirectory, out var diagnostic))
        {
            lock (_lock)
                _watchDiagnostics.Add(diagnostic);
            return;
        }

        if (WatchCompleteRegex.IsMatch(line))
        {
            Diagnostic[] diagnostics;
            TimeSpan duration;
            lock (_lock)
            {
                _diagnostics = _watchDiagnostics.ToArray();
                diagnostics = _diagnostics;
                duration = _watchStart?.Elapsed ?? TimeSpan.Zero;
                _watchDiagnostics.Clear();
                _watchCompiling = false;
                _watchGeneration++;
                _watchCompletion?.TrySetResult(_watchGeneration);
                _watchCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            LogTypeCheckResult(diagnostics, duration);
            return;
        }

        if (_logger.Verbose)
            _logger.Info(line);
    }

    List<Diagnostic> ParseDiagnostics(string output)
    {
        var diagnostics = new List<Diagnostic>();
        foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = AnsiEscapeRegex.Replace(rawLine, "").TrimEnd();
            if (line.Length == 0)
                continue;
            if (TryParseDiagnosticLine(line, _currentDirectory, out var diagnostic))
            {
                diagnostics.Add(diagnostic);
            }
            else if (_logger.Verbose)
            {
                _logger.Info(line);
            }
        }

        return diagnostics;
    }

    Stopwatch? _watchStart;

    void LogTypeCheckResult(IReadOnlyCollection<Diagnostic> diagnostics, TimeSpan duration)
    {
        var errors = 0;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.IsError)
                errors++;
        }

        _logger.Info(
            $"Go TypeScript typecheck found {errors.ToString(CultureInfo.InvariantCulture)} errors in {duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s");
    }

    internal static bool HasTsgoExecutable(string nativePreviewDirectory, IFsAbstraction fsAbstraction)
    {
        return fsAbstraction.FileExists(PathUtils.Join(nativePreviewDirectory, "bin/tsgo.js")) ||
               fsAbstraction.FileExists(PathUtils.Join(nativePreviewDirectory, "bin/tsgo")) ||
               fsAbstraction.FileExists(PathUtils.Join(nativePreviewDirectory, "bin/tsgo.exe"));
    }

    internal static bool TryFindGlobalTsgoExecutable(out string tsgoExecutable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                foreach (var candidate in TsgoExecutableNames())
                {
                    var fullPath = Path.Combine(dir, candidate);
                    if (File.Exists(fullPath))
                    {
                        tsgoExecutable = fullPath;
                        return true;
                    }
                }
            }
        }

        tsgoExecutable = "";
        return false;
    }

    static IEnumerable<string> TsgoExecutableNames()
    {
        yield return "tsgo";
        if (OperatingSystem.IsWindows())
        {
            yield return "tsgo.exe";
            yield return "tsgo.cmd";
            yield return "tsgo.bat";
        }
    }

    static bool IsTsgoExecutablePath(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName is "tsgo" or "tsgo.exe" or "tsgo.cmd" or "tsgo.bat";
    }

    internal static bool TryParseDiagnosticLine(string line, string currentDirectory, out Diagnostic diagnostic)
    {
        var match = FileDiagnosticRegex.Match(line);
        if (match.Success)
        {
            var fileName = PathUtils.Normalize(match.Groups["file"].Value);
            diagnostic = new Diagnostic
            {
                IsError = match.Groups["severity"].Value == "error",
                IsSemantic = true,
                Code = int.Parse(match.Groups["code"].Value, CultureInfo.InvariantCulture),
                Text = match.Groups["text"].Value,
                FileName = Path.IsPathRooted(fileName) ? PathUtils.Subtract(fileName, currentDirectory) : fileName,
                StartLine = int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture) - 1,
                StartCol = int.Parse(match.Groups["col"].Value, CultureInfo.InvariantCulture) - 1
            };
            diagnostic.EndLine = diagnostic.StartLine;
            diagnostic.EndCol = diagnostic.StartCol;
            return true;
        }

        match = GlobalDiagnosticRegex.Match(line);
        if (match.Success)
        {
            diagnostic = new Diagnostic
            {
                IsError = match.Groups["severity"].Value == "error",
                IsSemantic = true,
                Code = int.Parse(match.Groups["code"].Value, CultureInfo.InvariantCulture),
                Text = match.Groups["text"].Value
            };
            return true;
        }

        diagnostic = null!;
        return false;
    }

    public void Dispose()
    {
        StopWatchProcess();
    }
}
