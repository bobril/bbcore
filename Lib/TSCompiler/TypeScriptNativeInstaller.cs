using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Lib.DiskCache;
using Lib.Utils;
using Lib.Utils.Logger;
using Newtonsoft.Json.Linq;

namespace Lib.TSCompiler;

// TypeScript 7 native compiler (`tsc` from @typescript/typescript-<platform>), used for typecheck and through its API
// also for transpilation. Version 7.0 API cannot transpile, so 7.1 is minimum.
public static class TypeScriptNativeInstaller
{
    // TypeScript build ported by tsrs TsrsInstaller.DefaultVersion, so fallback from tsrs gives same results
    public const string DefaultVersion = "7.1.0-dev.20260930.4";

    static readonly SemanticVersioning.Version MinimumVersion = new("7.1.0-0");

    public static string ExecutableName => OperatingSystem.IsWindows() ? "tsc.exe" : "tsc";

    static NativePackage Package => new("TypeScript", "@typescript/typescript-", PlatformNames().ToArray(),
        "lib/" + ExecutableName, "tsc-", "Version 7");

    public static bool IsNativeVersion(string? version) =>
        TryParse(version, out var parsed) && parsed.Major >= 7;

    public static bool SupportsApi(string? version) =>
        TryParse(version, out var parsed) && parsed >= MinimumVersion;

    static bool TryParse(string? version, out SemanticVersioning.Version parsed)
    {
        parsed = null!;
        if (string.IsNullOrEmpty(version)) return false;
        try
        {
            parsed = new(version, loose: true);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static IEnumerable<string> PlatformNames()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };
        if (OperatingSystem.IsMacOS())
            yield return "darwin-" + arch;
        else if (OperatingSystem.IsLinux())
            yield return "linux-" + arch;
        else if (OperatingSystem.IsWindows())
            yield return "win32-" + arch;
    }

    public static string InstallDirectory(string toolsPath, string version) =>
        NativePackageInstaller.InstallDirectory(toolsPath, Package, version);

    public static bool TryInstall(string toolsPath, string version, ILogger logger, out string tscExecutable,
        out string failureReason)
    {
        if (!SupportsApi(version))
        {
            tscExecutable = "";
            failureReason = "TypeScript " + version + " is not supported, native TypeScript needs version 7.1 or newer";
            return false;
        }

        return NativePackageInstaller.TryInstall(toolsPath, Package, version, logger, out tscExecutable,
            out failureReason);
    }

    // Project local `typescript` 7.1+ package in node_modules (searched also in parent directories)
    public static bool TryFindInProject(string projectDirectory, IFsAbstraction fsAbstraction,
        out string tscExecutable, out string version)
    {
        var dir = PathUtils.Normalize(projectDirectory).TrimEnd('/');
        while (!string.IsNullOrEmpty(dir))
        {
            var packageJson = PathUtils.Join(dir, "node_modules/typescript/package.json");
            if (fsAbstraction.FileExists(packageJson))
            {
                version = ReadVersion(fsAbstraction, packageJson);
                if (!SupportsApi(version))
                    break;
                foreach (var platform in PlatformNames())
                {
                    foreach (var nodeModules in new[] { "node_modules", "node_modules/typescript/node_modules" })
                    {
                        tscExecutable = PathUtils.Join(dir,
                            nodeModules + "/@typescript/typescript-" + platform + "/lib/" + ExecutableName);
                        if (fsAbstraction.FileExists(tscExecutable))
                            return true;
                    }
                }

                break;
            }

            var parentSpan = PathUtils.Parent(dir);
            var parent = parentSpan.IsEmpty ? "" : parentSpan.ToString();
            if (parent == dir)
                break;
            dir = parent;
        }

        tscExecutable = "";
        version = "";
        return false;
    }

    static string ReadVersion(IFsAbstraction fsAbstraction, string packageJson)
    {
        try
        {
            return JObject.Parse(fsAbstraction.ReadAllUtf8(packageJson))["version"]?.Value<string>() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
