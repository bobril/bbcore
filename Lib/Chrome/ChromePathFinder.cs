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

        public static string GetChromePath(IFsAbstraction fsAbstraction)
        {
            if (fsAbstraction.IsMac)
            {
                return GetMacChromePath(fsAbstraction);
            }
            if (fsAbstraction.IsUnixFs)
            {
                return GetLinuxChromePath(fsAbstraction);
            }
            else
            {
                return WindowsChromePath;
            }
        }

        static string GetMacChromePath(IFsAbstraction fsAbstraction)
        {
            foreach (string chromePaths in MacChromePaths)
            {
                if (fsAbstraction.FileExists(chromePaths))
                {
                    return chromePaths;
                }
            }
            throw new Exception("Chrome not found. Install Google Chrome or Chromium.");
        }

        static string GetLinuxChromePath(IFsAbstraction fsAbstraction)
        {
            if (fsAbstraction.FileExists(LinuxChromePath))
            {
                return LinuxChromePath;
            }

            foreach (string chromiumPath in LinuxChromiumPaths)
            {
                if (fsAbstraction.FileExists(chromiumPath))
                {
                    return chromiumPath;
                }
            }
            throw new Exception("Chrome not found. Install Google Chrome or Chromium.");
        }
    }

}