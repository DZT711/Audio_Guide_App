using Microsoft.Extensions.Logging;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Services.DependencyInjection;
using MauiApp_Mobile.Services.Platform;
using ZXing.Net.Maui.Controls;

namespace MauiApp_Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SQLitePCL.Batteries_V2.Init();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(_ => MobileDatabaseService.Instance);
        builder.Services.AddSingleton(_ => AppSettingsService.Instance);
        builder.Services.AddSingleton(_ => PlaceCatalogService.Instance);
        builder.Services.AddSingleton(_ => LocationTrackingService.Instance);
        builder.Services.AddSingleton(_ => HistoryService.Instance);
        builder.Services.AddSingleton(_ => BackgroundSyncService.Instance);
        builder.Services.AddSingleton(_ => AudioPlaybackService.Instance);
        builder.Services.AddSingleton(_ => AudioDownloadService.Instance);
        builder.Services.AddSingleton(_ => PlaybackCoordinatorService.Instance);
        builder.Services.AddSingleton(_ => TelemetryAnonymizerService.Instance);
        builder.Services.AddSingleton(_ => TelemetryCaptureService.Instance);
        builder.Services.AddSingleton(_ => TelemetrySyncService.Instance);
        builder.Services.AddSingleton<IAnalyticsService>(_ => AnalyticsService.Instance);
        builder.Services.AddSingleton(_ => AnalyticsService.Instance);

#if ANDROID
        builder.Services.AddSingleton<ILocationForegroundServiceController, AndroidLocationForegroundServiceController>();
        builder.Services.AddSingleton<IPlatformDownloadNotificationService, AndroidPlatformDownloadNotificationService>();
#elif IOS
        builder.Services.AddSingleton<ILocationForegroundServiceController, IosLocationForegroundServiceController>();
        builder.Services.AddSingleton<IPlatformDownloadNotificationService, IosPlatformDownloadNotificationService>();
#else
        builder.Services.AddSingleton<ILocationForegroundServiceController, DefaultLocationForegroundServiceController>();
        builder.Services.AddSingleton<IPlatformDownloadNotificationService, DefaultPlatformDownloadNotificationService>();
#endif

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddDebug();
        System.Diagnostics.Debug.WriteLine($"[MobileApi] Configured API base URL: {Services.MobileApiOptions.BaseUrl}");
#endif

        var app = builder.Build();
        MauiServiceHost.Configure(app.Services);
        return app;
    }
}
