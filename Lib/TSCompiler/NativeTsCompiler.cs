using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
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

public enum NativeTypeScriptKind
{
    Tsgo,
    Tsrs,
    // TypeScript 7.1+ tsc
    Tsc
}

public sealed partial class NativeTsCompiler : ITSCompiler
{
    public const string GlobalTsgoExecutable = "tsgo";

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex AnsiEscapeRegex { get; }

    [GeneratedRegex(
        @"^(?<file>.+)\((?<line>\d+),(?<col>\d+)\): (?<severity>error|warning) TS(?<code>\d+): (?<text>.*)$")]
    private static partial Regex FileDiagnosticRegex { get; }

    [GeneratedRegex(@"^(?<severity>error|warning) TS(?<code>\d+): (?<text>.*)$")]
    private static partial Regex GlobalDiagnosticRegex { get; }

    [GeneratedRegex(@"Found \d+ errors?\. Watching for file changes\.")]
    private static partial Regex WatchCompleteRegex { get; }

    readonly string _nativePreviewDirectory;
    readonly NativeTypeScriptKind _kind;
    readonly ILogger _logger;
    readonly object _lock = new();
    readonly List<Diagnostic> _watchDiagnostics = new();
    Diagnostic[] _diagnostics = [];
    Process? _watchProcess;
    TaskCompletionSource<int>? _watchCompletion;
    int _watchGeneration;
    bool _watchCompiling;
    bool _watchExited;
    string? _lastWatchOutput;
    string _currentDirectory = "";
    string _typeCheckProjectPath = "tsconfig.json";
    string[] _mainFiles = [];

    public NativeTsCompiler(string nativePreviewDirectory, ILogger logger,
        NativeTypeScriptKind kind = NativeTypeScriptKind.Tsgo)
    {
        _nativePreviewDirectory = nativePreviewDirectory;
        _kind = kind;
        _logger = logger;
    }

    public static string GetDisplayName(NativeTypeScriptKind kind) => kind switch
    {
        NativeTypeScriptKind.Tsrs => "Rust TypeScript (tsrs)",
        NativeTypeScriptKind.Tsc => "TypeScript 7 (tsc)",
        _ => "Go TypeScript"
    };

    public static string GetTypeCheckProjectFileName(NativeTypeScriptKind kind) => kind switch
    {
        NativeTypeScriptKind.Tsrs => ".bbcore-tsrs-typecheck.json",
        NativeTypeScriptKind.Tsc => ".bbcore-tsc-typecheck.json",
        _ => ".bbcore-tsgo-typecheck.json"
    };

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
        WriteTypeCheckProject(_mainFiles);
        bool exited;
        lock (_lock)
            exited = _watchExited;
        if (exited)
        {
            // Watch process died (crash, unsupported platform), start it again to report current state
            StopWatchProcess();
            StartWatchProcess();
            WaitForWatchCompilation(0, true);
            return;
        }

