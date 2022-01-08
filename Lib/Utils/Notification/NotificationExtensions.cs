using Lib.Composition;

namespace Lib.Utils.Notification;

public static class NotificationExtensions
{
    public static NotificationParameters ToNotificationParameters(this TestResultsHolder results) =>
        NotificationParameters.CreateTestsParameters(results.TestsFailed, results.TestsSkipped, results.TotalTests, results.Duration * 0.001);
}