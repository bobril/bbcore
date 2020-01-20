using System;
using System.IO;
using Lib.DiskCache;

namespace Lib.Chrome
{

    public static class ChromePathFinder
    {

        const string WindowsChromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        static readonly string[] LinuxChromePaths = { "/usr/bin/google-chrome", "/opt/google/chrome/google-chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser", "/snap/bin/chromium" };
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
            foreach (string path in LinuxChromePaths)
            {
                if (fsAbstraction.FileExists(path))
                {
                    return path;
                }
            }
            throw new Exception("Chrome not found. Install Google Chrome or Chromium.");
        }
    }

}