        WaitForWatchCompilation(_watchGeneration, false);
    }

    public void ClearDiagnostics()
    {
        lock (_lock)
        {
            _diagnostics = [];
            _watchDiagnostics.Clear();
        }
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
        // Read stderr concurrently, so a full stderr pipe cannot block the compiler while stdout is read
        var errorTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        var error = errorTask.GetAwaiter().GetResult();
        process.WaitForExit();
        if (error.Length > 0 && _logger.Verbose)
            _logger.Info(error.TrimEnd());
        return output + error;
    }

    ProcessStartInfo CreateStartInfo()
    {
        if (_kind != NativeTypeScriptKind.Tsgo || IsTsgoExecutablePath(_nativePreviewDirectory))
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
            _watchExited = false;
            _lastWatchOutput = null;
            _watchCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        var process = Process.Start(start)!;
        lock (_lock)
            _watchProcess = process;
        RegisterWatchProcess(process);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => HandleWatchProcessExit(process);
        process.OutputDataReceived += (_, args) => HandleWatchLine(args.Data);
        process.ErrorDataReceived += (_, args) => HandleWatchLine(args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    // Watch process should run until StopWatchProcess, exit before that is reported as error, otherwise typecheck
    // would silently report no errors (like tsrs --watch on Windows, which exits with "not supported")
    void HandleWatchProcessExit(Process process)
    {
        int exitCode;
        try
        {
            // Waits for redirected output to be fully read
            process.WaitForExit();
            exitCode = process.ExitCode;
        }
        catch
        {
            exitCode = -1;
        }

        lock (_lock)
        {
            if (ReferenceEquals(process, _watchProcess))
            {
                _watchExited = true;
                var text = GetDisplayName(_kind) + " watch process exited unexpectedly with exit code " +
                           exitCode.ToString(CultureInfo.InvariantCulture) +
                           (_lastWatchOutput != null ? ": " + _lastWatchOutput : "");
                _diagnostics = [.. _watchDiagnostics, new Diagnostic { IsError = true, IsSemantic = true, Text = text }];
                _watchDiagnostics.Clear();
                _watchCompiling = false;
                _watchGeneration++;
            }

            _watchCompletion?.TrySetResult(_watchGeneration);
        }
    }

    // tsrs --watch keeps running when bb exits, so kill watch processes on exit and on termination signals
    static readonly HashSet<Process> RunningWatchProcesses = new();
    static List<PosixSignalRegistration>? _signalRegistrations;

    static void RegisterWatchProcess(Process process)
    {
        lock (RunningWatchProcesses)
        {
            RunningWatchProcesses.Add(process);
            if (_signalRegistrations != null) return;
            _signalRegistrations = new();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => KillRunningWatchProcesses();
            foreach (var signal in new[] { PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP })
            {
                try
                {
                    _signalRegistrations.Add(PosixSignalRegistration.Create(signal, _ => KillRunningWatchProcesses()));
                }
                catch (PlatformNotSupportedException)
                {
                    // ignored
                }
            }
        }
    }

    static void UnregisterWatchProcess(Process process)
    {
        lock (RunningWatchProcesses)
            RunningWatchProcesses.Remove(process);
    }

    static void KillRunningWatchProcesses()
    {
        Process[] processes;
        lock (RunningWatchProcesses)
            processes = [.. RunningWatchProcesses];
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch
            {
                // ignored
            }
        }
    }

    void StopWatchProcess()
    {
        Process? process;
        lock (_lock)
        {
            process = _watchProcess;
            // Cleared before kill, so exit handler knows this exit is expected
            _watchProcess = null;
        }

        if (process == null)
            return;
        UnregisterWatchProcess(process);
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    void WriteTypeCheckProject(string[] mainFiles)
    {
        _mainFiles = mainFiles;
        var files = new List<string>(mainFiles.Length);
        foreach (var file in mainFiles)
        {
            // TypeScript 7.1 (tsrs) stops checking whole program on missing root file (TS6053), TypeScript 7.0 ignored it
            var fullPath = Path.IsPathRooted(file) ? file : PathUtils.Join(_currentDirectory, file);
            if (!File.Exists(fullPath))
            {
                if (_logger.Verbose)
                    _logger.Info("Typecheck skips missing file " + fullPath);
                continue;
            }

            files.Add(Path.IsPathRooted(file) ? PathUtils.Subtract(file, _currentDirectory) : file);
        }
        var project = new NativeTsTypeCheckProject
        {
            extends = "./tsconfig.json",
            files = files,
            include = []
        };
        _typeCheckProjectPath = GetTypeCheckProjectFileName(_kind);
        File.WriteAllText(PathUtils.Join(_currentDirectory, _typeCheckProjectPath),
            JsonConvert.SerializeObject(project, Formatting.Indented));
    }

    sealed class NativeTsTypeCheckProject
    {
        public string extends = "";
        public List<string> files = new();
        public string[] include = [];
    }

    void WaitForWatchCompilation(int previousGeneration, bool initial)
    {
        TaskCompletionSource<int>? completion = null;
        var waitUntil = DateTime.UtcNow + (initial ? TimeSpan.Zero : TimeSpan.FromSeconds(5));
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
                _lastWatchOutput = null;
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
            lock (_lock)
            {
                _diagnostics = _watchDiagnostics.ToArray();
                _watchDiagnostics.Clear();
                _watchCompiling = false;
                _watchGeneration++;
                _watchCompletion?.TrySetResult(_watchGeneration);
                _watchCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return;
        }

        // Remember error message (tsrs prints "error: ..."), otherwise last line, to report unexpected exit
        lock (_lock)
            if (_lastWatchOutput == null || !_lastWatchOutput.StartsWith("error", StringComparison.OrdinalIgnoreCase))
                _lastWatchOutput = line;
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

    void LogTypeCheckResult(IReadOnlyCollection<Diagnostic> diagnostics, TimeSpan duration)
    {
        var errors = 0;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.IsError)
                errors++;
        }

        _logger.Info(
            $"{GetDisplayName(_kind)} typecheck found {errors.ToString(CultureInfo.InvariantCulture)} errors in {duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)}s");
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
