using Microsoft.Toolkit.Uwp.Notifications;
using System;

namespace YouTube4KDownloader;

internal static class WindowsNotificationService
{
    public static bool ShowDownloadComplete(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
