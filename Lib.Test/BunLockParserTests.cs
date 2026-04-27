using System.Linq;
using Lib.DiskCache;
using Lib.Registry;
using Xunit;

namespace Lib.Test;

public class BunLockParserTests
{
    [Fact]
    public void BunParserTest()
    {
        const string source = """
                              {
                                "lockfileVersion": 1,
                                "packages": {
                                  "bobril": ["bobril@npm:20.7.1", {}, "sha512-a"],
                                  "@types/node": ["@types/node@npm:20.11.30", {}, "sha512-b"],
                                  "typescript": ["typescript@npm:5.5.3", {}, "sha512-c"]
                                }
                              }
                              """;

        var fs = new InMemoryFs();
        fs.WriteAllUtf8("proj/bun.lock", source);
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var directory = dc.TryGetItem("proj") as IDirectoryCache;
        var result = new BunNodePackageManager(dc, null).GetLockedDependencies(directory).ToArray();
        Assert.Equal(3, result.Length);
        Assert.Equal("bobril", result[0].Name);
        Assert.Equal("20.7.1", result[0].Version);
        Assert.Equal(directory.FullPath + "/node_modules/bobril", result[0].Path);
        Assert.Equal("@types/node", result[1].Name);
        Assert.Equal("20.11.30", result[1].Version);
        Assert.Equal(directory.FullPath + "/node_modules/@types/node", result[1].Path);
        Assert.Equal("typescript", result[2].Name);
        Assert.Equal("5.5.3", result[2].Version);
        Assert.Equal(directory.FullPath + "/node_modules/typescript", result[2].Path);
    }

    [Fact]
    public void DetectsBunLockFilesInParentDirectories()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("proj/bun.lockb", "");
        fs.WriteAllUtf8("proj/src/package.json", "{}");
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var directory = dc.TryGetItem("proj/src") as IDirectoryCache;

        Assert.True(new BunNodePackageManager(dc, null).IsUsedInProject(directory, dc));
    }
}
