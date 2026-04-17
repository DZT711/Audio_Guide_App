namespace MauiApp_Mobile.Services;

public static partial class DownloadNotificationService
{
    public static void ShowProgress(string title, AudioDownloadProgressUpdate progress) =>
        ShowProgressCore(title, progress);

    public static void ShowSuccess(string title) =>
        ShowSuccessCore(title);

    public static void ShowFailure(string title, string message) =>
        ShowFailureCore(title, message);

    static partial void ShowProgressCore(string title, AudioDownloadProgressUpdate progress);
    static partial void ShowSuccessCore(string title);
    static partial void ShowFailureCore(string title, string message);
}
