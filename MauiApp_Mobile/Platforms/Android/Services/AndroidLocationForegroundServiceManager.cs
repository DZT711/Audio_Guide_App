#if ANDROID
using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace MauiApp_Mobile.Services;

public static partial class AndroidLocationForegroundServiceManager
{
    public static partial void Start()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        var intent = new Intent(activity, typeof(LocationForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            activity.StartForegroundService(intent);
            return;
        }

        activity.StartService(intent);
    }

    public static partial void Stop()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        var intent = new Intent(activity, typeof(LocationForegroundService));
        activity.StopService(intent);
    }
}
#endif
