#if ANDROID
using Android.App;
using Android.Content.PM;
using AndroidX.Core.App;

namespace MauiApp_Mobile.Services;

public static partial class DownloadNotificationService
{
    private const string ChannelId = "smarttour-downloads";
    private const int NotificationId = 3011;

    static partial void ShowProgressCore(string title, AudioDownloadProgressUpdate progress)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        EnsureChannel(activity);
        var percent = (int)Math.Round(progress.ProgressRatio * 100d);
        var speedText = progress.SpeedBytesPerSecond > 0
            ? $"{progress.SpeedBytesPerSecond / 1024d / 1024d:0.0} MB/s"
            : "-- MB/s";
        var detail = $"{percent}% • {speedText}";

        var notification = new NotificationCompat.Builder(activity, ChannelId)
            .SetContentTitle($"Đang tải: {title}")
            .SetContentText(detail)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOnlyAlertOnce(true)
            .SetOngoing(true)
            .SetProgress(100, Math.Clamp(percent, 0, 100), false)
            .Build();

        NotificationManagerCompat.From(activity).Notify(NotificationId, notification);
    }

    static partial void ShowSuccessCore(string title)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        EnsureChannel(activity);
        var notification = new NotificationCompat.Builder(activity, ChannelId)
            .SetContentTitle("Tải audio thành công")
            .SetContentText(title)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetAutoCancel(true)
            .Build();

        NotificationManagerCompat.From(activity).Notify(NotificationId, notification);
    }

    static partial void ShowFailureCore(string title, string message)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        EnsureChannel(activity);
        var notification = new NotificationCompat.Builder(activity, ChannelId)
            .SetContentTitle($"Tải thất bại: {title}")
            .SetContentText(message)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetAutoCancel(true)
            .Build();

        NotificationManagerCompat.From(activity).Notify(NotificationId, notification);
    }

    private static void EnsureChannel(Android.App.Activity activity)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = activity.GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
        if (manager?.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, "SmartTour Downloads", NotificationImportance.Low)
        {
            Description = "Thông báo tiến trình tải audio offline."
        };
        manager?.CreateNotificationChannel(channel);
    }
}
#endif
