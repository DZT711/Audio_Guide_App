using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;

namespace MauiApp_Mobile;

[Activity(
    Theme = "@style/SmartTour.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = QrDeepLinkService.SmartTourScheme,
    DataHost = QrDeepLinkService.SmartTourHost,
    DataPathPrefix = "/location")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = QrDeepLinkService.SmartTourScheme,
    DataHost = QrDeepLinkService.SmartTourHost,
    DataPathPrefix = "/poi")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = QrDeepLinkService.SmartTourScheme,
    DataHost = QrDeepLinkService.SmartTourHost,
    DataPathPrefix = "/place")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        QueueIncomingDeepLink(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        QueueIncomingDeepLink(intent);
    }

    private static void QueueIncomingDeepLink(Intent? intent)
    {
        var rawLink = intent?.DataString;
        if (string.IsNullOrWhiteSpace(rawLink))
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[QrDeepLink] Android intent received: {rawLink}");
        QrDeepLinkService.Instance.QueuePendingDeepLink(rawLink);
        MainThread.BeginInvokeOnMainThread(() => _ = QrDeepLinkService.Instance.TryHandlePendingAsync());
    }
}
