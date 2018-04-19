using System;
using System.Diagnostics;

namespace Lib.Utils.Notification
{
    public class NotificationManager
    {
        public void SendNotification(int errors, int warnings, double time)
        {
            void SendWindowsNotification()
            {
                const string SendNotificationPath = @"Resources\SendNotification.ps1";
                var p = Process.Start("Powershell.exe", $"{SendNotificationPath} {errors} {warnings} {time}");
                p.WaitForExit();
            }

            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    SendWindowsNotification();
                    break;
                case PlatformID.Unix:
                    break;
                case PlatformID.Xbox:
                    break;
                case PlatformID.MacOSX:
                    break;
                default:
                    break;
            }
        }
    }
}
