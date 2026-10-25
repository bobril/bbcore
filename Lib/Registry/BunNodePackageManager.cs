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

    // Keys are install paths like "debug" or nested "send/debug", "@scope/a/@scope/b". Values start with resolution
    // "name@version", non registry ones look like "name@github:...", "name@workspace:..." or "name@file:..."
    public static IEnumerable<PackagePathVersion> ParseBunLock(IDirectoryCache projectDirectory, string content)
    {
        var parsed = JObject.Parse(content);
        if (parsed["packages"] is not JObject packages)
        {
            yield break;
        }

        var entries = new List<(string[] Segments, string Version)>();
        foreach (var prop in packages.Properties())
        {
            if (prop.Value is not JArray { Count: > 0 } packageInfo)
            {
                continue;
            }

            var version = ExtractPackageVersion(packageInfo[0].Type == JTokenType.String
                ? packageInfo[0].Value<string>()
                : null);
            if (version == null)
            {
                continue;
            }

            entries.Add((SplitInstallPath(prop.Name), version));
        }

        // Top level packages win over nested ones with same name, consumers expect unique names
        var known = new HashSet<string>();
        foreach (var (segments, version) in entries.OrderBy(e => e.Segments.Length))
        {
            var name = segments[^1];
            if (!known.Add(name)) continue;
            yield return new()
            {
                Name = name,
                Version = version,
                Path = PathUtils.Join(projectDirectory.FullPath,
                    string.Join('/', segments.Select(segment => "node_modules/" + segment)))
            };
        }
    }

    static string[] SplitInstallPath(string key)
    {
        var parts = key.Split('/');
        var segments = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith('@') && i + 1 < parts.Length)
            {
                segments.Add(parts[i] + "/" + parts[++i]);
            }
            else
            {
                segments.Add(parts[i]);
            }
        }

        return segments.ToArray();
    }

    public static string? ExtractPackageVersion(string? resolution)
    {
        if (string.IsNullOrEmpty(resolution)) return null;
        // Skip first character so scope '@' of "@scope/name@1.0.0" is not taken as separator
        var at = resolution.IndexOf('@', 1);
        if (at < 0) return null;
        var spec = resolution[(at + 1)..];
        if (spec.StartsWith("npm:", StringComparison.Ordinal))
        {
            spec = spec["npm:".Length..];
        }

        // github:, git+https:, workspace:, file:, link: and tarball URLs are not registry versions
        if (spec.Length == 0 || spec.IndexOfAny([':', '/', '#']) >= 0) return null;
        return spec;
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
