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

public class NpmNodePackageManager : INodePackageManager
{
    readonly IDiskCache _diskCache;
    readonly ILogger _logger;
    string _npmPath;

    public NpmNodePackageManager(IDiskCache diskCache, ILogger logger)
    {
        _diskCache = diskCache;
        _logger = logger;
        _npmPath = GetNpmPath();
    }

    private string GetNpmPath()
    {
        var npmExecName = "npm";
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

    public bool IsAvailable => _npmPath != null;

    public bool IsUsedInProject(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        return projectDirectory.TryGetChild("package-lock.json") is IFileCache;
    }

    public IEnumerable<PackagePathVersion> GetLockedDependencies(IDirectoryCache projectDirectory)
    {
        var lockFile = projectDirectory.TryGetChild("package-lock.json") as IFileCache;
        if (lockFile == null)
        {
            yield break;
        }

        var parsed = JObject.Parse(lockFile.Utf8Content);
        foreach (var prop in parsed["packages"]!.Children<JProperty>())
        {
            if (!prop.Name.StartsWith("node_modules/"))
            {
                continue;
            }
            yield return new PackagePathVersion
            {
                Name = prop.Name["node_modules/".Length..],
                Version = ((JObject) prop.Value)["version"]!.Value<string>()!,
                Path = PathUtils.Join(projectDirectory.FullPath, prop.Name)
            };
        }
    }

    public void RunNpm(string dir, string aParams)
    {
        _logger.Info("Npm " + aParams);
        var start = new ProcessStartInfo(_npmPath, aParams)
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

    public void Install(IDirectoryCache projectDirectory, IDiskCache? dc)
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
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BBCoreNoLinks")))
        {
            par += " --no-bin-links";
        }

        RunNpm(fullPath, par);
    }

    public void UpgradeAll(IDirectoryCache projectDirectory, IDiskCache? dc)
    {
        _diskCache.UpdateIfNeeded(projectDirectory);
        _diskCache.FsAbstraction.Delete(PathUtils.Join(projectDirectory.FullPath, "package-lock.json"));
        var dirToDelete = projectDirectory.TryGetChild("node_modules") as IDirectoryCache;
        RecursiveDelete(dirToDelete);
        Install(projectDirectory, dc);
    }

    void RecursiveDelete(IDirectoryCache dirToDelete)
    {
        if (dirToDelete == null)
            return;
        _diskCache.UpdateIfNeeded(dirToDelete);
        foreach (var item in dirToDelete)
        {
            if (item is IFileCache)
            {
                try
                {
                    _diskCache.FsAbstraction.Delete(item.FullPath);
                }
                catch (Exception)
                {
                    // ignored
                }

                continue;
            }

            var dir = item as IDirectoryCache;
            if (dir != null && dir.IsLink)
            {
                try
                {
                    Directory.Delete(dir.FullPath, false);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            RecursiveDelete(dir);
        }

        try
        {
            Directory.Delete(dirToDelete.FullPath, true);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public void Upgrade(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName)
    {
        RunNpmWithParam(projectDirectory, "update " + packageName);
    }

    public void Add(IDirectoryCache projectDirectory, IDiskCache? dc, string packageName, bool devDependency = false)
    {
        RunNpmWithParam(projectDirectory, "install " + packageName + (devDependency ? " --save-dev" : " --save"));
    }
}