#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using MauiApp_Mobile.Services.Geofencing;

namespace MauiApp_Mobile.Services;

[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public sealed class LocationForegroundService : Service
{
    internal const string ChannelId = "smarttour-location-tracking";
    internal const int NotificationId = 3007;
    internal const string ActionStopTracking = "smarttour.location.stop";
    private CancellationTokenSource? _serviceCts;

    public override void OnCreate()
    {
        base.OnCreate();
        EnsureChannel();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (string.Equals(intent?.Action, ActionStopTracking, StringComparison.Ordinal))
        {
            _ = Task.Run(DisableBackgroundTrackingAsync);
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("SmartTour GPS tracking")
            .SetContentText("Background location tracking is active.")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .AddAction(
                Android.Resource.Drawable.IcMenuCloseClearCancel,
                "Stop",
                BuildStopTrackingPendingIntent())
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
                    var healthCheck = GeofenceOrchestratorService.Instance.GetProcessingHealthCheck();
                    if (!healthCheck.IsHealthy)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Geofence] foreground-service-health-check | status={healthCheck.Status}, runState={healthCheck.RunState}, queueDepth={healthCheck.QueueDepth}, lastHeartbeatUtc={healthCheck.LastHeartbeatUtc:O}");
                        await GeofenceOrchestratorService.Instance.WarmStartAsync(localToken);
                    }

                    await Task.Delay(LocationTrackingService.Instance.GetRecommendedTrackingInterval(), localToken);
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocationForegroundService] background-loop-error | {ex}");
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

    private PendingIntent BuildStopTrackingPendingIntent()
    {
        var intent = new Intent(this, typeof(LocationForegroundService));
        intent.SetAction(ActionStopTracking);
        return PendingIntent.GetService(
            this,
            52,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    private static async Task DisableBackgroundTrackingAsync()
    {
        try
        {
            var snapshot = AppSettingsService.Instance.CreateSnapshot() with
            {
                BackgroundTrackingEnabled = false
            };

            await AppSettingsService.Instance.SaveAsync(snapshot);
            await LocationTrackingService.Instance.StopAsync();
        }
        catch
        {
        }
    }
}
#endif
