using System;
using System.Diagnostics;
using System.Text;
using Lib.Utils.Logger;

namespace Lib.Registry;

public class PackageManagerException : Exception
{
    public PackageManagerException(string message) : base(message)
    {
    }
}

public static class PackageManagerProcess
{
    // Runs package manager with output forwarded to logger. Throws PackageManagerException when it cannot be started
    // or exits with non-zero code, so failed installs (including security policy rejections) are never ignored.
    public static void Run(string toolName, string? exePath, string dir, string aParams, ILogger logger)
    {
        if (exePath == null)
            throw new PackageManagerException(toolName + " not found in PATH");
        var start = CreateStartInfo(exePath, dir, aParams);
        using var process = StartProcess(toolName, start);
        process.OutputDataReceived += (_, e) => logger.WriteLine(e.Data ?? "");
        process.ErrorDataReceived += (_, e) => logger.WriteLine(e.Data ?? "");
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.StandardInput.Close();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new PackageManagerException(
                $"{toolName} {aParams} failed with exit code {process.ExitCode} in {dir}");
    }

    // Runs package manager and returns its trimmed standard output or null when it fails
    public static string? RunAndCapture(string exePath, string dir, string aParams)
    {
        try
        {
            using var process = Process.Start(CreateStartInfo(exePath, dir, aParams))!;
            process.StandardInput.Close();
            var stdErr = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            stdErr.Wait();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static ProcessStartInfo CreateStartInfo(string exePath, string dir, string aParams)
    {
        return new(exePath, aParams)
        {
            UseShellExecute = false,
            WorkingDirectory = dir,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };
    }

    static Process StartProcess(string toolName, ProcessStartInfo start)
    {
        try
        {
            return Process.Start(start) ??
                   throw new PackageManagerException(toolName + " " + start.Arguments + " could not be started");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new PackageManagerException(toolName + " " + start.Arguments + " could not be started: " +
                                              e.Message);
        }
    }
}
