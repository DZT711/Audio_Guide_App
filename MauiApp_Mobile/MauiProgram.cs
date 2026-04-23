using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Services.DependencyInjection;
using MauiApp_Mobile.Services.Platform;
using MauiApp_Mobile.Services.Geofencing;
using MauiApp_Mobile.Views;
using Project_SharedClassLibrary.Contracts;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

namespace MauiApp_Mobile;

/// <summary>
/// Configures Dependency Injection container for Smart Tourism MAUI app.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates and configures the MAUI application with DI container.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        try
        {
            Debug.WriteLine("[MauiProgram] ============ MAUI APP INITIALIZATION START ============");

            try
            {
                SQLitePCL.Batteries_V2.Init();
                Debug.WriteLine("[MauiProgram] SQLite engine initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MauiProgram] SQLite init warning (non-critical): {ex.Message}");
            }

            var builder = MauiApp.CreateBuilder();

            try
            {
                builder
                    .UseMauiApp<App>()
                    .UseSkiaSharp()
                    .UseBarcodeReader()
                    .ConfigureFonts(fonts =>
                    {
                        try
                        {
                            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MauiProgram] Font loading warning: {ex.Message}");
                        }
                    });

                Debug.WriteLine("[MauiProgram] MAUI builder configured");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MauiProgram] MAUI builder configuration failed: {ex.Message}");
                throw;
            }

            try
            {
                RegisterAllServices(builder.Services);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MauiProgram] Service registration failed: {ex.Message}");
                throw;
            }

            try
            {
                ConfigureLogging(builder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MauiProgram] Logging configuration warning: {ex.Message}");
            }

            try
            {
                var app = builder.Build();
                
                // Lấy từ nhánh Rebuild: Cấu hình Service Host để các class không dùng DI constructor có thể truy cập Services
                MauiServiceHost.Configure(app.Services);

                Debug.WriteLine("[MauiProgram] MAUI app built successfully");
                Debug.WriteLine("[MauiProgram] ============ MAUI APP INITIALIZATION COMPLETE ============");
                return app;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MauiProgram] CRITICAL: Failed to build MAUI app: {ex.Message}");
                Debug.WriteLine($"[MauiProgram] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram] FATAL ERROR: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private static void RegisterAllServices(IServiceCollection services)
    {
        try
        {
            // Resolve: Đã truyền services vào RegisterPlatformServices để tích hợp code từ Rebuild
            RegisterPlatformServices(services); 
            RegisterCoreServices(services);
            RegisterLocationServices(services);
            RegisterGeofencingServices(services);
            RegisterAudioServices(services);
            RegisterDataServices(services);
            RegisterNavigationServices(services);
            RegisterTelemetryServices(services); // Resolve: Nhóm mới cho Telemetry từ Rebuild
            RegisterPages(services);

            Debug.WriteLine("[MauiProgram.DI] All services registered successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Service registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterPlatformServices(IServiceCollection services)
    {
        try
        {
            // Resolve: Tích hợp logic xử lý Foreground/Notification đa nền tảng của nhánh Rebuild
#if ANDROID
            services.AddSingleton<ILocationForegroundServiceController, AndroidLocationForegroundServiceController>();
            services.AddSingleton<IPlatformDownloadNotificationService, AndroidPlatformDownloadNotificationService>();
#elif IOS
            services.AddSingleton<ILocationForegroundServiceController, IosLocationForegroundServiceController>();
            services.AddSingleton<IPlatformDownloadNotificationService, IosPlatformDownloadNotificationService>();
#else
            services.AddSingleton<ILocationForegroundServiceController, DefaultLocationForegroundServiceController>();
            services.AddSingleton<IPlatformDownloadNotificationService, DefaultPlatformDownloadNotificationService>();
#endif
            Debug.WriteLine("[MauiProgram.DI] Platform services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Platform services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton(_ => MobileDatabaseService.Instance);
            services.AddSingleton(_ => AppSettingsService.Instance);
            services.AddSingleton(_ => AppDataModeService.Instance);
            services.AddSingleton(_ => LocalizationService.Instance);
            services.AddSingleton(_ => ThemeService.Instance);
            services.AddSingleton(_ => SearchPlaceholderService.Instance);

            Debug.WriteLine("[MauiProgram.DI] Core services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Core services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterTelemetryServices(IServiceCollection services)
    {
        try
        {
            // Resolve: Tích hợp và gom gọn các dịch vụ Tracking từ nhánh Rebuild
            services.AddSingleton(_ => TelemetryAnonymizerService.Instance);
            services.AddSingleton(_ => TelemetryCaptureService.Instance);
            services.AddSingleton(_ => TelemetrySyncService.Instance);
            services.AddSingleton<IAnalyticsService>(_ => AnalyticsService.Instance);
            services.AddSingleton(_ => AnalyticsService.Instance);

            Debug.WriteLine("[MauiProgram.DI] Telemetry services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Telemetry services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterLocationServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton(_ => UserLocationService.Instance);
            services.AddSingleton(_ => LocationTrackingService.Instance);
            Debug.WriteLine("[MauiProgram.DI] Location services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Location services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterGeofencingServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton(_ => GeofenceOrchestratorService.Instance);
            services.AddSingleton(_ => GeofencePlatformMonitor.Instance);
            services.AddSingleton(_ => PlaceCatalogService.Instance);
            services.AddSingleton(_ => TourCatalogService.Instance);
            services.AddSingleton(_ => QrDeepLinkService.Instance);

            Debug.WriteLine("[MauiProgram.DI] Geofencing services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Geofencing services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterAudioServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton(_ => AudioPlaybackService.Instance);
            services.AddSingleton(_ => PlaybackCoordinatorService.Instance);
            services.AddSingleton(_ => AudioDownloadService.Instance);
            services.AddSingleton(_ => MiniPlayerPresentationService.Instance);

            Debug.WriteLine("[MauiProgram.DI] Audio services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Audio services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterDataServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton(_ => HistoryService.Instance);
            services.AddSingleton(_ => BackgroundSyncService.Instance);

            Debug.WriteLine("[MauiProgram.DI] Data services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Data services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterNavigationServices(IServiceCollection services)
    {
        try
        {
            services.AddSingleton(_ => PlaceNavigationService.Instance);
            Debug.WriteLine("[MauiProgram.DI] Navigation services registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Navigation services registration failed: {ex.Message}");
            throw;
        }
    }

    private static void RegisterPages(IServiceCollection services)
    {
        try
        {
            services.AddSingleton<MainPage>();
            services.AddSingleton<MapPage>();
            services.AddSingleton<HistoryPage>();
            services.AddSingleton<OfflinePage>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<LanguagePage>();

            services.AddTransient<QrScannerPage>();
            services.AddTransient<PlaybackQueuePage>();

            Debug.WriteLine("[MauiProgram.DI] Pages registered");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram.DI] Pages registration failed: {ex.Message}");
            throw;
        }
    }

    private static void ConfigureLogging(MauiAppBuilder builder)
    {
        try
        {
#if DEBUG
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Logging.AddDebug();
            Debug.WriteLine("[MauiProgram] Logging: DEBUG level");
#else
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            Debug.WriteLine("[MauiProgram] Logging: WARNING level");
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiProgram] Logging configuration warning: {ex.Message}");
        }
    }
}