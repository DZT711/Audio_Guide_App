using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Xaml;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Services.Geofencing;

namespace MauiApp_Mobile;

/// <summary>
/// Application lifecycle management with safe DI service resolution.
/// </summary>
public partial class App : Application
{
    private int _hasStartedInitialization;
    private IServiceProvider? _serviceProvider;

    public App()
    {
        try
        {
            Debug.WriteLine("[App] InitializeComponent starting...");
            InitializeComponent();
            Debug.WriteLine("[App] InitializeComponent completed");
        }
        catch (XamlParseException ex) when (ex.Message?.Contains("DynamicResource", StringComparison.OrdinalIgnoreCase) == true)
        {
            Debug.WriteLine($"[App] DynamicResource binding issue: {ex.Message}");
            CreateMinimalResources();
        }
        catch (XamlParseException ex) when (IsMissingXamlResource(ex))
        {
            Debug.WriteLine($"[App] Missing XAML resource: {ex.Message}");
            Resources ??= new ResourceDictionary();
            CreateMinimalResources();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] InitializeComponent error: {ex.GetType().Name}: {ex.Message}");
            CreateMinimalResources();
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            Debug.WriteLine("[App] CreateWindow: Creating AppShell...");
            var window = new Window(new AppShell());
            window.Created += OnWindowCreated;
            window.Destroying += OnWindowDestroying;
            Debug.WriteLine("[App] CreateWindow completed");
            return window;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] CreateWindow failed: {ex.GetType().Name}: {ex.Message}");
            return CreateStartupFallbackWindow($"Window creation failed: {ex.Message}");
        }
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        try
        {
            if (Interlocked.Exchange(ref _hasStartedInitialization, 1) == 1)
            {
                Debug.WriteLine("[App] Initialization already in progress, skipping duplicate");
                return;
            }

            Debug.WriteLine("[App] Window.Created event: Starting async initialization");
            _ = InitializeApplicationAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] OnWindowCreated error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnWindowDestroying(object? sender, EventArgs e)
    {
        Debug.WriteLine("[App] Window.Destroying event: Starting cleanup");
        _ = Task.Run(CleanupAsync);
    }

    private async Task InitializeApplicationAsync()
    {
        try
        {
            Debug.WriteLine("[App] ========== APPLICATION INITIALIZATION START ==========");

            _serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
            if (_serviceProvider == null)
            {
                Debug.WriteLine("[App] CRITICAL: ServiceProvider is null");
                return;
            }

            Debug.WriteLine("[App] ServiceProvider resolved from DI container");

            if (!await RunInitStepAsync("mobile-database", async () =>
                {
                    var service = GetService<MobileDatabaseService>();
                    if (service == null) throw new InvalidOperationException("MobileDatabaseService not registered");
                    await service.InitializeAsync();
                }))
            {
                return;
            }

            if (!await RunInitStepAsync("app-settings", async () =>
                {
                    var service = GetService<AppSettingsService>();
                    if (service == null) throw new InvalidOperationException("AppSettingsService not registered");
                    await service.InitializeAsync();
                }))
            {
                return;
            }

            await RunInitStepAsync("api-base-url-resolve", () => MobileApiOptions.EnsureResolvedBaseUrlAsync());

            await RunInitStepAsync("theme-service", () =>
            {
                var service = GetService<ThemeService>();
                if (service == null) throw new InvalidOperationException("ThemeService not registered");
                service.Initialize();
                return Task.CompletedTask;
            });

            await RunInitStepAsync("localization-service", () =>
            {
                var service = GetService<LocalizationService>();
                if (service == null) throw new InvalidOperationException("LocalizationService not registered");
                _ = service.Language;
                return Task.CompletedTask;
            });

            await RunInitStepAsync("app-data-mode", () =>
            {
                var service = GetService<AppDataModeService>();
                if (service == null) throw new InvalidOperationException("AppDataModeService not registered");
                service.Initialize(service.IsApiEnabled);
                return Task.CompletedTask;
            });

            await RunInitStepAsync("location-tracking", async () =>
            {
                var service = GetService<LocationTrackingService>();
                if (service == null) throw new InvalidOperationException("LocationTrackingService not registered");
                await service.InitializeAsync();
            });

            await RunInitStepAsync("geofence-orchestrator", async () =>
            {
                var service = GetService<GeofenceOrchestratorService>();
                if (service == null) throw new InvalidOperationException("GeofenceOrchestratorService not registered");
                await service.WarmStartAsync();
            });

            await RunInitStepAsync("history-service", async () =>
            {
                var service = GetService<HistoryService>();
                if (service == null) throw new InvalidOperationException("HistoryService not registered");
                await service.InitializeAsync();
            });

            await RunInitStepAsync("background-sync-start", () =>
            {
                var service = GetService<BackgroundSyncService>();
                if (service == null) throw new InvalidOperationException("BackgroundSyncService not registered");
                service.Start();
                return Task.CompletedTask;
            });

            await RunInitStepAsync("background-services-start", StartBackgroundServicesAsync);

            await RunInitStepAsync("place-catalog", async () =>
            {
                var service = GetService<PlaceCatalogService>();
                if (service == null) throw new InvalidOperationException("PlaceCatalogService not registered");
                await service.EnsureLoadedAsync(false);
            });

            Debug.WriteLine("[App] ========== APPLICATION INITIALIZATION COMPLETE ==========");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] FATAL: Initialization failed: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[App] Stack: {ex.StackTrace}");
        }
    }

    private async Task StartBackgroundServicesAsync()
    {
        var backgroundSyncService = GetService<BackgroundSyncService>();
        var appSettingsService = GetService<AppSettingsService>();
        var locationTrackingService = GetService<LocationTrackingService>();

        if (backgroundSyncService == null || appSettingsService == null || locationTrackingService == null)
        {
            throw new InvalidOperationException("Background startup dependencies are not registered");
        }

        await backgroundSyncService.TriggerCatalogSyncAsync();

        var locationPermission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locationPermission != PermissionStatus.Granted)
        {
            Debug.WriteLine("[App] Foreground location permission is not granted");
            return;
        }

        if (!appSettingsService.BackgroundTrackingEnabled)
        {
            Debug.WriteLine("[App] Background tracking disabled in settings");
            return;
        }

        var backgroundPermission = await locationTrackingService.GetBackgroundPermissionStatusAsync();
        if (backgroundPermission != PermissionStatus.Granted)
        {
            Debug.WriteLine("[App] Background location permission is not granted");
            return;
        }

        await locationTrackingService.StartBackgroundTrackingAsync();
    }

    private async Task<bool> RunInitStepAsync(string stepName, Func<Task> step)
    {
        try
        {
            Debug.WriteLine($"[App.Init] {stepName}: starting...");
            var sw = Stopwatch.StartNew();
            await step();
            sw.Stop();
            Debug.WriteLine($"[App.Init] {stepName}: completed in {sw.ElapsedMilliseconds}ms");
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[App.Init] {stepName}: cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App.Init] {stepName}: failed - {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            Debug.WriteLine("[App] ========== APPLICATION CLEANUP START ==========");

            await SafeStopServiceAsync("playback-coordinator", () =>
                GetService<PlaybackCoordinatorService>()?.StopAsync() ?? Task.CompletedTask);

            await SafeStopServiceAsync("audio-playback", () =>
                GetService<AudioPlaybackService>()?.ShutdownForAppTerminationAsync() ?? Task.CompletedTask);

            await SafeStopServiceAsync("geofence-orchestrator", () =>
                GetService<GeofenceOrchestratorService>()?.StopAsync() ?? Task.CompletedTask);

            await SafeStopServiceAsync("location-tracking", () =>
                GetService<LocationTrackingService>()?.StopAsync() ?? Task.CompletedTask);

            await SafeStopServiceAsync("background-sync", () =>
            {
                var service = GetService<BackgroundSyncService>();
                service?.Stop();
                return Task.CompletedTask;
            });

            Debug.WriteLine("[App] ========== APPLICATION CLEANUP COMPLETE ==========");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Cleanup error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task SafeStopServiceAsync(string serviceName, Func<Task> stopAction)
    {
        try
        {
            await stopAction();
            Debug.WriteLine($"[App.Cleanup] {serviceName}: stopped");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App.Cleanup] {serviceName}: warning during stop - {ex.GetType().Name}: {ex.Message}");
        }
    }

    private T? GetService<T>() where T : class
    {
        try
        {
            if (_serviceProvider == null)
            {
                Debug.WriteLine($"[App] GetService<{typeof(T).Name}>: ServiceProvider not available");
                return null;
            }

            var service = _serviceProvider.GetService<T>();
            if (service == null)
            {
                Debug.WriteLine($"[App] GetService<{typeof(T).Name}>: Service not registered");
                return null;
            }

            return service;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] GetService<{typeof(T).Name}>: Exception - {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private Window CreateStartupFallbackWindow(string errorMessage)
    {
        try
        {
            var titleLabel = new Label
            {
                Text = "Startup Error",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Red,
                HorizontalOptions = LayoutOptions.Center
            };

            var messageLabel = new Label
            {
                Text = errorMessage,
                FontSize = 14,
                TextColor = Colors.DarkRed,
                Padding = 10,
                LineBreakMode = LineBreakMode.WordWrap
            };

            var retryButton = new Button
            {
                Text = "Retry",
                Command = new Command(() =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Application.Current?.Quit();
                    });
                })
            };

            var container = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 15,
                VerticalOptions = LayoutOptions.Center,
                Children = { titleLabel, messageLabel, retryButton }
            };

            var contentPage = new ContentPage
            {
                Content = container,
                BackgroundColor = Colors.White
            };

            return new Window(contentPage);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] CreateFallbackWindow failed: {ex.Message}");
            throw;
        }
    }

    private void CreateMinimalResources()
    {
        try
        {
            Debug.WriteLine("[App] Creating minimal resource dictionary...");
            Resources = new ResourceDictionary
            {
                { "PrimaryGreen", Color.FromArgb("#18A94B") },
                { "SecondaryGreen", Color.FromArgb("#1DB954") },
                { "TabBarBackgroundColor", Colors.White },
                { "TabBarUnselectedColor", Color.FromArgb("#98A2B3") },
                { "BodyText", Color.FromArgb("#1E3250") },
                { "TitleText", Color.FromArgb("#0F172A") },
                { "MutedText", Color.FromArgb("#8A94A6") },
                { "PageBackgroundColor", Color.FromArgb("#FAFAFA") }
            };
            Debug.WriteLine("[App] Minimal resources created");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Failed to create minimal resources: {ex.Message}");
        }
    }

    private static bool IsMissingXamlResource(XamlParseException ex)
    {
        return ex.Message?.Contains("resource", StringComparison.OrdinalIgnoreCase) == true ||
               ex.Message?.Contains("DynamicResource", StringComparison.OrdinalIgnoreCase) == true;
    }
}
