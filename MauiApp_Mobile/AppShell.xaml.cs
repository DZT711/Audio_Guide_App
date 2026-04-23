using System.Diagnostics;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Views;

namespace MauiApp_Mobile;

/// <summary>
/// Application shell and routing coordinator.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell()
    {
        try
        {
            Debug.WriteLine("[AppShell] InitializeComponent starting...");
            InitializeComponent();

            Routing.RegisterRoute("playback-queue", typeof(PlaybackQueuePage));
            Routing.RegisterRoute("qr-scanner", typeof(QrScannerPage));

            Debug.WriteLine("[AppShell] Routes registered");

            ApplyTabTexts();

            var locService = LocalizationService.Instance;
            if (locService != null)
            {
                locService.PropertyChanged += (_, _) => ApplyTabTexts();
            }

            Loaded += OnLoaded;

            Debug.WriteLine("[AppShell] Initialization complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppShell] ERROR: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private void ApplyTabTexts()
    {
        try
        {
            var loc = LocalizationService.Instance;
            if (loc == null)
            {
                return;
            }

            if (FindByName("PlacesTab") is ShellContent placesTab)
            {
                placesTab.Title = loc.T("Places.Title") ?? "Places";
            }

            if (FindByName("MapTab") is ShellContent mapTab)
            {
                mapTab.Title = loc.T("Map.Title") ?? "Map";
            }

            if (FindByName("HistoryTab") is ShellContent historyTab)
            {
                historyTab.Title = loc.T("History.Title") ?? "History";
            }

            if (FindByName("OfflineTab") is ShellContent offlineTab)
            {
                offlineTab.Title = "Offline";
            }

            if (FindByName("SettingsTab") is ShellContent settingsTab)
            {
                settingsTab.Title = loc.T("Settings.Title") ?? "Settings";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppShell] Tab text application error: {ex.Message}");
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _ = QrDeepLinkService.Instance.TryHandlePendingAsync();
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
