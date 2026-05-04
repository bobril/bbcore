using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Lib.DiskCache;
using Lib.Utils;
using Njsast;

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
    readonly bool _verbose;

    public StrategyEnhancedBrowserProcessFactory(bool inDocker, string? strategy, IFsAbstraction fsAbstraction,
        bool verbose = false)
    {
        _inDocker = inDocker;
        _strategy = strategy ?? "default";
        _fsAbstraction = fsAbstraction;
        _verbose = verbose;
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
            if (browserPath != null) browserPathsToRun.AddUnique(browserPath);
        }

        if (browserPathsToRun.Count == 0)
        {
            var browserPath = BrowserPathFinder.GetBrowserPath(_fsAbstraction, false);
            if (browserPath != null) browserPathsToRun.AddUnique(browserPath);
        }

        if (browserPathsToRun.Count == 0)
        {
            throw new Exception(
                "Cannot find browser on common known paths, use BBBROWSER environmental variable to define path to browser.");
        }

        return new BrowserProcessFactory(_inDocker, browserPathsToRun[0], _verbose).Create(urlToOpen);
    }
}

public class BrowserProcessFactory : IBrowserProcessFactory
{
    readonly bool _inDocker;
    readonly string _browserPath;
    readonly bool _isFirefox;
    readonly bool _verbose;

    public BrowserProcessFactory(bool inDocker,
        string browserPath = "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe", bool verbose = false)
    {
        _inDocker = inDocker;
        _browserPath = browserPath;
        _isFirefox = PathUtils.GetFile(PathUtils.Normalize(browserPath))
            .Contains("firefox", StringComparison.OrdinalIgnoreCase);
        _verbose = verbose;
    }

    public IBrowserProcess Create(string urlToOpen)
    {
        var processArgs = new List<string>();
        DirectoryInfo directoryInfo = null!;
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
            processArgs.Add("--window-position=-10000,-10000"); // Workaround to be really headless
        }

        processArgs.Add("\"" + urlToOpen + "\"");
        var processStartInfo = new ProcessStartInfo(_browserPath, string.Join(" ", processArgs));
        processStartInfo.RedirectStandardError = true;
        processStartInfo.RedirectStandardOutput = true;
        var browserProcess = Process.Start(processStartInfo);
        if (browserProcess == null)
            throw new Exception("Headless browser process did not start.");
        browserProcess.EnableRaisingEvents = true;
        browserProcess.ErrorDataReceived += (e, d) =>
        {
            if (_verbose && !string.IsNullOrEmpty(d.Data)) Console.WriteLine(d.Data);
        };
        browserProcess.OutputDataReceived += (e, d) =>
        {
            if (_verbose && !string.IsNullOrEmpty(d.Data)) Console.WriteLine(d.Data);
        };
        browserProcess.BeginErrorReadLine();
        browserProcess.BeginOutputReadLine();

        var jobHandle = IntPtr.Zero;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            jobHandle = CreateKillOnCloseJobObject();
            if (jobHandle != IntPtr.Zero)
            {
                AssignProcessToJobObject(jobHandle, browserProcess.Handle);
            }
        }

        return new LocalBrowserProcess(directoryInfo, browserProcess, _verbose, jobHandle);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(
        IntPtr hJob,
        int jobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    const int JobObjectExtendedLimitInformation = 9;
    const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    static IntPtr CreateKillOnCloseJobObject()
    {
        var hJob = CreateJobObject(IntPtr.Zero, null);
        if (hJob == IntPtr.Zero)
            return IntPtr.Zero;

        var infoSize = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var infoPtr = Marshal.AllocHGlobal(infoSize);
        try
        {
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };
            Marshal.StructureToPtr(info, infoPtr, false);
            if (!SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, infoPtr, (uint)infoSize))
            {
                CloseHandle(hJob);
                return IntPtr.Zero;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }

        return hJob;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public IntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    public class LocalBrowserProcess : IBrowserProcess
    {
        readonly DirectoryInfo? _userDirectory;
        readonly EventHandler _disposeHandler;
        readonly UnhandledExceptionEventHandler _unhandledExceptionHandler;
        readonly bool _verbose;
        readonly IntPtr _jobHandle;
        int _disposed;

        public LocalBrowserProcess(DirectoryInfo? userDirectory, Process process, bool verbose,
            IntPtr jobHandle = default)
        {
            Process = process;
            _verbose = verbose;
            _jobHandle = jobHandle;
            process.Exited += (sender, args) =>
            {
                if (_verbose)
                    Console.WriteLine("Headless browser stopped with " + process.ExitCode);
            };
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
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            GC.SuppressFinalize(this);
            AppDomain.CurrentDomain.DomainUnload -= _disposeHandler;
            AppDomain.CurrentDomain.ProcessExit -= _disposeHandler;
            AppDomain.CurrentDomain.UnhandledException -= _unhandledExceptionHandler;
            KillProcessTree(Process);

            if (_userDirectory != null && OperatingSystem.IsWindows())
            {
                KillDetachedWindowsBrowserProcesses(_userDirectory.FullName);
            }

            try
            {
                Process.WaitForExit(5000);
            }
            catch
            {
                // ignored
            }

            DeleteUserDirectoryWithRetry();

            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
            }
        }

        static void KillProcessTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
                // ignored
            }
        }

        [SupportedOSPlatform("windows")]
        static void KillDetachedWindowsBrowserProcesses(string userDirectory)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, Name, CommandLine FROM Win32_Process " +
                    "WHERE Name = 'chrome.exe' OR Name = 'msedge.exe' OR Name = 'chromium.exe'");
                using var processes = searcher.Get();
                foreach (ManagementObject processInfo in processes)
                {
                    var commandLine = processInfo["CommandLine"] as string;
                    if (commandLine == null ||
                        commandLine.IndexOf(userDirectory, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var processId = Convert.ToInt32(processInfo["ProcessId"]);
                    using var process = Process.GetProcessById(processId);
                    KillProcessTree(process);
                }
            }
            catch
            {
                // ignored
            }
        }

        void DeleteUserDirectoryWithRetry()
        {
            if (_userDirectory == null)
                return;
            var repetition = 0;
            while (repetition++ < 20)
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    RunWindowsCommand("cmd", "/C rd /s /q \"" + _userDirectory.FullName + "\"");
                    if (!Directory.Exists(_userDirectory.FullName))
                        return;
                }
                else
                {
                    try
                    {
                        _userDirectory.Delete(true);
                        return;
                    }
                    catch
                    {
                        // ignored
                    }
                }

                Thread.Sleep(100 * repetition);
            }
        }

        static void RunWindowsCommand(string fileName, string arguments)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo(fileName, arguments)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                Process.Start(processStartInfo)?.WaitForExit();
            }
            catch
            {
                // ignored
            }
        }
    }
}
