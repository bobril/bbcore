using System.Linq;
using Lib.DiskCache;
using Lib.Registry;
using Xunit;

namespace Lib.Test;

public class PnpmLockParserTests
{
    [Fact]
    public void PnpmParserTest()
    {
        const string source = """
                              lockfileVersion: '9.0'

                              settings:
                                autoInstallPeers: true
                                excludeLinksFromLockfile: false

                              importers:
                              
                                .:
                                  dependencies:
                                    bobril:
                                      specifier: '*'
                                      version: 20.7.1
                                    bobril-g11n:
                                      specifier: '*'
                                      version: 5.1.1
                                  devDependencies:
                                    typescript:
                                      specifier: '*'
                                      version: 5.5.3

                              packages:
                              
                                bobril-g11n@5.1.1:
                                  resolution: {integrity: sha512-c1PdUCIIxLXnu1FsYAxhezDppHjohjTjmrZN9aCRD5LPtFrHugLbtMXOh6y4GEpD1ZBJbM2Dvf5b4eccRgARlQ==}
                              
                                bobril@20.7.1:
                                  resolution: {integrity: sha512-KiyG/zysyze3w87O/6eHl1XmLPMkZdAW6Pt4y5k5w2ZtORsLKLyB+yEWvTHQl/XFE6OucwxexZplwpHJO6Wlwg==}
                              
                                moment@2.30.1:
                                  resolution: {integrity: sha512-uEmtNhbDOrWPFS+hdjFCBfy9f2YoyzRpwcl+DqpC6taX21FzsTLQVbMV/W7PzNSX6x/bhC1zA3c2UQ5NzH6how==}
                              
                                typescript@5.5.3:
                                  resolution: {integrity: sha512-/hreyEujaB0w76zKo6717l3L0o/qEUtRgdvUBvlkhoWeOVMjMuHNHk0BRBzikzuGDqNmPQbg5ifMEqsHLiIUcQ==}
                                  engines: {node: '>=14.17'}
                                  hasBin: true

                              snapshots:
                              
                                bobril-g11n@5.1.1:
                                  dependencies:
                                    bobril: 20.7.1
                                    moment: 2.30.1
                              
                                bobril@20.7.1: {}
                              
                                moment@2.30.1: {}
                              
                                typescript@5.5.3: {}

                              """;

        var fs = new InMemoryFs();
        fs.WriteAllUtf8("proj/pnpm-lock.yaml", source);
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var directory = dc.TryGetItem("proj") as IDirectoryCache;
        var result = new PnpmNodePackageManager(dc, null).GetLockedDependencies(directory).ToArray();
        Assert.Equal(3, result.Length);
        Assert.Equal("bobril", result[0].Name);
        Assert.Equal("20.7.1", result[0].Version);
        Assert.Equal(directory.FullPath + "/node_modules/bobril", result[0].Path);
        Assert.Equal("bobril-g11n", result[1].Name);
        Assert.Equal("5.1.1", result[1].Version);
        Assert.Equal(directory.FullPath + "/node_modules/bobril-g11n", result[1].Path);
        Assert.Equal("typescript", result[2].Name);
        Assert.Equal("5.5.3", result[2].Version);
        Assert.Equal(directory.FullPath + "/node_modules/typescript", result[2].Path);
    }
}