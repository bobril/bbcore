using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lib.Registry;
using Lib.Utils;
using Lib.Utils.Logger;

namespace Lib.TSCompiler;

// npm package with native executable for one platform, like @tsrs/darwin-arm64 or @typescript/typescript-win32-x64
public sealed record NativePackage(
    string DisplayName,
    string PackagePrefix,
    string[] Platforms,
    string ExecutablePath,
    string DirectoryPrefix,
    string VersionOutputMarker);

// Downloads native compiler packages from npm into bbcore tools directory
public static class NativePackageInstaller
{
    static readonly object Lock = new();

    public static string InstallDirectory(string toolsPath, NativePackage package, string version) =>
        PathUtils.Join(toolsPath, package.DirectoryPrefix + version);

    public static bool TryInstall(string toolsPath, NativePackage package, string version, ILogger logger,
        out string executable, out string failureReason)
    {
        executable = "";
        failureReason = "";
        if (package.Platforms.Length == 0)
        {
            failureReason = "no " + package.DisplayName + " build exists for " + PlatformDescription();
            return false;
        }

        var installDirectory = InstallDirectory(toolsPath, package, version);
        lock (Lock)
        {
            foreach (var platform in package.Platforms)
            {
                var candidate = PathUtils.Join(installDirectory, platform + "/" + package.ExecutablePath);
                if (File.Exists(candidate))
                {
                    executable = candidate;
                    return true;
                }
            }

            foreach (var platform in package.Platforms)
            {
                try
                {
                    if (DownloadAndExtract(installDirectory, package, platform, version, logger).GetAwaiter()
                            .GetResult() is { } installed)
                    {
                        executable = installed;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    failureReason = "downloading " + package.PackagePrefix + platform + "@" + version + " failed: " +
                                    ex.Message;
                    return false;
                }
            }
        }

        failureReason = package.PackagePrefix + package.Platforms[0] + "@" + version + " is not published for " +
                        PlatformDescription();
        return false;
    }

    // Returns null when package or version does not exist
    static async Task<string?> DownloadAndExtract(string installDirectory, NativePackage package, string platform,
        string version, ILogger logger)
    {
        var packageName = package.PackagePrefix + platform;
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

        logger.Info("Downloading " + package.DisplayName + " " + version + " (" + packageName + ")");
        var bytes = await npm.GetPackageTgz(packageName, tgzName);
        // Extract to temporary directory and move it into place, so parallel bb processes never see half written binary
        var targetDirectory = PathUtils.Join(installDirectory, platform);
        DeleteStaleTemporaryDirectories(installDirectory, platform);
        var temporaryDirectory = targetDirectory + TemporarySuffix + Guid.NewGuid().ToString("N");
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
            var temporaryExecutable = PathUtils.Join(temporaryDirectory, package.ExecutablePath);
            if (!File.Exists(temporaryExecutable))
                throw new IOException("package does not contain " + package.ExecutablePath);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporaryExecutable,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                    UnixFileMode.OtherExecute);
            VerifyRuns(temporaryExecutable, package);
            MoveDirectory(temporaryDirectory, targetDirectory);
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
        }

        var executable = PathUtils.Join(targetDirectory, package.ExecutablePath);
        logger.Info("Installed " + package.DisplayName + " " + version + " to " + executable);
        return executable;
    }

    const string TemporarySuffix = ".tmp-";
    const int MoveAttempts = 20;
    const int DeleteAttempts = 5;

    // Waits before next attempt: 100, 200, 300, ... ms up to 1 s, together about 15 s for all move attempts
    internal static Action<int> RetryDelay = attempt => Thread.Sleep(Math.Min(attempt * 100, 1000));

    // On Windows a directory cannot be renamed while any file in it is open. Just written and just executed binary is
    // often held for a while by antivirus real time scanning (much longer for unsigned binary it sees first time) or by
    // loader after process exits, so the move is retried. Directory installed meanwhile by another bb process is used.
    internal static void MoveDirectory(string source, string target)
    {
        for (var attempt = 1;; attempt++)
        {
            if (Directory.Exists(target))
                return;
            try
            {
                Directory.Move(source, target);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (Directory.Exists(target))
                    return;
                if (attempt >= MoveAttempts)
                    throw new IOException("moving " + source + " to " + target + " failed " + attempt +
                                          " times, file is probably held by antivirus: " + ex.Message, ex);
                RetryDelay(attempt);
            }
        }
    }

    // Best effort, failure to remove temporary directory must not fail installation
    internal static bool TryDeleteDirectory(string directory)
    {
        for (var attempt = 1;; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= DeleteAttempts)
                    return false;
                RetryDelay(attempt);
            }
        }
    }

    // Leftovers of earlier installations which could not delete their temporary directory. Recent ones may belong to
    // installation running in parallel bb process, so they are kept.
    internal static void DeleteStaleTemporaryDirectories(string installDirectory, string platform)
    {
        try
        {
            if (!Directory.Exists(installDirectory))
                return;
            foreach (var directory in Directory.GetDirectories(installDirectory, platform + TemporarySuffix + "*"))
            {
                if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) > TimeSpan.FromHours(1))
                    TryDeleteDirectory(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    static void VerifyRuns(string executable, NativePackage package)
    {
        var start = new ProcessStartInfo(executable)
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
        if (!process.WaitForExit(30000) || process.ExitCode != 0 ||
            !output.Contains(package.VersionOutputMarker, StringComparison.Ordinal))
            throw new IOException("downloaded " + package.DisplayName + " binary does not run");
    }

    internal static bool IsMusl()
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
