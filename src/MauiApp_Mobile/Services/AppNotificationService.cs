namespace MauiApp_Mobile.Services;

public static partial class AppNotificationService
{
    public static async Task EnsurePermissionAsync()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
#else
        await Task.CompletedTask;
#endif
    }

    public static async Task ShowTransientInfoAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
#if ANDROID
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return Task.CompletedTask;
                }

                var context = Microsoft.Maui.ApplicationModel.Platform.AppContext
                    ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.ApplicationContext;
                if (context is null)
                {
                    return Task.CompletedTask;
                }

                Android.Widget.Toast.MakeText(context, message, Android.Widget.ToastLength.Short)?.Show();
                return Task.CompletedTask;
            });
#else
            await Task.CompletedTask;
#endif
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppNotification] transient-info-failed: {ex.Message}");
        }
    }
}
