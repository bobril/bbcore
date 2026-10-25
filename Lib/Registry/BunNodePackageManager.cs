using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;

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
        IDirectoryCache? dir = projectDirectory;
        while (dir != null)
        {
            if (dir.IsFake && dc != null)
            {
                if (dc.FsAbstraction.FileExists(PathUtils.Join(dir.FullPath, "bun.lock")) ||
                    dc.FsAbstraction.FileExists(PathUtils.Join(dir.FullPath, "bun.lockb")))
                {
                    return true;
                }
            }

            if (dir.TryGetChild("bun.lock") is IFileCache ||
                dir.TryGetChild("bun.lockb") is IFileCache)
            {
                return true;
            }

            dir = dir.Parent;
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
        var parsed = JObject.Parse(content);
        var packages = parsed["packages"] as JObject;
        if (packages == null)
        {
            yield break;
        }

        foreach (var prop in packages.Properties())
        {
            if (prop.Value is not JArray packageInfo || packageInfo.Count == 0)
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
        PackageManagerProcess.Run("Bun", _bunPath, dir, aParams, _logger);
    }

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc, bool frozen = false)
    {
        RunBunWithParam(projectDirectory, frozen ? "install --frozen-lockfile" : "install");
    }

    void RunBunWithParam(IDirectoryCache projectDirectory, string param)
    {
        var fullPath = projectDirectory.FullPath;
        ProjectNpmRc.EnsureRegistry(projectDirectory, _diskCache, _logger);

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
