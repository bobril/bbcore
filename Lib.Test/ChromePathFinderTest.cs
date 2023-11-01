using Lib.DiskCache;
using Lib.HeadlessBrowser;
using Shared.DiskCache;
using Xunit;

namespace Lib.Test;

public class ChromePathFinderTest
{


    [Fact]
    void ReturnsWindowsPathIfNotUnixFs()
    {
        var chromePath = BrowserPathFinder.GetBrowserPath(new FakeFs(false, @"C:/Program Files (x86)/Google/Chrome/Application/chrome.exe"), true);
        Assert.Equal(@"C:/Program Files (x86)/Google/Chrome/Application/chrome.exe", chromePath);
    }

    [Fact]
    void ReturnsLinuxPathIfIsUnixFs()
    {
        var chromePath = BrowserPathFinder.GetBrowserPath(new FakeFs(true, "/opt/google/chrome/google-chrome"), true);
        Assert.Equal("/opt/google/chrome/google-chrome", chromePath);
    }

    [Fact]
    void ReturnsLinuxChromiumPathIfIsUnixFsAndChromeNotInstalled()
    {
        var chromePath = BrowserPathFinder.GetBrowserPath(new FakeFs(true, "/usr/bin/chromium"), true);
        Assert.Equal("/usr/bin/chromium", chromePath);
    }

    [Fact]
    void ReturnsLinuxChromiumBrowserPathIfIsUnixFsAndChromeNotInstalled()
    {
        var chromePath = BrowserPathFinder.GetBrowserPath(new FakeFs(true, "/usr/bin/chromium-browser"), true);
        Assert.Equal("/usr/bin/chromium-browser", chromePath);
    }

    [Fact]
    void ReturnsNullWhenChromeNorChromiumIsNotFound()
    {
        Assert.Null(BrowserPathFinder.GetBrowserPath(new FakeFs(true), true));
    }
}