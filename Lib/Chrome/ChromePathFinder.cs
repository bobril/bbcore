using System;

namespace Lib.Chrome {

    public static class ChromePathFinder {

        const string WindowsChromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        const string LinuxChromePath = "/opt/google/chrome/google-chrome";

        public static string GetChromePath(bool isUnixFs) {
            if (isUnixFs) {
                return LinuxChromePath;
            } else {
                return WindowsChromePath;
            }
        }
    }

}