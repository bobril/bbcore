using Microsoft.DotNet.PlatformAbstractions;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Lib.Utils.Notification;

enum NotificationType
{
    Build,
    Tests,
}

public class NotificationParameters
{
    internal NotificationType Type { get; }

    // build
    public int Errors { get; }
    public int Warnings { get; }
    public double Time { get; }

    // tests
    public int Failed { get; }
    public int Skipped { get; }
    public int Total { get; }
    public double Duration { get; }

    public NotificationParameters(int errors, int warnings, double time)
    {
        Type = NotificationType.Build;
        Errors = errors;
        Warnings = warnings;
        Time = time;
    }

    public NotificationParameters(int failed, int skipped, int total, double duration)
    {
        Type = NotificationType.Tests;
        Failed = failed;
        Skipped = skipped;
        Total = total;
        Duration = duration;
    }

    public static NotificationParameters CreateBuildParameters(int errors, int warnings, double time) =>
        new NotificationParameters(errors, warnings, time);

    public static NotificationParameters CreateTestsParameters(int failed, int skipped, int total, double duration) =>
        new NotificationParameters(failed, skipped, total, duration);

    public string ToPowershellArguments()
    {
        switch (Type)
        {
            case NotificationType.Build:
                return $"-type build -Errors {Errors} -Warnings {Warnings} -Time {Time}";
            case NotificationType.Tests:
                return $"-type tests -Failed {Failed} -Skipped {Skipped} -Total {Total} -Duration {Duration}";
            default:
                throw new NotImplementedException();
        }
    }
}

public class NotificationManager
{
    static readonly Version WindowsMinimumVersionForToastNotificationManager = new Version(10, 0, 10240, 0);
    static readonly Version OSVersion;

    public static bool Enabled = true;
    static NotificationManager()
    {
        Version.TryParse(RuntimeEnvironment.OperatingSystemVersion, out OSVersion);
    }

    public void SendNotification(NotificationParameters parameters)
    {
        if (!Enabled) return;

        string GetCurrentDirectory() => Path.GetDirectoryName(Assembly.GetAssembly(typeof(NotificationManager))!.Location)!;

        void SendWindowsToastNotification()
        {
            const string sendNotificationRelativePath = @"Resources\SendNotification.ps1";
            string sendNotificationFullPath = Path.Combine(GetCurrentDirectory(), sendNotificationRelativePath);
            string args = parameters.ToPowershellArguments();
            var p = Process.Start("Powershell.exe", $"-ExecutionPolicy ByPass -File {sendNotificationFullPath} {args}");
            p.WaitForExit();
        }

        switch (RuntimeEnvironment.OperatingSystemPlatform)
        {
            case Platform.Windows:
                if (OSVersion != null && OSVersion >= WindowsMinimumVersionForToastNotificationManager)
                    SendWindowsToastNotification();
                break;
            default:
                break;
        }
    }
}