using System;
using Lib.DiskCache;

namespace Lib.HeadlessBrowser
{
    public static class BrowserPathFinder
    {
        static readonly string[] WindowsBrowserPaths =
        {
            "c:/Program Files/Mozilla Firefox/firefox.exe",
            "c:/Program Files (x86)/Mozilla Firefox/firefox.exe",
            "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe"
        };

        static readonly string[] LinuxChromePaths =
        {
            "/usr/bin/google-chrome", "/opt/google/chrome/google-chrome", "/usr/bin/chromium",
            "/usr/bin/chromium-browser", "/snap/bin/chromium"
        };

        static readonly string[] MacChromePaths =
        {
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/Applications/Chromium.app/Contents/MacOS/Chromium"
        };

        public static string? GetBrowserPath(IFsAbstraction fsAbstraction, bool allowFirefox)
        {
            return GetBrowserPath(
                fsAbstraction.IsMac ? MacChromePaths : fsAbstraction.IsUnixFs ? LinuxChromePaths : WindowsBrowserPaths,
                fsAbstraction, allowFirefox);
        }

        static string? GetBrowserPath(string[] tryPaths, IFsAbstraction fsAbstraction, bool allowFirefox)
        {
            foreach (var browserPath in tryPaths)
            {
                if (!allowFirefox && browserPath.Contains("firefox", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (fsAbstraction.FileExists(browserPath))
                {
                    return browserPath;
                }
            }

            return null;
        }
    }
}
