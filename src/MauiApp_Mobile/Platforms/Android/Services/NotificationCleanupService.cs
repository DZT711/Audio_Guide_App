#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;

namespace MauiApp_Mobile.Services;

[Service(
    Name = "com.companyname.mauiapp_mobile.NotificationCleanupService",
    Exported = false,
    Enabled = true)]
public sealed class NotificationCleanupService : Service
{
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId) =>
        StartCommandResult.Sticky;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        try
        {
            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CancelAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationCleanupService] clear notifications failed: {ex.Message}");
        }

        StopSelf();
        base.OnTaskRemoved(rootIntent);
    }
}
#endif
