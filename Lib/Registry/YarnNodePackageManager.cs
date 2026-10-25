using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lib.DiskCache;
using Lib.TSCompiler;
using Lib.Utils;
using Lib.Utils.Logger;

namespace Lib.Registry;

public class YarnNodePackageManager : INodePackageManager
{
    readonly IDiskCache _diskCache;
    readonly ILogger _logger;
    string? _yarnPath;

    public YarnNodePackageManager(IDiskCache diskCache, ILogger logger)
    {
        _diskCache = diskCache;
        _logger = logger;
        _yarnPath = GetYarnPath();
    }

    private string? GetYarnPath()
    {
        var yarnExecName = "yarn";
        if (!PathUtils.IsUnixFs)
        {
            yarnExecName += ".cmd";
        }

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(p => PathUtils.Join(PathUtils.Normalize(new DirectoryInfo(p).FullName), yarnExecName))
            .FirstOrDefault(_diskCache.FsAbstraction.FileExists);
    }

    public bool IsAvailable => _yarnPath != null;

    public bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        IDirectoryCache? dir = projectDirectory;
        while (dir != null)
        {
            if (dir.IsFake && dc != null)
            {
                if (dc.FsAbstraction.FileExists(PathUtils.Join(dir.FullPath, "yarn.lock")))
                {
                    return true;
                }
            }

            if (dir.TryGetChild("yarn.lock") is IFileCache)
            {
                return true;
            }

            dir = dir.Parent;
        }

        return false;
    }

    public IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory)
    {
        var yarnLockFile = projectDirectory.TryGetChild("yarn.lock") as IFileCache;
        if (yarnLockFile == null)
        {
            return Enumerable.Empty<PackagePathVersion>();
        }

        return ParseYarnLock(projectDirectory, yarnLockFile.Utf8Content);
    }

    public static string ExtractPackageName(string nameWithVersion)
    {
        var protocolIndex = nameWithVersion.IndexOf("@npm:", StringComparison.Ordinal);
        if (protocolIndex > 0)
        {
            return nameWithVersion[..protocolIndex];
        }

        var patchProtocolIndex = nameWithVersion.IndexOf("@patch:", StringComparison.Ordinal);
        if (patchProtocolIndex > 0)
        {
            return nameWithVersion[..patchProtocolIndex];
        }

        return nameWithVersion[..nameWithVersion.LastIndexOf('@')];
    }

    public static IEnumerable<PackagePathVersion> ParseYarnLock(IDirectoryCache projectDirectory, string content)
    {
        if (content.Contains("__metadata:"))
        {
            foreach (var dependency in ParseModernYarnLock(projectDirectory, content))
            {
                yield return dependency;
            }

            yield break;
        }

        var parsed = YarnLockParser.Parse(content);
        var known = new HashSet<string>();
        foreach (var pair in parsed)
        {
            if (!pair.Key.Contains('@') ||
                pair.Value is not Dictionary<string, object> packageInfo ||
                !packageInfo.ContainsKey("version"))
            {
                continue;
            }

            var name = ExtractPackageName(pair.Key);
            if (!known.Add(name)) continue;
            yield return new()
            {
                Name = name,
                Version = packageInfo["version"].ToString()!,
                Path = PathUtils.Join(projectDirectory.FullPath, "node_modules/" + name)
            };
        }
    }

    static IEnumerable<PackagePathVersion> ParseModernYarnLock(IDirectoryCache projectDirectory, string content)
    {
        string? currentKey = null;
        var known = new HashSet<string>();

        foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            if (!char.IsWhiteSpace(line[0]))
            {
                currentKey = NormalizeModernYarnKey(line);
                continue;
            }

            if (currentKey == null)
            {
                continue;
            }

            var trimmed = line.TrimStart();
            const string versionPrefix = "version:";
            if (!trimmed.StartsWith(versionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!currentKey.Contains('@'))
            {
                continue;
            }

            var name = ExtractPackageName(currentKey);
            if (!known.Add(name))
            {
                continue;
            }

            var version = trimmed[versionPrefix.Length..].Trim().Trim('"');
            yield return new()
            {
                Name = name,
                Version = version,
                Path = PathUtils.Join(projectDirectory.FullPath, "node_modules/" + name)
            };
        }
    }

    static string NormalizeModernYarnKey(string line)
    {
        var key = line.Trim();
        if (key.EndsWith(':'))
        {
            key = key[..^1];
        }

        return key.Trim('"');
    }

    public void RunYarn(string dir, string aParams)
    {
        _logger.Info("Yarn " + aParams);
        PackageManagerProcess.Run("Yarn", _yarnPath, dir, aParams, _logger);
    }

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc, bool frozen = false)
    {
        RunYarnCommand(projectDirectory, YarnCommand.Install, frozen: frozen);
    }

    public enum YarnCommand
    {
        Install,
        UpgradeAll,
        Upgrade,
        Add
    }

    // Yarn 2+ (Berry) rejects Yarn 1 only options like --ignore-optional, --no-emoji, --non-interactive or
    // --frozen-lockfile, so arguments depend on major version of Yarn used in project directory.
    public static string BuildYarnArguments(int majorVersion, YarnCommand command, string? packageName = null,
        bool devDependency = false, bool frozen = false, bool noBinLinks = false)
    {
        var berry = majorVersion >= 2;
        var par = command switch
        {
            YarnCommand.Install => berry
                ? frozen ? "install --immutable" : "install"
                : frozen ? "install --frozen-lockfile --ignore-optional" : "install --ignore-optional",
            YarnCommand.UpgradeAll => berry ? "up \"*\"" : "upgrade",
            YarnCommand.Upgrade => (berry ? "up " : "upgrade ") + packageName,
            YarnCommand.Add => "add " + packageName + (devDependency ? " --dev" : ""),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        if (berry) return par;
        par += " --no-emoji --non-interactive";
        if (noBinLinks)
        {
            par += " --no-bin-links";
        }

        return par;
    }

    public static int ParseMajorVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return 1;
        var trimmed = version.Trim();
        var dot = trimmed.IndexOf('.');
        return int.TryParse(dot < 0 ? trimmed : trimmed[..dot], out var major) ? major : 1;
    }

    void RunYarnCommand(IDirectoryCache projectDirectory, YarnCommand command, string? packageName = null,
        bool devDependency = false, bool frozen = false)
    {
        var fullPath = projectDirectory.FullPath;
        ProjectNpmRc.EnsureRegistry(projectDirectory, _diskCache, _logger);
        // Version must be detected in project directory, because packageManager field could select different Yarn
        var majorVersion = _yarnPath == null
            ? 1
            : ParseMajorVersion(PackageManagerProcess.RunAndCapture(_yarnPath, fullPath, "--version"));
        var noBinLinks = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BBCoreNoLinks"));
        RunYarn(fullPath, BuildYarnArguments(majorVersion, command, packageName, devDependency, frozen, noBinLinks));
    }

    public void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        if (!IsUsedInProject(projectDirectory, dc))
        {
            Install(projectDirectory, dc);
            return;
        }

        RunYarnCommand(projectDirectory, YarnCommand.UpgradeAll);
    }

    public void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName)
    {
        RunYarnCommand(projectDirectory, YarnCommand.Upgrade, packageName);
    }

    public void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false)
    {
        RunYarnCommand(projectDirectory, YarnCommand.Add, packageName, devDependency);
    }
}
