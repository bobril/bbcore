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
        while (projectDirectory != null)
        {
            if (projectDirectory.IsFake && dc != null)
            {
                if (dc.FsAbstraction.FileExists(PathUtils.Join(projectDirectory.FullPath, "yarn.lock")))
                {
                    return true;
                }
            }

            if (projectDirectory.TryGetChild("yarn.lock") is IFileCache)
            {
                return true;
            }

            projectDirectory = projectDirectory.Parent;
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
        var start = new ProcessStartInfo(_yarnPath!, aParams)
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
        _logger.WriteLine(e.Data!);
    }

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        RunYarnWithParam(projectDirectory, "install --ignore-optional");
    }

    void RunYarnWithParam(IDirectoryCache projectDirectory, string param)
    {
        var fullPath = projectDirectory.FullPath;
        var project = TSProject.Create(projectDirectory, _diskCache, _logger, null)!;
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

        var par = param;
        par += " --no-emoji --non-interactive";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BBCoreNoLinks")))
        {
            par += " --no-bin-links";
        }

        RunYarn(fullPath, par);
    }

    public void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        if (!IsUsedInProject(projectDirectory, dc))
        {
            Install(projectDirectory, dc);
            return;
        }

        RunYarnWithParam(projectDirectory, "upgrade");
    }

    public void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName)
    {
        RunYarnWithParam(projectDirectory, "upgrade " + packageName);
    }

    public void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false)
    {
        RunYarnWithParam(projectDirectory, "add " + packageName + (devDependency ? " --dev" : ""));
    }
}
