#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace MauiApp_Mobile.Services;

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public sealed class LocationForegroundService : Service
{
    internal const string ChannelId = "smarttour-location-tracking";
    internal const int NotificationId = 3007;
    private CancellationTokenSource? _serviceCts;

    public override void OnCreate()
    {
        base.OnCreate();
        EnsureChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("SmartTour GPS tracking")
            .SetContentText("Background location tracking is active.")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .Build();

        StartForeground(NotificationId, notification);

        _serviceCts?.Cancel();
        _serviceCts = new CancellationTokenSource();
        var localToken = _serviceCts.Token;

        _ = Task.Run(async () =>
        {
            while (!localToken.IsCancellationRequested)
            {
                try
                {
                    await LocationTrackingService.Instance.RunSingleBackgroundTickAsync(localToken);
                    await Task.Delay(TimeSpan.FromSeconds(25), localToken);
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }, localToken);

        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        _serviceCts?.Cancel();
        _serviceCts = null;
        base.OnDestroy();
    }

    private void EnsureChannel()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager?.GetNotificationChannel(ChannelId) is not null)
            {
                return;
            }

            var channel = new NotificationChannel(ChannelId, "SmartTour GPS Tracking", NotificationImportance.Low)
            {
                Description = "Keeps location tracking active for SmartTour background guidance."
            };
            manager?.CreateNotificationChannel(channel);
        }
    }
}
#endif
