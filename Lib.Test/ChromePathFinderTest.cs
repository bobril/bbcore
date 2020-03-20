using System;
using System.Collections.Generic;
using Lib.DiskCache;
using Lib.HeadlessBrowser;
using Xunit;


namespace Lib.Test
{
    class FakeFs : IFsAbstraction
    {
        bool _isUnix;
        bool _isMac;
        string _chromPath;
        public FakeFs(bool isUnix, string chromePath = null, bool isMac = false)
        {
            _isUnix = isUnix;
            _isMac = isMac;
            _chromPath = chromePath;
        }

        public bool IsMac => _isMac;

        public bool IsUnixFs => _isUnix;

        public bool FileExists(string path)
        {
            if (_chromPath == null) {
                return false;
            }
            return path == _chromPath;
        }

        public IReadOnlyList<FsItemInfo> GetDirectoryContent(string path)
        {
            throw new NotImplementedException();
        }

        public FsItemInfo GetItemInfo(ReadOnlySpan<char> path)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadAllBytes(string path)
        {
            throw new NotImplementedException();
        }

        public string ReadAllUtf8(string path)
        {
            throw new NotImplementedException();
        }
    }


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

}
