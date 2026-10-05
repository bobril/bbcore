using System;
using System.Diagnostics;
using System.IO;
using Lib.HeadlessBrowser;
using Xunit;

namespace Lib.Test;

public class LocalBrowserProcessDisposeTests : IDisposable
{
    readonly DirectoryInfo _profileDir =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

    public void Dispose()
    {
        try
        {
            _profileDir.Delete(true);
        }
        catch
        {
            // ignored: a failing test may leave something running in the directory
        }
    }

    static Process StartCmd(string commandLine, string workingDirectory)
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/C " + commandLine)
        {
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false
        })!;
        process.EnableRaisingEvents = true;
        return process;
    }

    // Mimics chrome.exe handing over to a detached browser process and exiting: the launched process starts a long
    // running child inside the profile directory (its working directory locks the directory) and exits.
    [Fact]
    public void DisposeKillsBrowserThatOutlivedTheLaunchedProcessSoProfileDirectoryCanBeDeleted()
    {
        if (!OperatingSystem.IsWindows())
            return;
        // ping -n 2 waits about a second, so the job is assigned before the detached child is created
        var launched = StartCmd("ping -n 2 127.0.0.1 > nul & start /b ping -n 60 127.0.0.1 > nul", _profileDir.FullName);
        var job = BrowserProcessFactory.CreateKillOnCloseJobFor(launched);
        Assert.NotEqual(IntPtr.Zero, job);
        Assert.True(launched.WaitForExit(10000), "the launched process should exit and leave the child running");
        var browser = new BrowserProcessFactory.LocalBrowserProcess(_profileDir, launched, false, job);

        var stopwatch = Stopwatch.StartNew();
        browser.Dispose();

        Assert.False(Directory.Exists(_profileDir.FullName), "profile directory should have been deleted");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8), "Dispose took " + stopwatch.Elapsed);
    }

    // Whatever keeps the profile directory locked, Dispose has to give up in a few seconds instead of retrying for
    // about half a minute while the test result is already known.
    [Fact]
    public void DisposeDoesNotWaitLongWhenProfileDirectoryStaysLocked()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var launched = StartCmd("exit 0", _profileDir.FullName);
        Assert.True(launched.WaitForExit(10000));
        using var lockedFile = new FileStream(Path.Combine(_profileDir.FullName, "locked.tmp"), FileMode.Create,
            FileAccess.ReadWrite, FileShare.None);
        var browser = new BrowserProcessFactory.LocalBrowserProcess(_profileDir, launched, false);

        var stopwatch = Stopwatch.StartNew();
        browser.Dispose();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Dispose took " + stopwatch.Elapsed);
        Assert.True(Directory.Exists(_profileDir.FullName), "the locked directory cannot have been deleted");
    }
}
