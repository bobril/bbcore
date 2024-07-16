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

    public bool IsUsedInProject(IDirectoryCache projectDirectory)
    {
        return projectDirectory.TryGetChild("pnpm-lock.yaml") is IFileCache;
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
        var start = new ProcessStartInfo(_pnpmPath, aParams)
        {
            UseShellExecute = false,
            WorkingDirectory = dir,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };

        var process = Process.Start(start);
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

    public void Install(IDirectoryCache projectDirectory)
    {
        RunNpmWithParam(projectDirectory, "install");
    }

    void RunNpmWithParam(IDirectoryCache projectDirectory, string param)
    {
        var fullPath = projectDirectory.FullPath;
        var project = TSProject.Create(projectDirectory, _diskCache, _logger, null);
        project.LoadProjectJson(true, null);
        if (project.ProjectOptions.NpmRegistry != null)
        {
            if (!(projectDirectory.TryGetChild(".npmrc") is IFileCache))
            {
                _diskCache.FsAbstraction.WriteAllUtf8(
                    PathUtils.Join(fullPath, ".npmrc"),
                    "registry =" + project.ProjectOptions.NpmRegistry);
            }
        }

        var par = param;
        /* No such parameter exists in pnpm
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BBCoreNoLinks")))
        {
            par += " --no-bin-links";
        }
        */
        RunPnpm(fullPath, par);
    }

    public void UpgradeAll(IDirectoryCache projectDirectory)
    {
        _diskCache.UpdateIfNeeded(projectDirectory);
        RunNpmWithParam(projectDirectory, "update");
    }
    
    public void Upgrade(IDirectoryCache projectDirectory, string packageName)
    {
        RunNpmWithParam(projectDirectory, "update " + packageName);
    }

    public void Add(IDirectoryCache projectDirectory, string packageName, bool devDependency = false)
    {
        RunNpmWithParam(projectDirectory, "add "  + (devDependency ? "-D " : "") + packageName);
    }
}