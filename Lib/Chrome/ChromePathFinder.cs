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

        public static string GetChromePath(IFsAbstraction fsAbstratction)
        {
            if (fsAbstratction.IsUnixFs)
            {
                return GetLinuxChromePath(fsAbstratction);
            }
            else
            {
                return WindowsChromePath;
            }
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