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

namespace Lib.Registry
{
    public class YarnNodePackageManager : INodePackageManager
    {
        readonly IDiskCache _diskCache;
        readonly ILogger _logger;
        string _yarnPath;

        public YarnNodePackageManager(IDiskCache diskCache, ILogger logger)
        {
            _diskCache = diskCache;
            _logger = logger;
            _yarnPath = GetYarnPath();
        }

        static string GetYarnPath()
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
                .FirstOrDefault(File.Exists);
        }

        public bool IsAvailable => _yarnPath != null;

        public bool IsUsedInProject(IDirectoryCache projectDirectory)
        {
            return projectDirectory.TryGetChild("yarn.lock") is IFileCache;
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
            return nameWithVersion.Substring(0, nameWithVersion.LastIndexOf('@'));
        }

        public static IEnumerable<PackagePathVersion> ParseYarnLock(IDirectoryCache projectDirectory, string content)
        {
            var parsed = YarnLockParser.Parse(content);
            var known = new HashSet<string>();
            foreach (var pair in parsed)
            {
                var name = ExtractPackageName(pair.Key);
                if (!known.Add(name)) continue;
                yield return new PackagePathVersion
                {
                    Name = name,
                    Version = ((Dictionary<string, object>) pair.Value)["version"] as string,
                    Path = PathUtils.Join(projectDirectory.FullPath, "node_modules/" + name)
                };
            }
        }

        public void RunYarn(string dir, string aParams)
        {
            var start = new ProcessStartInfo(_yarnPath, aParams)
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
            RunYarnWithParam(projectDirectory, "install");
        }

        void RunYarnWithParam(IDirectoryCache projectDirectory, string param)
        {
            var fullPath = projectDirectory.FullPath;
            var project = TSProject.Create(projectDirectory, _diskCache, _logger, null);
            project.LoadProjectJson(true, null);
            if (project.ProjectOptions.NpmRegistry != null)
            {
                if (!(projectDirectory.TryGetChild(".npmrc") is IFileCache))
                {
                    File.WriteAllText(PathUtils.Join(fullPath, ".npmrc"),
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

        public void UpgradeAll(IDirectoryCache projectDirectory)
        {
            if (!IsUsedInProject(projectDirectory))
            {
                Install(projectDirectory);
                return;
            }

            RunYarnWithParam(projectDirectory, "upgrade");
        }

        public void Upgrade(IDirectoryCache projectDirectory, string packageName)
        {
            RunYarnWithParam(projectDirectory, "upgrade " + packageName);
        }

        public void Add(IDirectoryCache projectDirectory, string packageName, bool devDependency = false)
        {
            RunYarnWithParam(projectDirectory, "add " + packageName + (devDependency ? " --dev" : ""));
        }
    }
}
