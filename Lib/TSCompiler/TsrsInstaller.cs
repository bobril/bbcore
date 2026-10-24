using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lib.Registry;
using Lib.Utils;
using Lib.Utils.Logger;

namespace Lib.TSCompiler;

// Downloads tsrs native binary for current platform from npm into bbcore tools directory
public static class TsrsInstaller
{
    public const string DefaultVersion = "0.18.0-ts7.1.0-dev.20260929";

    static readonly object Lock = new();

    public static string InstallDirectory(string toolsPath, string version) =>
        PathUtils.Join(toolsPath, "tsrs-" + version);

    public static bool TryInstall(string toolsPath, string version, ILogger logger, out string tsrsExecutable,
        out string failureReason)
    {
        tsrsExecutable = "";
        failureReason = "";
        var packages = TsrsLocator.PlatformPackageNames(IsMusl()).ToArray();
        if (packages.Length == 0)
        {
            failureReason = "no tsrs build exists for " + PlatformDescription();
            return false;
        }

        var installDirectory = InstallDirectory(toolsPath, version);
        lock (Lock)
        {
            foreach (var package in packages)
            {
                var executable = PathUtils.Join(installDirectory, package + "/" + TsrsLocator.ExecutableName);
                if (File.Exists(executable))
                {
                    tsrsExecutable = executable;
                    return true;
                }
            }

            foreach (var package in packages)
            {
                try
                {
                    if (DownloadAndExtract(installDirectory, package, version, logger).GetAwaiter().GetResult() is
                        { } executable)
                    {
                        tsrsExecutable = executable;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    failureReason = "downloading @tsrs/" + package + "@" + version + " failed: " + ex.Message;
                    return false;
                }
            }
        }

        failureReason = "@tsrs/" + packages[0] + "@" + version + " is not published for " + PlatformDescription();
        return false;
    }

    // Returns null when package or version does not exist
    static async Task<string?> DownloadAndExtract(string installDirectory, string package, string version,
        ILogger logger)
    {
        var packageName = "@tsrs/" + package;
        var semanticVersion = new SemanticVersioning.Version(version);
        using var npm = new NpmRepositoryAccessor();
        var (_, content) = await npm.GetPackageInfo(packageName, null);
        if (string.IsNullOrEmpty(content) || !content.TrimStart().StartsWith('{') || content.Contains("\"error\""))
            return null;
        string? tgzName = null;
        new PackageInfo(content).LazyParseVersions(v => v == semanticVersion, reader =>
        {
            var packageJson = PackageJson.Parse(reader);
            PathUtils.SplitDirAndFile(packageJson.Dist.Tarball, out var name);
            tgzName = name.ToString();
        });
        if (tgzName == null)
            return null;

        logger.Info("Downloading tsrs " + version + " (" + packageName + ")");
        var bytes = await npm.GetPackageTgz(packageName, tgzName);
        // Extract to temporary directory and move it into place, so parallel bb processes never see half written binary
        var targetDirectory = PathUtils.Join(installDirectory, package);
        var temporaryDirectory = targetDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            await TarExtractor.ExtractTgzAsync(bytes, async (name, fileContent, _) =>
            {
                if (name.StartsWith("package/", StringComparison.Ordinal))
                    name = name["package/".Length..];
                var fileName = PathUtils.Join(temporaryDirectory, name);
                Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
                await File.WriteAllBytesAsync(fileName, fileContent);
                return true;
            });
            var temporaryExecutable = PathUtils.Join(temporaryDirectory, TsrsLocator.ExecutableName);
            if (!File.Exists(temporaryExecutable))
                throw new IOException("package does not contain " + TsrsLocator.ExecutableName);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporaryExecutable,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                    UnixFileMode.OtherExecute);
            VerifyRuns(temporaryExecutable);
            if (!Directory.Exists(targetDirectory))
                Directory.Move(temporaryDirectory, targetDirectory);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, true);
        }

        var executable = PathUtils.Join(targetDirectory, TsrsLocator.ExecutableName);
        logger.Info("Installed tsrs " + version + " to " + executable);
        return executable;
    }

    static void VerifyRuns(string tsrsExecutable)
    {
        var start = new ProcessStartInfo(tsrsExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start)!;
        var errorTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        errorTask.GetAwaiter().GetResult();
        if (!process.WaitForExit(30000) || process.ExitCode != 0 || !output.Contains("tsrs", StringComparison.Ordinal))
            throw new IOException("downloaded tsrs binary does not run");
    }

    static bool IsMusl()
    {
        if (!OperatingSystem.IsLinux()) return false;
        try
        {
            return File.Exists("/etc/alpine-release") || Directory.GetFiles("/lib", "ld-musl-*").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    static string PlatformDescription() =>
        System.Runtime.InteropServices.RuntimeInformation.OSDescription + " " +
        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
}
