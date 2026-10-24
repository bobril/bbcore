using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Lib.DiskCache;
using Lib.Utils;

namespace Lib.TSCompiler;

public static class TsrsLocator
{
    public const string BinaryEnvironmentVariable = "TSRS_BINARY";

    // Finds tsrs native binary: TSRS_BINARY, node_modules/@tsrs/<platform>, node_modules/tsrs launcher, PATH
    public static bool TryFind(string projectDirectory, IFsAbstraction fsAbstraction, out string tsrsExecutable)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(BinaryEnvironmentVariable);
        if (!string.IsNullOrEmpty(fromEnvironment) && File.Exists(fromEnvironment))
        {
            tsrsExecutable = fromEnvironment;
            return true;
        }

        var dir = PathUtils.Normalize(projectDirectory).TrimEnd('/');
        while (!string.IsNullOrEmpty(dir))
        {
            if (TryFindInNodeModules(PathUtils.Join(dir, "node_modules"), fsAbstraction, out tsrsExecutable))
                return true;

            var parentSpan = PathUtils.Parent(dir);
            var parent = parentSpan.IsEmpty ? "" : parentSpan.ToString();
            if (parent == dir)
                break;
            dir = parent;
        }

        return TryFindOnPath(out tsrsExecutable);
    }

    internal static bool TryFindInNodeModules(string nodeModulesDirectory, IFsAbstraction fsAbstraction,
        out string tsrsExecutable)
    {
        foreach (var package in PlatformPackageNames())
        {
            tsrsExecutable = PathUtils.Join(nodeModulesDirectory, "@tsrs/" + package + "/" + ExecutableName);
            if (fsAbstraction.FileExists(tsrsExecutable))
                return true;
        }

        if (!OperatingSystem.IsWindows())
        {
            tsrsExecutable = PathUtils.Join(nodeModulesDirectory, "tsrs/bin/tsrs");
            if (fsAbstraction.FileExists(tsrsExecutable))
                return true;
        }

        tsrsExecutable = "";
        return false;
    }

    static string ExecutableName => OperatingSystem.IsWindows() ? "tsrs.exe" : "tsrs";

    internal static IEnumerable<string> PlatformPackageNames()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
        if (OperatingSystem.IsMacOS())
        {
            yield return "darwin-" + arch;
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return "linux-" + arch + "-gnu";
            yield return "linux-" + arch + "-musl";
        }
        else if (OperatingSystem.IsWindows())
        {
            yield return "win32-" + arch;
            yield return "win32-" + arch + "-msvc";
        }
    }

    static bool TryFindOnPath(out string tsrsExecutable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                var fullPath = Path.Combine(dir, ExecutableName);
                if (File.Exists(fullPath))
                {
                    tsrsExecutable = fullPath;
                    return true;
                }
            }
        }

        tsrsExecutable = "";
        return false;
    }
}
