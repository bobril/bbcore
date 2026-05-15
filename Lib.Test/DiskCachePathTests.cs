using Lib.DiskCache;
using Xunit;

namespace Lib.Test;

public class DiskCachePathTests
{
    [Fact]
    public void WindowsDriveRootIsKeptAsRootDirectory()
    {
        var fs = new InMemoryFs(false);
        fs.WriteAllUtf8("C:/Dev/project/src/file.ts", "export const value = 1;");
        var diskCache = new DiskCache.DiskCache(fs, () => fs);

        var project = Assert.IsAssignableFrom<IDirectoryCache>(diskCache.TryGetItem("C:/Dev/project"));
        diskCache.UpdateIfNeeded(project);
        var file = Assert.IsAssignableFrom<IFileCache>(diskCache.TryGetItem("C:/Dev/project/src/file.ts"));

        Assert.Equal("C:/Dev/project/src/file.ts", file.FullPath);
        Assert.Equal("export const value = 1;", file.Utf8Content);
    }

    [Fact]
    public void WindowsBackslashPathsAreNormalizedBeforeCacheLookup()
    {
        var fs = new InMemoryFs(false);
        fs.WriteAllUtf8("C:/Dev/project/src/file.ts", "export const value = 1;");
        var diskCache = new DiskCache.DiskCache(fs, () => fs);

        var file = Assert.IsAssignableFrom<IFileCache>(diskCache.TryGetItem(@"C:\Dev\project\src\file.ts"));

        Assert.Equal("C:/Dev/project/src/file.ts", file.FullPath);
        Assert.Equal("export const value = 1;", file.Utf8Content);
    }
}
