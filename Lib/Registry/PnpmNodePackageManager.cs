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

public class PnpmNodePackageManager : INodePackageManager
{
    readonly IDiskCache _diskCache;
    readonly ILogger _logger;
    string? _pnpmPath;

    public PnpmNodePackageManager(IDiskCache diskCache, ILogger logger)
    {
        _diskCache = diskCache;
        _logger = logger;
        _pnpmPath = GetPnpmPath();
    }

    string? GetPnpmPath()
    {
        var npmExecName = "pnpm";
        if (!_diskCache.FsAbstraction.IsUnixFs)
        {
            npmExecName += ".cmd";
        }

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(p => PathUtils.Join(PathUtils.Normalize(new DirectoryInfo(p).FullName), npmExecName))
            .FirstOrDefault(_diskCache.FsAbstraction.FileExists);
    }

    public bool IsAvailable => _pnpmPath != null;

    public bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        IDirectoryCache? dir = projectDirectory;
        while (dir != null)
        {
            if (dir.IsFake && dc != null)
            {
                if (dc.FsAbstraction.FileExists(PathUtils.Join(dir.FullPath, "pnpm-lock.yaml")))
                {
                    return true;
                }
            }

            if (dir.TryGetChild("pnpm-lock.yaml") is IFileCache)
            {
                return true;
            }

            dir = dir.Parent;
        }

        return false;
    }

    public IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory)
    {
        var lockFile = projectDirectory.TryGetChild("pnpm-lock.yaml") as IFileCache;
        if (lockFile == null)
        {
            return Enumerable.Empty<PackagePathVersion>();
        }

        return ParsePnpmLock(projectDirectory, lockFile.Utf8Content);
    }

    IEnumerable<PackagePathVersion> ParsePnpmLock(IDirectoryCache projectDirectory, string content)
    {
        var parsed = PnpmLockParser.Parse(content);
        var known = new HashSet<string>();
        foreach (var pair in parsed)
        {
            var name = pair.Key;
            if (!known.Add(name)) continue;
            yield return new()
            {
                Name = name,
                Version = pair.Value,
                Path = PathUtils.Join(projectDirectory.FullPath, "node_modules/" + name)
            };
        }
    }

    public void RunPnpm(string dir, string aParams)
    {
        _logger.Info("Pnpm " + aParams);
        PackageManagerProcess.Run("Pnpm", _pnpmPath, dir, aParams, _logger);
    }

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc, bool frozen = false)
    {
        RunNpmWithParam(projectDirectory, frozen ? "install --frozen-lockfile" : "install");
    }

    void RunNpmWithParam(IDirectoryCache projectDirectory, string param)
    {
        var fullPath = projectDirectory.FullPath;
        ProjectNpmRc.EnsureRegistry(projectDirectory, _diskCache, _logger);

        var par = param;
        /* No such parameter exists in pnpm
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BBCoreNoLinks")))
        {
            par += " --no-bin-links";
        }
        */
        RunPnpm(fullPath, par);
    }

    public void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        _diskCache.UpdateIfNeeded(projectDirectory);
        RunNpmWithParam(projectDirectory, "update");
    }

    public void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName)
    {
        RunNpmWithParam(projectDirectory, "update " + packageName);
    }

    public void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false)
    {
        RunNpmWithParam(projectDirectory, "add " + (devDependency ? "-D " : "") + packageName);
    }
}