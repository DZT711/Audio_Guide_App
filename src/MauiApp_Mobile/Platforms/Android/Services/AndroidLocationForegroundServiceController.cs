#if ANDROID
using MauiApp_Mobile.Services.Platform;

namespace MauiApp_Mobile.Services;

public sealed class AndroidLocationForegroundServiceController : ILocationForegroundServiceController
{
    public void Start()
    {
        AndroidLocationForegroundServiceManager.Start();
    }

    public void Stop()
    {
        AndroidLocationForegroundServiceManager.Stop();
    }
}
#endif
