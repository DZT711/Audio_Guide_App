using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        StartNotificationCleanupService();
        RegisterActivityLifecycleCallbacks(new TaskRemovalCleanupCallbacks());
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    private void StartNotificationCleanupService()
    {
        try
        {
            StartService(new Intent(this, typeof(NotificationCleanupService)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainApplication] notification cleanup service start failed: {ex.Message}");
        }
    }

    private sealed class TaskRemovalCleanupCallbacks : Java.Lang.Object, IActivityLifecycleCallbacks
    {
        public void OnActivityCreated(Activity activity, Bundle? savedInstanceState)
        {
        }

        public void OnActivityStarted(Activity activity)
        {
        }

        public void OnActivityResumed(Activity activity)
        {
        }

        public void OnActivityPaused(Activity activity)
        {
        }

        public void OnActivityStopped(Activity activity)
        {
        }

        public void OnActivitySaveInstanceState(Activity activity, Bundle outState)
        {
        }

        public void OnActivityDestroyed(Activity activity)
        {
            if (activity.IsChangingConfigurations || !activity.IsTaskRoot)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await PlaybackCoordinatorService.Instance.StopAsync();
                }
                catch
                {
                }

                AndroidAudioPlaybackNotificationManager.Instance.Cancel();
            });
        }
    }
}
