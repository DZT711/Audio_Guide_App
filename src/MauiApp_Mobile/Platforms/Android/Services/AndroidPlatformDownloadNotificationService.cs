#if ANDROID
using MauiApp_Mobile.Services.Platform;

namespace MauiApp_Mobile.Services;

public sealed class AndroidPlatformDownloadNotificationService : IPlatformDownloadNotificationService
{
    public void ShowProgress(string title, AudioDownloadProgressUpdate progress)
    {
        DownloadNotificationService.ShowProgress(title, progress);
    }

    public void ShowSuccess(string title)
    {
        DownloadNotificationService.ShowSuccess(title);
    }

    public void ShowFailure(string title, string message)
    {
        DownloadNotificationService.ShowFailure(title, message);
    }
}
#endif
