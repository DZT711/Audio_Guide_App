#if ANDROID
using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace MauiApp_Mobile.Services;

public static partial class AndroidLocationForegroundServiceManager
{
    public static partial void Start()
    {
        var context = (Context?)Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? Android.App.Application.Context;
        var intent = new Intent(context, typeof(LocationForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
            return;
        }

        context.StartService(intent);
    }

    public static partial void Stop()
    {
        var context = (Context?)Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? Android.App.Application.Context;
        var intent = new Intent(context, typeof(LocationForegroundService));
        context.StopService(intent);
    }
}
#endif
