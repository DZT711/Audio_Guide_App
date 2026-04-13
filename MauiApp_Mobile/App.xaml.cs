using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;

namespace MauiApp_Mobile;

public partial class App : Application
{
    private int _hasStartedInitialization;

    public App()
    {
        InitializeComponent();
        ThemeService.Instance.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Created += OnWindowCreated;
        return window;
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _hasStartedInitialization, 1) == 1)
        {
            return;
        }

        _ = InitializeApplicationAsync();
    }

    private static async Task InitializeApplicationAsync()
    {
        try
        {
            await MobileDatabaseService.Instance.InitializeAsync();
            await AppSettingsService.Instance.InitializeAsync();
            await LocationTrackingService.Instance.InitializeAsync();
            await HistoryService.Instance.InitializeAsync();
            BackgroundSyncService.Instance.Start();
            await StartBackgroundServicesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App initialization failed: {ex}");
        }
    }

    private static async Task StartBackgroundServicesAsync()
    {
        try
        {
            await BackgroundSyncService.Instance.TriggerCatalogSyncAsync();

            var locationPermission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (locationPermission == PermissionStatus.Granted)
            {
                if (AppSettingsService.Instance.BackgroundTrackingEnabled)
                {
                    var backgroundPermission = await LocationTrackingService.Instance.GetBackgroundPermissionStatusAsync();
                    if (backgroundPermission == PermissionStatus.Granted)
                    {
                        await LocationTrackingService.Instance.StartBackgroundTrackingAsync();
                    }
                    else
                    {
                        await LocationTrackingService.Instance.StartForegroundTrackingAsync();
                    }
                }
                else
                {
                    await LocationTrackingService.Instance.StartForegroundTrackingAsync();
                }
            }
        }
        catch
        {
        }
    }
}
