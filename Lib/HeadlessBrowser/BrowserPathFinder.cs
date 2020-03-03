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

        public static string GetBrowserPath(IFsAbstraction fsAbstraction)
        {
            var headlessOverride = Environment.GetEnvironmentVariable("BBBROWSER");
            if (headlessOverride != null)
                return headlessOverride;
            return GetBrowserPath(
                fsAbstraction.IsMac ? MacChromePaths : fsAbstraction.IsUnixFs ? LinuxChromePaths : WindowsBrowserPaths,
                fsAbstraction);
        }

        static string GetBrowserPath(string[] tryPaths, IFsAbstraction fsAbstraction)
        {
            foreach (var browserPath in tryPaths)
            {
                if (fsAbstraction.FileExists(browserPath))
                {
                    return browserPath;
                }
            }

            throw new Exception("Browser not found. Searched " + string.Join(';', tryPaths) +
                                ". Use BBBROWSER to specify path to your browser");
        }
    }
}
