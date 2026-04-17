using Microsoft.Extensions.Logging;
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

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddDebug();
        System.Diagnostics.Debug.WriteLine($"[MobileApi] Configured API base URL: {Services.MobileApiOptions.BaseUrl}");
#endif

        return builder.Build();
    }
}
