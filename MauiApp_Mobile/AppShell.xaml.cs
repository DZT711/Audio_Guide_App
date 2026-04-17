using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("playback-queue", typeof(Views.PlaybackQueuePage));
        Routing.RegisterRoute("qr-scanner", typeof(Views.QrScannerPage));
        Loaded += OnLoaded;
        ApplyTabTexts();
        LocalizationService.Instance.PropertyChanged += (_, _) => ApplyTabTexts();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _ = QrDeepLinkService.Instance.TryHandlePendingAsync();
    }

    private void ApplyTabTexts()
    {
        PlacesTab.Title = LocalizationService.Instance.T("Places.Title");
        MapTab.Title = LocalizationService.Instance.T("Map.Title");
        HistoryTab.Title = LocalizationService.Instance.T("History.Title");
        OfflineTab.Title = "Offline";
        SettingsTab.Title = LocalizationService.Instance.T("Settings.Title");
    }

    public Task NavigateToPlacesTabAsync()
    {
        return GoToAsync("//mainTabs/places");
    }

    public Task NavigateToMapTabAsync()
    {
        return GoToAsync("//mainTabs/map");
    }
}
