using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Lib.Chrome
{
    public interface IChromeProcess : IDisposable
    {
    }

    public interface IChromeProcessFactory
    {
        IChromeProcess Create(string urlToOpen);
    }

    public class ChromeProcessFactory : IChromeProcessFactory
    {
        public string ChromePath { get; }

        public ChromeProcessFactory(string chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe")
        {
            ChromePath = chromePath;
        }

        public IChromeProcess Create(string urlToOpen)
        {
            string path = Path.GetRandomFileName();
            var directoryInfo = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), path));
            var chromeProcessArgs = new List<string>
            {
                $"--user-data-dir=\"{directoryInfo.FullName}\"",
                $"--remote-debugging-port={8081}",
                "--bwsi",
                "--no-first-run",
                "--headless",
                "--disable-gpu",
                "\""+urlToOpen+"\""
            };
            var processStartInfo = new ProcessStartInfo(ChromePath, string.Join(" ", chromeProcessArgs));
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardOutput = true;
            var chromeProcess = Process.Start(processStartInfo);
            chromeProcess.ErrorDataReceived += (e, d) =>
            {
                Console.Write(d.Data);
            };
            chromeProcess.OutputDataReceived += (e, d) =>
            {
                Console.Write(d.Data);
            };
            return new LocalChromeProcess(directoryInfo, chromeProcess);
        }

        public class LocalChromeProcess : IChromeProcess
        {
            private readonly DirectoryInfo _userDirectory;
            private readonly EventHandler _disposeHandler;
            private readonly UnhandledExceptionEventHandler _unhandledExceptionHandler;

            public LocalChromeProcess(DirectoryInfo userDirectory, Process process)
            {
                Process = process;
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
                    catch (Exception ex) { Console.WriteLine(ex); }
                }
                else
                {
                    Process.Kill();
                }
                Process.WaitForExit();
                var repetition = 0;
                while (repetition++ < 5)
                {
                    try
                    {
                        _userDirectory.Delete(true);
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
}
