using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;
using System.Text.Json.Nodes;

namespace Lib.Registry;

public class BunNodePackageManager : INodePackageManager
{
    readonly IDiskCache _diskCache;
    readonly ILogger _logger;
    readonly string? _bunPath;

    public BunNodePackageManager(IDiskCache diskCache, ILogger logger)
    {
        _diskCache = diskCache;
        _logger = logger;
        _bunPath = GetBunPath();
    }

    string? GetBunPath()
    {
        var bunExecName = "bun";
        if (!_diskCache.FsAbstraction.IsUnixFs)
        {
            bunExecName += ".cmd";
        }

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(p => PathUtils.Join(PathUtils.Normalize(new DirectoryInfo(p).FullName), bunExecName))
            .FirstOrDefault(_diskCache.FsAbstraction.FileExists);
    }

    public bool IsAvailable => _bunPath != null;

    public bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        while (projectDirectory != null)
        {
            if (projectDirectory.IsFake && dc != null)
            {
                if (dc.FsAbstraction.FileExists(PathUtils.Join(projectDirectory.FullPath, "bun.lock")) ||
                    dc.FsAbstraction.FileExists(PathUtils.Join(projectDirectory.FullPath, "bun.lockb")))
                {
                    return true;
                }
            }

            if (projectDirectory.TryGetChild("bun.lock") is IFileCache ||
                projectDirectory.TryGetChild("bun.lockb") is IFileCache)
            {
                return true;
            }

            projectDirectory = projectDirectory.Parent;
        }

        return false;
    }

    public IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory)
    {
        var lockFile = projectDirectory.TryGetChild("bun.lock") as IFileCache;
        if (lockFile == null)
        {
            return Enumerable.Empty<PackagePathVersion>();
        }

        return ParseBunLock(projectDirectory, lockFile.Utf8Content);
    }

    public static IEnumerable<PackagePathVersion> ParseBunLock(IDirectoryCache projectDirectory, string content)
    {
        var parsed = JsonNode.Parse(content)!.AsObject();
        var packages = parsed["packages"] as JsonObject;
        if (packages == null)
        {
            yield break;
        }

        foreach (var prop in packages.Properties())
        {
            if (prop.Value is not JsonArray packageInfo || packageInfo.Count == 0)
            {
                continue;
            }

            var resolution = packageInfo[0].Value<string>();
            var version = ExtractPackageVersion(resolution);
            if (version == null)
            {
                continue;
            }

            yield return new PackagePathVersion
            {
                Name = prop.Name,
                Version = version,
                Path = PathUtils.Join(projectDirectory.FullPath, "node_modules/" + prop.Name)
            };
        }
    }

    static string? ExtractPackageVersion(string? resolution)
    {
        const string npmProtocol = "@npm:";
        var npmProtocolIndex = resolution?.LastIndexOf(npmProtocol, StringComparison.Ordinal);
        if (npmProtocolIndex is null or < 0)
        {
            return null;
        }

        return resolution![(npmProtocolIndex.Value + npmProtocol.Length)..];
    }

    public void RunBun(string dir, string aParams)
    {
        _logger.Info("Bun " + aParams);
        var start = new ProcessStartInfo(_bunPath!, aParams)
        {
            UseShellExecute = false,
            WorkingDirectory = dir,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };

        var process = Process.Start(start)!;
        process.OutputDataReceived += Process_OutputDataReceived;
        process.ErrorDataReceived += Process_OutputDataReceived;
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();
    }

    void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        _logger.WriteLine(e.Data);
    }

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        RunBunWithParam(projectDirectory, "install");
    }

    void RunBunWithParam(IDirectoryCache projectDirectory, string param)
    {
        var fullPath = projectDirectory.FullPath;
        var project = TSProject.Create(projectDirectory, _diskCache, _logger, null);
        project.LoadProjectJson(true, null);
        if (project.ProjectOptions.NpmRegistry != null)
        {
            if (projectDirectory.TryGetChild(".npmrc") is not IFileCache)
            {
                _diskCache.FsAbstraction.WriteAllUtf8(
                    PathUtils.Join(fullPath, ".npmrc"),
                    "registry =" + project.ProjectOptions.NpmRegistry);
            }
        }

        RunBun(fullPath, param);
    }

    public void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        RunBunWithParam(projectDirectory, "update");
    }

    public void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName)
    {
        RunBunWithParam(projectDirectory, "update " + packageName);
    }

    public void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false)
    {
        RunBunWithParam(projectDirectory, "add " + (devDependency ? "-d " : "") + packageName);
    }
}
