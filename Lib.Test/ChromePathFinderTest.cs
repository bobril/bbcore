using System;
using Lib.Chrome;
using Xunit;


namespace Lib.Test
{
    public class ChromePathFinderTest {


        [Fact]
        void ReturnsWindowsPathIfNotUnixFs()
        {
            var chromePath = ChromePathFinder.GetChromePath(false);
            Assert.Equal(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", chromePath);
        }

        [Fact]
        void ReturnsLinuxPathIfIsUnixFs() {
            var chromePath = ChromePathFinder.GetChromePath(true);
            Assert.Equal("/opt/google/chrome/google-chrome", chromePath);
        }
    }

}