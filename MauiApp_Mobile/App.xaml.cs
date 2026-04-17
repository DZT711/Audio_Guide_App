using MauiApp_Mobile.Services;
using MauiApp_Mobile.Services.Geofencing;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Xaml;

namespace MauiApp_Mobile;

public partial class App : Application
{
    private int _hasStartedInitialization;

    public App()
    {
        try
        {
            InitializeComponent();
        }
        catch (XamlParseException ex) when (IsMissingXamlResource(ex))
        {
            LogStartup("initializecomponent:missing-xaml-resource", ex);
            Resources ??= new ResourceDictionary();
        }

        try
        {
            LogStartup("theme-init:begin");
            ThemeService.Instance.Initialize();
            LogStartup("theme-init:complete");
        }
        catch (Exception ex)
        {
            LogStartup("theme-init:failed", ex);
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var window = new Window(new AppShell());
            window.Created += OnWindowCreated;
            window.Destroying += OnWindowDestroying;
            return window;
        }
        catch (Exception ex)
        {
            LogStartup("create-window:failed", ex);
            return CreateStartupFallbackWindow(ex.Message);
        }
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _hasStartedInitialization, 1) == 1)
        {
            return;
        }

        _ = InitializeApplicationAsync();
    }

    private void OnWindowDestroying(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _hasStartedInitialization, 0);

        _ = Task.Run(async () =>
        {
            try
            {
                await PlaybackCoordinatorService.Instance.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App shutdown playback cleanup failed: {ex.Message}");
            }

            try
            {
                await GeofenceOrchestratorService.Instance.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App shutdown geofence cleanup failed: {ex.Message}");
            }

#if ANDROID
            try
            {
                AndroidAudioPlaybackNotificationManager.Instance.Cancel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App shutdown notification cleanup failed: {ex.Message}");
            }
#endif
        });
    }

    private static async Task InitializeApplicationAsync()
    {
        try
        {
            LogStartup("initialize-application:begin");

            if (!await RunStartupStepAsync("mobile-database-init", () => MobileDatabaseService.Instance.InitializeAsync()))
            {
                return;
            }

            if (!await RunStartupStepAsync("app-settings-init", () => AppSettingsService.Instance.InitializeAsync()))
            {
                return;
            }

            if (!await RunStartupStepAsync("api-base-url-resolve", () => MobileApiOptions.EnsureResolvedBaseUrlAsync()))
            {
                return;
            }

            if (!await RunStartupStepAsync("location-tracking-init", () => LocationTrackingService.Instance.InitializeAsync()))
            {
                return;
            }

            if (!await RunStartupStepAsync("history-init", () => HistoryService.Instance.InitializeAsync()))
            {
                return;
            }

            await RunStartupStepAsync("geofence-warm-start", () => GeofenceOrchestratorService.Instance.WarmStartAsync());
            RunStartupStep("background-sync-start", () => BackgroundSyncService.Instance.Start());
            await RunStartupStepAsync("background-services-start", StartBackgroundServicesAsync);

            LogStartup("initialize-application:complete");
        }
        catch (Exception ex)
        {
            LogStartup("initialize-application:failed", ex);
        }
    }

    private static async Task StartBackgroundServicesAsync()
    {
        try
        {
            LogStartup("background-services:catalog-sync:begin");
            await BackgroundSyncService.Instance.TriggerCatalogSyncAsync();
            LogStartup("background-services:catalog-sync:complete");

            var locationPermission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            LogStartup($"background-services:foreground-location={locationPermission}");
            if (locationPermission != PermissionStatus.Granted)
            {
                LogStartup("background-services:skip-background-tracking:foreground-location-not-granted");
                return;
            }

            if (!AppSettingsService.Instance.BackgroundTrackingEnabled)
            {
                LogStartup("background-services:skip-background-tracking:disabled-in-settings");
                return;
            }

            var backgroundPermission = await LocationTrackingService.Instance.GetBackgroundPermissionStatusAsync();
            LogStartup($"background-services:background-location={backgroundPermission}");
            if (backgroundPermission != PermissionStatus.Granted)
            {
                LogStartup("background-services:skip-background-tracking:background-location-not-granted");
                return;
            }

            LogStartup("background-services:start-background-tracking:begin");
            await LocationTrackingService.Instance.StartBackgroundTrackingAsync();
            LogStartup("background-services:start-background-tracking:complete");
        }
        catch (Exception ex)
        {
            LogStartup("background-services:failed", ex);
        }
    }

    private static async Task<bool> RunStartupStepAsync(string stepName, Func<Task> action)
    {
        LogStartup($"{stepName}:begin");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await action();
            LogStartup($"{stepName}:complete:{stopwatch.ElapsedMilliseconds}ms");
            return true;
        }
        catch (Exception ex)
        {
            LogStartup($"{stepName}:failed:{stopwatch.ElapsedMilliseconds}ms", ex);
            return false;
        }
    }

    private static bool RunStartupStep(string stepName, Action action)
    {
        LogStartup($"{stepName}:begin");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            action();
            LogStartup($"{stepName}:complete:{stopwatch.ElapsedMilliseconds}ms");
            return true;
        }
        catch (Exception ex)
        {
            LogStartup($"{stepName}:failed:{stopwatch.ElapsedMilliseconds}ms", ex);
            return false;
        }
    }

    private static void LogStartup(string message, Exception? ex = null)
    {
        var payload = ex is null
            ? $"[Startup] {message}"
            : $"[Startup] {message}: {ex}";

        System.Diagnostics.Debug.WriteLine(payload);

#if ANDROID
        Android.Util.Log.Info("SmartTour.Startup", payload);
#endif
    }

    private static bool IsMissingXamlResource(XamlParseException ex) =>
        ex.Message.Contains("No embeddedresource found", StringComparison.OrdinalIgnoreCase);

    private static Window CreateStartupFallbackWindow(string details)
    {
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20),
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Startup recovery mode",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold
                },
                new Label
                {
                    Text = "The app recovered from a startup load error. Rebuild and reinstall to restore full UI resources.",
                    FontSize = 14
                },
                new Label
                {
                    Text = details,
                    FontSize = 12
                }
            }
        };

        return new Window(new ContentPage
        {
            Content = content
        });
    }
}
