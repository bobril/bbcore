using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Lib.DiskCache;
using Lib.Utils;
using Njsast;
using Shared.DiskCache;
using Shared.Utils;

namespace Lib.HeadlessBrowser;

public interface IBrowserProcess : IDisposable
{
}

public interface IBrowserProcessFactory
{
    IBrowserProcess Create(string urlToOpen);
}

public class StrategyEnhancedBrowserProcessFactory : IBrowserProcessFactory
{
    readonly bool _inDocker;
    readonly string _strategy;
    readonly IFsAbstraction _fsAbstraction;

    public StrategyEnhancedBrowserProcessFactory(bool inDocker, string? strategy, IFsAbstraction fsAbstraction)
    {
        _inDocker = inDocker;
        _strategy = strategy ?? "default";
        _fsAbstraction = fsAbstraction;
    }

    public IBrowserProcess Create(string urlToOpen)
    {
        var browserPathsToRun = new StructList<string>();
        var headlessOverride = Environment.GetEnvironmentVariable("BBBROWSER");
        if (headlessOverride != null)
        {
            browserPathsToRun.AddUnique(headlessOverride);
        }

        if (!_fsAbstraction.IsUnixFs && _strategy == "PreferFirefoxOnWindows")
        {
            var browserPath = BrowserPathFinder.GetBrowserPath(_fsAbstraction, true);
            if (browserPath!=null) browserPathsToRun.AddUnique(browserPath);
        }

        if (browserPathsToRun.Count == 0)
        {
            var browserPath = BrowserPathFinder.GetBrowserPath(_fsAbstraction, false);
            if (browserPath!=null) browserPathsToRun.AddUnique(browserPath);
        }

        if (browserPathsToRun.Count == 0)
        {
            throw new Exception("Cannot find browser on common known paths, use BBBROWSER environmental variable to define path to browser.");
        }

        return new BrowserProcessFactory(_inDocker, browserPathsToRun[0]).Create(urlToOpen);
    }
}

public class BrowserProcessFactory : IBrowserProcessFactory
{
    readonly bool _inDocker;
    readonly string _browserPath;
    readonly bool _isFirefox;

    public BrowserProcessFactory(bool inDocker,
        string browserPath = "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe")
    {
        _inDocker = inDocker;
        _browserPath = browserPath;
        _isFirefox = PathUtils.GetFile(PathUtils.Normalize(browserPath))
            .Contains("firefox", StringComparison.OrdinalIgnoreCase);
    }

    public IBrowserProcess Create(string urlToOpen)
    {
        var processArgs = new List<string>();
        DirectoryInfo directoryInfo = null;
        if (_isFirefox)
        {
            processArgs.Add("-CreateProfile bb");
            try
            {
                var processStartInfoInit = new ProcessStartInfo(_browserPath, string.Join(" ", processArgs));
                var initProcess = Process.Start(processStartInfoInit);
                initProcess?.WaitForExit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            processArgs.Clear();
            processArgs.Add("--headless");
            processArgs.Add("--no-remote");
            processArgs.Add("-P bb");
        }
        else
        {
            if (!_inDocker)
            {
                var path = Path.GetRandomFileName();
                directoryInfo = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), path));
                processArgs.Add($"--user-data-dir=\"{directoryInfo.FullName}\"");
                processArgs.Add("--bwsi");
            }
            else
            {
                processArgs.Add("--no-sandbox");
                processArgs.Add("--remote-debugging-address=0.0.0.0");
            }

            processArgs.Add($"--remote-debugging-port={9223}");
            processArgs.Add("--headless");
            processArgs.Add("--disable-gpu");
            processArgs.Add("--no-first-run");
            processArgs.Add("--disable-background-timer-throttling");
        }
        processArgs.Add("\"" + urlToOpen + "\"");
        var processStartInfo = new ProcessStartInfo(_browserPath, string.Join(" ", processArgs));
        processStartInfo.RedirectStandardError = true;
        processStartInfo.RedirectStandardOutput = true;
        var browserProcess = Process.Start(processStartInfo);
        browserProcess.ErrorDataReceived += (e, d) => { Console.Write(d.Data); };
        browserProcess.OutputDataReceived += (e, d) => { Console.Write(d.Data); };
        return new LocalBrowserProcess(directoryInfo, browserProcess);
    }

    public class LocalBrowserProcess : IBrowserProcess
    {
        readonly DirectoryInfo _userDirectory;
        readonly EventHandler _disposeHandler;
        readonly UnhandledExceptionEventHandler _unhandledExceptionHandler;

        public LocalBrowserProcess(DirectoryInfo? userDirectory, Process process)
        {
            Process = process;
            process.Exited += (sender, args) => { Console.WriteLine("Headless browser stopped with " + process.ExitCode); };
            _userDirectory = userDirectory;
            _disposeHandler = (s, e) => Dispose();
            _unhandledExceptionHandler = (s, e) => Dispose();
            AppDomain.CurrentDomain.DomainUnload += _disposeHandler;
            AppDomain.CurrentDomain.ProcessExit += _disposeHandler;
            AppDomain.CurrentDomain.UnhandledException += _unhandledExceptionHandler;
        }

        public Process Process { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            AppDomain.CurrentDomain.DomainUnload -= _disposeHandler;
            AppDomain.CurrentDomain.ProcessExit -= _disposeHandler;
            AppDomain.CurrentDomain.UnhandledException -= _unhandledExceptionHandler;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // On Windows Chrome locks pma file with some child process, so we have to kill whole process tree
                try
                {
                    var processStartInfo = new ProcessStartInfo("taskkill", "/F /T /pid " + this.Process.Id)
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        //RedirectStandardOutput = true,
                        //RedirectStandardError = true
                    };
                    Process.Start(processStartInfo).WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            else
            {
                try
                {
                    Process.Kill();
                }
                catch
                {
                    // ignored
                }
            }

            Process.WaitForExit();
            var repetition = 0;
            while (repetition++ < 5)
            {
                try
                {
                    _userDirectory?.Delete(true);
                    return;
                }
                catch
                {
                    Thread.Sleep(50 * repetition);
                }
            }
        }
    }
}
