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
        readonly bool _inDocker;
        public string ChromePath { get; }

        public ChromeProcessFactory(bool inDocker,
            string chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe")
        {
            _inDocker = inDocker;
            ChromePath = chromePath;
        }

        public IChromeProcess Create(string urlToOpen)
        {
            var chromeProcessArgs = new List<string>();
            DirectoryInfo directoryInfo = null;
            if (!_inDocker)
            {
                var path = Path.GetRandomFileName();
                directoryInfo = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), path));
                chromeProcessArgs.Add($"--user-data-dir=\"{directoryInfo.FullName}\"");
                chromeProcessArgs.Add("--bwsi");
            }
            else
            {
                chromeProcessArgs.Add("--no-sandbox");
                chromeProcessArgs.Add("--remote-debugging-address=0.0.0.0");
            }

            chromeProcessArgs.Add($"--remote-debugging-port={9223}");
            chromeProcessArgs.Add("--headless");
            chromeProcessArgs.Add("--disable-gpu");
            chromeProcessArgs.Add("--no-first-run");
            chromeProcessArgs.Add("\"" + urlToOpen + "\"");
            var processStartInfo = new ProcessStartInfo(ChromePath, string.Join(" ", chromeProcessArgs));
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardOutput = true;
            var chromeProcess = Process.Start(processStartInfo);
            chromeProcess.ErrorDataReceived += (e, d) => { Console.Write(d.Data); };
            chromeProcess.OutputDataReceived += (e, d) => { Console.Write(d.Data); };
            return new LocalChromeProcess(directoryInfo, chromeProcess);
        }

        public class LocalChromeProcess : IChromeProcess
        {
            readonly DirectoryInfo _userDirectory;
            readonly EventHandler _disposeHandler;
            readonly UnhandledExceptionEventHandler _unhandledExceptionHandler;

            public LocalChromeProcess(DirectoryInfo userDirectory, Process process)
            {
                Process = process;
                process.Exited += (sender, args) => { Console.WriteLine("Chromium exited with " + process.ExitCode); };
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
}
