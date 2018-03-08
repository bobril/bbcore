using System;
using System.IO;
using Lib.DiskCache;

namespace Lib.Chrome
{

    public static class ChromePathFinder
    {

        const string WindowsChromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        const string LinuxChromePath = "/opt/google/chrome/google-chrome";
        static readonly string[] LinuxChromiumPaths = { "/usr/bin/chromium", "/usr/bin/chromium-browser" };
        static readonly string[] MacChromePaths = { "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", "/Applications/Chromium.app/Contents/MacOS/Chromium" };

        public static string GetChromePath(IFsAbstraction fsAbstratction)
        {
            if (fsAbstratction.IsMac)
            {
                return GetMacChromePath(fsAbstratction);
            }
            if (fsAbstratction.IsUnixFs)
            {
                return GetLinuxChromePath(fsAbstratction);
            }
            else
            {
                return WindowsChromePath;
            }
        }

        static string GetMacChromePath(IFsAbstraction fsAbstratction)
        {
            foreach (string chromePaths in MacChromePaths)
            {
                if (fsAbstratction.FileExists(chromePaths))
                {
                    return chromePaths;
                }
            }
            throw new Exception("Chrome not found. Install Google Chrome or Chromium.");
        }

        static string GetLinuxChromePath(IFsAbstraction fsAbstratction)
        {
            if (fsAbstratction.FileExists(LinuxChromePath))
            {
                return LinuxChromePath;
            }

            foreach (string chromiumPath in LinuxChromiumPaths)
            {
                if (fsAbstratction.FileExists(chromiumPath))
                {
                    return chromiumPath;
                }
            }
            throw new Exception("Chrome not found. Install Google Chrome or Chromium.");
        }
    }

}