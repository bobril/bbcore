using System;
using System.Collections.Generic;
using Lib.Chrome;
using Lib.DiskCache;
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
            var chromePath = ChromePathFinder.GetChromePath(new FakeFs(false, @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"));
            Assert.Equal(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", chromePath);
        }

        [Fact]
        void ReturnsLinuxPathIfIsUnixFs()
        {
            var chromePath = ChromePathFinder.GetChromePath(new FakeFs(true, "/opt/google/chrome/google-chrome"));
            Assert.Equal("/opt/google/chrome/google-chrome", chromePath);
        }

        [Fact]
        void ReturnsLinuxChromiumPathIfIsUnixFsAndChromeNotInstalled()
        {
            var chromePath = ChromePathFinder.GetChromePath(new FakeFs(true, "/usr/bin/chromium"));
            Assert.Equal("/usr/bin/chromium", chromePath);
        }

        [Fact]
        void ReturnsLinuxChromiumBrowserPathIfIsUnixFsAndChromeNotInstalled()
        {
            var chromePath = ChromePathFinder.GetChromePath(new FakeFs(true, "/usr/bin/chromium-browser"));
            Assert.Equal("/usr/bin/chromium-browser", chromePath);
        }

        [Fact]
        void ThrowsExceptionWhenChromeNorChromiumIsNotFound()
        {
            Exception ex = Assert.Throws<Exception>(() => ChromePathFinder.GetChromePath(new FakeFs(true)));
            Assert.Equal("Chrome not found. Install Google Chrome or Chromium.", ex.Message);
        }
    }

}