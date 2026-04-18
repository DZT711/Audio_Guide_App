#if IOS
using MauiApp_Mobile.Services.Platform;

namespace MauiApp_Mobile.Services;

public sealed class IosPlatformDownloadNotificationService : IPlatformDownloadNotificationService
{
    public void ShowProgress(string title, AudioDownloadProgressUpdate progress)
    {
    }

    public void ShowSuccess(string title)
    {
    }

    public void ShowFailure(string title, string message)
    {
    }
}
#endif
