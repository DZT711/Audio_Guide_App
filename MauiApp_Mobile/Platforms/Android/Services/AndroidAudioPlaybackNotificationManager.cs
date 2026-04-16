#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.App;

namespace MauiApp_Mobile.Services;

internal sealed class AndroidAudioPlaybackNotificationManager
{
    private const string ChannelId = "smarttour-playback";
    private const int NotificationId = 3021;

    internal const string ActionToggle = "smarttour.playback.toggle";
    internal const string ActionStop = "smarttour.playback.stop";
    internal const string ActionPrevious = "smarttour.playback.previous";
    internal const string ActionNext = "smarttour.playback.next";
    internal const string ActionSeekBackward = "smarttour.playback.seekback";
    internal const string ActionSeekForward = "smarttour.playback.seekforward";

    public static AndroidAudioPlaybackNotificationManager Instance { get; } = new();

    public void Cancel()
    {
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var context = activity ?? Android.App.Application.Context;
            NotificationManagerCompat.From(context).Cancel(NotificationId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback notification cancel skipped: {ex.Message}");
        }
    }

    public void Refresh(AudioPlaybackService playbackService)
    {
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var context = activity ?? Android.App.Application.Context;
            var playbackCoordinator = PlaybackCoordinatorService.Instance;
            EnsureChannel(context);

            if (playbackService.CurrentTrack is null && !playbackService.IsPaused)
            {
                Cancel();
                return;
            }

            var subtitle = BuildSubtitle(playbackService, playbackCoordinator);
            var contentText = BuildContentText(playbackService);
            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle(BuildTitle(playbackService, playbackCoordinator))
                .SetContentText(subtitle)
                .SetSubText(contentText)
                .SetOnlyAlertOnce(true)
                .SetOngoing(playbackService.IsPlaying || playbackService.IsLoading)
                .SetVisibility(NotificationCompat.VisibilityPublic)
                .SetCategory(NotificationCompat.CategoryTransport)
                .SetSilent(true)
                .SetColor(Android.Graphics.Color.ParseColor("#0F4E8A"))
                .SetColorized(true)
                .SetShowWhen(false)
                .SetPriority((int)NotificationPriority.Low)
                .SetDeleteIntent(BuildActionIntent(context, ActionStop, 90))
                .SetLargeIcon(BuildLargeIcon(context))
                .SetStyle(new NotificationCompat.BigTextStyle()
                    .BigText(subtitle)
                    .SetSummaryText(contentText))
                .SetContentIntent(BuildLaunchIntent(context));

            if (playbackService.CanSeek)
            {
                builder.SetProgress((int)Math.Max(playbackService.CurrentDuration.TotalSeconds, 1), (int)Math.Max(playbackService.CurrentPosition.TotalSeconds, 0), false);
            }

            builder
                .AddAction(Android.Resource.Drawable.IcMediaPrevious, "Trước", BuildActionIntent(context, ActionPrevious, 1))
                .AddAction(
                    playbackService.IsPlaying ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay,
                    playbackService.IsPlaying ? "Tạm dừng" : "Phát",
                    BuildActionIntent(context, ActionToggle, 2))
                .AddAction(Android.Resource.Drawable.IcMediaNext, "Tiếp", BuildActionIntent(context, ActionNext, 3));

            if (playbackService.CanSeek)
            {
                builder
                    .AddAction(Android.Resource.Drawable.IcMediaRew, "-5 giây", BuildActionIntent(context, ActionSeekBackward, 4))
                    .AddAction(Android.Resource.Drawable.IcMediaFf, "+5 giây", BuildActionIntent(context, ActionSeekForward, 5));
            }

            builder.AddAction(Android.Resource.Drawable.IcMenuCloseClearCancel, "Dừng", BuildActionIntent(context, ActionStop, 6));

            NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback notification refresh skipped: {ex.Message}");
        }
    }

    private static string BuildTitle(AudioPlaybackService playbackService, PlaybackCoordinatorService playbackCoordinator)
    {
        if (!string.IsNullOrWhiteSpace(playbackService.CurrentTrack?.Title))
        {
            return playbackService.CurrentTrack.Title;
        }

        if (!string.IsNullOrWhiteSpace(playbackCoordinator.CurrentTitle))
        {
            return playbackCoordinator.CurrentTitle;
        }

        return "SmartTour";
    }

    private static string BuildSubtitle(AudioPlaybackService playbackService, PlaybackCoordinatorService playbackCoordinator)
    {
        if (!string.IsNullOrWhiteSpace(playbackCoordinator.CurrentSubtitle))
        {
            return playbackCoordinator.CurrentSubtitle;
        }

        if (!string.IsNullOrWhiteSpace(playbackCoordinator.QueueTitle))
        {
            return playbackCoordinator.QueueTitle;
        }

        if (!string.IsNullOrWhiteSpace(playbackService.CurrentTrack?.LocationName))
        {
            return playbackService.CurrentTrack.LocationName;
        }

        return playbackService.CurrentTrack?.LanguageName
            ?? playbackService.CurrentTrack?.Language
            ?? "Audio guide";
    }

    private static string BuildContentText(AudioPlaybackService playbackService)
    {
        var state = playbackService.IsLoading ? "Đang tải" : playbackService.IsPaused ? "Tạm dừng" : playbackService.IsPlaying ? "Đang phát" : "Sẵn sàng";
        var position = playbackService.CurrentPosition.TotalSeconds > 0 ? playbackService.CurrentPosition.ToString(@"mm\:ss") : "00:00";
        var duration = playbackService.CurrentDuration.TotalSeconds > 0 ? playbackService.CurrentDuration.ToString(@"mm\:ss") : "--:--";
        return $"{state} • {position}/{duration}";
    }

    private static Bitmap? BuildLargeIcon(Context context)
    {
        try
        {
            return BitmapFactory.DecodeResource(context.Resources, Resource.Mipmap.appicon);
        }
        catch
        {
            return null;
        }
    }

    private static PendingIntent BuildActionIntent(Context context, string action, int requestCode)
    {
        var intent = new Intent(context, typeof(AudioPlaybackActionReceiver));
        intent.SetAction(action);
        return PendingIntent.GetBroadcast(context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static PendingIntent? BuildLaunchIntent(Context context)
    {
        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launchIntent is null)
        {
            return null;
        }

        launchIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        return PendingIntent.GetActivity(context, 99, launchIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager?.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, "SmartTour Playback", NotificationImportance.Low)
        {
            Description = "Điều khiển phát audio đang nghe."
        };
        manager?.CreateNotificationChannel(channel);
    }
}
#endif
