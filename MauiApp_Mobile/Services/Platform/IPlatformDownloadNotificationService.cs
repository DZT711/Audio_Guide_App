namespace MauiApp_Mobile.Services.Platform;

public interface IPlatformDownloadNotificationService
{
    void ShowProgress(string title, AudioDownloadProgressUpdate progress);
    void ShowSuccess(string title);
    void ShowFailure(string title, string message);
}
