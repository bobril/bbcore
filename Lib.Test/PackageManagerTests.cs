using System;
using System.IO;
using Lib.Registry;
using Lib.Utils.Logger;
using Xunit;

namespace Lib.Test;

public class PackageManagerTests
{
    [Theory]
    [InlineData("1.22.22", 1)]
    [InlineData("4.5.1\n", 4)]
    [InlineData("2.4.3-rc.1", 2)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("garbage", 1)]
    public void ParsesYarnMajorVersion(string? version, int expected)
    {
        Assert.Equal(expected, YarnNodePackageManager.ParseMajorVersion(version));
    }

    [Fact]
    public void YarnClassicKeepsLegacyArguments()
    {
        Assert.Equal("install --ignore-optional --no-emoji --non-interactive",
            YarnNodePackageManager.BuildYarnArguments(1, YarnNodePackageManager.YarnCommand.Install));
        Assert.Equal("install --frozen-lockfile --ignore-optional --no-emoji --non-interactive --no-bin-links",
            YarnNodePackageManager.BuildYarnArguments(1, YarnNodePackageManager.YarnCommand.Install, frozen: true,
                noBinLinks: true));
        Assert.Equal("upgrade bobril --no-emoji --non-interactive",
            YarnNodePackageManager.BuildYarnArguments(1, YarnNodePackageManager.YarnCommand.Upgrade, "bobril"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void YarnBerryDoesNotUseClassicOnlyArguments(int major)
    {
        Assert.Equal("install",
            YarnNodePackageManager.BuildYarnArguments(major, YarnNodePackageManager.YarnCommand.Install,
                noBinLinks: true));
        Assert.Equal("install --immutable",
            YarnNodePackageManager.BuildYarnArguments(major, YarnNodePackageManager.YarnCommand.Install,
                frozen: true));
        Assert.Equal("up \"*\"",
            YarnNodePackageManager.BuildYarnArguments(major, YarnNodePackageManager.YarnCommand.UpgradeAll));
        Assert.Equal("up bobril",
            YarnNodePackageManager.BuildYarnArguments(major, YarnNodePackageManager.YarnCommand.Upgrade, "bobril"));
        Assert.Equal("add bobril --dev",
            YarnNodePackageManager.BuildYarnArguments(major, YarnNodePackageManager.YarnCommand.Add, "bobril",
                true));
    }

    static (string exe, string args) ShellExit(int code) => OperatingSystem.IsWindows()
        ? ("cmd.exe", "/c exit " + code)
        : ("/bin/sh", "-c \"exit " + code + "\"");

    [Fact]
    public void RunThrowsOnNonZeroExitCode()
    {
        var (exe, args) = ShellExit(3);
        var e = Assert.Throws<PackageManagerException>(() =>
            PackageManagerProcess.Run("Fake", exe, Path.GetTempPath(), args, new DummyLogger()));
        Assert.Contains("exit code 3", e.Message);
    }

    [Fact]
    public void RunSucceedsOnZeroExitCode()
    {
        var (exe, args) = ShellExit(0);
        PackageManagerProcess.Run("Fake", exe, Path.GetTempPath(), args, new DummyLogger());
    }

    [Fact]
    public void RunThrowsWhenToolIsMissing()
    {
        Assert.Throws<PackageManagerException>(() =>
            PackageManagerProcess.Run("Fake", null, Path.GetTempPath(), "install", new DummyLogger()));
        Assert.Throws<PackageManagerException>(() =>
            PackageManagerProcess.Run("Fake", Path.Combine(Path.GetTempPath(), "does-not-exist-bb-pm"),
                Path.GetTempPath(), "install", new DummyLogger()));
    }
}
