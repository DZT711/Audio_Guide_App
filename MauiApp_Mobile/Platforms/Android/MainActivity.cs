using Android.App;
using Android.Content.PM;
using Android.OS;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

[Activity(
    Theme = "@style/SmartTour.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
    }

    protected override void OnDestroy()
    {
        _ = Task.Run(async () => await AudioPlaybackService.Instance.ShutdownForAppTerminationAsync());
        base.OnDestroy();
    }

    protected override void OnStop()
    {
        try
        {
            if (IsFinishing)
            {
                _ = Task.Run(async () => await AudioPlaybackService.Instance.ShutdownForAppTerminationAsync());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainActivity shutdown cleanup failed: {ex.Message}");
        }

        base.OnStop();
    }
}
