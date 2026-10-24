using System.Linq;
using Lib.Utils.Logger;

namespace Lib.TSCompiler;

// Downloads tsrs native binary for current platform from npm into bbcore tools directory
public static class TsrsInstaller
{
    public const string DefaultVersion = "0.18.0-ts7.1.0-dev.20260929";

    static NativePackage Package => new("tsrs", "@tsrs/",
        TsrsLocator.PlatformPackageNames(NativePackageInstaller.IsMusl()).ToArray(), TsrsLocator.ExecutableName,
        "tsrs-", "tsrs");

    public static string InstallDirectory(string toolsPath, string version) =>
        NativePackageInstaller.InstallDirectory(toolsPath, Package, version);

    public static bool TryInstall(string toolsPath, string version, ILogger logger, out string tsrsExecutable,
        out string failureReason) =>
        NativePackageInstaller.TryInstall(toolsPath, Package, version, logger, out tsrsExecutable, out failureReason);
}
