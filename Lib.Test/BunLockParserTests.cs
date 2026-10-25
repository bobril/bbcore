using System.Linq;
using Lib.DiskCache;
using Lib.Registry;
using Lib.Utils.Logger;
using Xunit;

namespace Lib.Test;

public class BunLockParserTests
{
    // Trimmed bun.lock written by Bun 1.4 (dependencies send, @types/node, github, workspace and file packages)
    const string RealBunLock = """
                              {
                                "lockfileVersion": 2,
                                "configVersion": 1,
                                "workspaces": {
                                  "": {
                                    "name": "p",
                                    "dependencies": {
                                      "@types/node": "20.11.30",
                                      "is-number": "github:jonschlinkert/is-number#7.0.0",
                                      "lib": "workspace:*",
                                      "local-pkg": "file:./local",
                                      "send": "0.19.0",
                                    },
                                  },
                                  "packages/lib": {
                                    "name": "lib",
                                    "version": "0.1.0",
                                  },
                                },
                                "packages": {
                                  "@types/node": ["@types/node@20.11.30", "", { "dependencies": { "undici-types": "~5.26.4" } }, "sha512-dHM6ZxwlmuZaRmUPfv1p+KrdD1Dci04FbdEm/9wEMouFqxYoFl5aMkt0VMAUtYRQDyYvD41WJLukhq/ha3YuTw=="],

                                  "debug": ["debug@2.6.9", "", { "dependencies": { "ms": "2.0.0" } }, "sha512-bC7ElrdJaJnPbAP+1EotYvqZsb3ecl5wi6Bfi6BJTUcNowp6cvspg0jXznRTKDjm/E7AdgFBVeAPVMNcKGsHMA=="],










                                  "is-number": ["is-number@github:jonschlinkert/is-number#98e8ff1", {}, "jonschlinkert-is-number-98e8ff1", "sha512-VRiId6WxXNzVNsf8kootsozLJ3nE+6fNmpwihMjEudNZbnSjqsRZDgvAIgzdsl5qk714XNJqG6QzO4cQ/WhULQ=="],

                                  "lib": ["lib@workspace:packages/lib"],

                                  "local-pkg": ["local-pkg@file:local", {}],


                                  "ms": ["ms@2.1.3", "", {}, "sha512-6FlzubTLZG3J2a/NVCAleEhjzq5oxgHyaCU9yYXvcLsvoVaHJq/s5xXI6/XXP6tz7R9xAOtHnSO/tXtF3WRTlA=="],







                                  "undici-types": ["undici-types@5.26.5", "", {}, "sha512-JlCMO+ehdEIKqlFxk6IfVoAUVmgz7cU7zD/h9XZ0qzeosSHmUJVOzSQvvYSYWXkFXC+IfLKSIffhv0sVZup6pA=="],

                                  "debug/ms": ["ms@2.0.0", "", {}, "sha512-Tpp60P6IUJDTuOq/5Z8cdskzJujfwqfOTkrwIwj7IRISpnkJnT6SyJ4PCPnGMoFjC9ddhal5KVIYtAt97ix05A=="],
                                }
                              }
                              """;

    static IDirectoryCache CreateProject(string bunLock, out DiskCache.DiskCache dc)
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("proj/bun.lock", bunLock);
        dc = new(fs, () => fs);
        var directory = dc.TryGetItem("proj") as IDirectoryCache;
        Assert.NotNull(directory);
        return directory;
    }

    [Fact]
    public void ParsesRealBunLock()
    {
        var directory = CreateProject(RealBunLock, out var dc);
        var result = new BunNodePackageManager(dc, new DummyLogger()).GetLockedDependencies(directory)
            .ToDictionary(p => p.Name);

        Assert.Equal(["@types/node", "debug", "ms", "undici-types"], result.Keys.Order());
        Assert.Equal("20.11.30", result["@types/node"].Version);
        Assert.Equal(directory.FullPath + "/node_modules/@types/node", result["@types/node"].Path);
        Assert.Equal("2.6.9", result["debug"].Version);
        // Top level ms wins over nested debug/ms 2.0.0
        Assert.Equal("2.1.3", result["ms"].Version);
        Assert.Equal(directory.FullPath + "/node_modules/ms", result["ms"].Path);
        Assert.Equal("5.26.5", result["undici-types"].Version);
    }

    [Fact]
    public void NestedPackagesGetNestedPaths()
    {
        const string source = """
                              {
                                "lockfileVersion": 1,
                                "packages": {
                                  "send/mime": ["mime@1.6.0", "", {}, "sha512-a"],
                                  "@babel/core/@babel/types": ["@babel/types@7.25.0", "", {}, "sha512-b"],
                                  "@babel/core/semver": ["semver@6.3.1", "", {}, "sha512-c"],
                                }
                              }
                              """;
        var directory = CreateProject(source, out var dc);
        var result = new BunNodePackageManager(dc, new DummyLogger()).GetLockedDependencies(directory)
            .ToDictionary(p => p.Name);

        Assert.Equal(directory.FullPath + "/node_modules/send/node_modules/mime", result["mime"].Path);
        Assert.Equal(directory.FullPath + "/node_modules/@babel/core/node_modules/@babel/types",
            result["@babel/types"].Path);
        Assert.Equal("7.25.0", result["@babel/types"].Version);
        Assert.Equal(directory.FullPath + "/node_modules/@babel/core/node_modules/semver", result["semver"].Path);
    }

    [Theory]
    [InlineData("debug@2.6.9", "2.6.9")]
    [InlineData("@types/node@20.11.30", "20.11.30")]
    [InlineData("typescript@npm:5.5.3", "5.5.3")]
    [InlineData("react@19.0.0-rc.1", "19.0.0-rc.1")]
    [InlineData("is-number@github:jonschlinkert/is-number#98e8ff1", null)]
    [InlineData("lib@workspace:packages/lib", null)]
    [InlineData("local-pkg@file:local", null)]
    [InlineData("pkg@https://example.com/pkg.tgz", null)]
    [InlineData("noversion", null)]
    [InlineData("", null)]
    public void ExtractsRegistryVersionsOnly(string resolution, string? expected)
    {
        Assert.Equal(expected, BunNodePackageManager.ExtractPackageVersion(resolution));
    }

    [Fact]
    public void DiffChangeLogAcceptsParsedBunLock()
    {
        var directory = CreateProject(RealBunLock, out var dc);
        var deps = new BunNodePackageManager(dc, new DummyLogger()).GetLockedDependencies(directory).ToArray();
        Assert.Empty(new DiffChangeLog(dc).Generate(deps, deps));
    }

    [Fact]
    public void DetectsBunLockFilesInParentDirectories()
    {
        var fs = new InMemoryFs();
        fs.WriteAllUtf8("proj/bun.lockb", "");
        fs.WriteAllUtf8("proj/src/package.json", "{}");
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var directory = dc.TryGetItem("proj/src") as IDirectoryCache;
        Assert.NotNull(directory);

        Assert.True(new BunNodePackageManager(dc, new DummyLogger()).IsUsedInProject(directory, dc));
    }
}
