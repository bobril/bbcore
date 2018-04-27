using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Lib.Utils.Notification
{
    public class NotificationManager
    {

        public void SendNotification(int errors, int warnings, double time)
        {
            string GetCurrentDirectory() => Path.GetDirectoryName(Assembly.GetAssembly(typeof(NotificationManager)).Location);

            void SendWindowsNotification()
            {
                const string SendNotificationRelativePath = @"Resources\SendNotification.ps1";
                string sendNotificationFullPath = Path.Combine(GetCurrentDirectory(), SendNotificationRelativePath);
                var p = Process.Start("Powershell.exe", $"-ExecutionPolicy ByPass -File {sendNotificationFullPath} -Errors {errors} -Warnings {warnings} -Time {time}");
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
