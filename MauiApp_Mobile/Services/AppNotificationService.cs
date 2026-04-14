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
}
