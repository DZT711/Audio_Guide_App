using MauiApp_Mobile.Services;
using Microsoft.Maui.Controls.Xaml;

namespace MauiApp_Mobile;

public partial class AppShell : Shell
{
    private readonly ShellContent _placesTab;
    private readonly ShellContent _mapTab;
    private readonly ShellContent _historyTab;
    private readonly ShellContent _offlineTab;
    private readonly ShellContent _settingsTab;

    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        SetValue(Shell.NavBarIsVisibleProperty, false);
        SetDynamicResource(Shell.TabBarBackgroundColorProperty, "TabBarBackgroundColor");
        SetDynamicResource(Shell.TabBarTitleColorProperty, "TabBarUnselectedColor");
        SetDynamicResource(Shell.TabBarUnselectedColorProperty, "TabBarUnselectedColor");
        SetDynamicResource(Shell.TabBarForegroundColorProperty, "PrimaryGreen");

        Items.Clear();
        Items.Add(new ShellContent
        {
            Route = "language",
            ContentTemplate = new DataTemplate(() => CreateSafePage(() => new Views.LanguagePage(), "LanguagePage"))
        });

        var mainTabs = new TabBar
        {
            Route = "mainTabs"
        };

        _placesTab = new ShellContent
        {
            Route = "places",
            Icon = "location.png",
            ContentTemplate = new DataTemplate(() => CreateSafePage(() => new MainPage(), "MainPage"))
        };
        _mapTab = new ShellContent
        {
            Route = "map",
            Icon = "map.png",
            ContentTemplate = new DataTemplate(() => CreateSafePage(() => new Views.MapPage(), "MapPage"))
        };
        _historyTab = new ShellContent
        {
            Route = "history",
            Icon = "history.png",
            ContentTemplate = new DataTemplate(() => CreateSafePage(() => new Views.HistoryPage(), "HistoryPage"))
        };
        _offlineTab = new ShellContent
        {
            Route = "offline",
            Icon = "offline.png",
            ContentTemplate = new DataTemplate(() => CreateSafePage(() => new Views.OfflinePage(), "OfflinePage"))
        };
        _settingsTab = new ShellContent
        {
            Route = "settings",
            Icon = "settings.png",
            ContentTemplate = new DataTemplate(() => CreateSafePage(() => new Views.SettingsPage(), "SettingsPage"))
        };

        mainTabs.Items.Add(_placesTab);
        mainTabs.Items.Add(_mapTab);
        mainTabs.Items.Add(_historyTab);
        mainTabs.Items.Add(_offlineTab);
        mainTabs.Items.Add(_settingsTab);

        Items.Add(mainTabs);

        Routing.RegisterRoute("playback-queue", typeof(Views.PlaybackQueuePage));
        ApplyTabTexts();
        LocalizationService.Instance.PropertyChanged += (_, _) => ApplyTabTexts();
    }

    private void ApplyTabTexts()
    {
        _placesTab.Title = LocalizationService.Instance.T("Places.Title");
        _mapTab.Title = LocalizationService.Instance.T("Map.Title");
        _historyTab.Title = LocalizationService.Instance.T("History.Title");
        _offlineTab.Title = "Offline";
        _settingsTab.Title = LocalizationService.Instance.T("Settings.Title");
    }

    public Task NavigateToPlacesTabAsync()
    {
        return GoToAsync("//mainTabs/places");
    }

    public Task NavigateToMapTabAsync()
    {
        return GoToAsync("//mainTabs/map");
    }

    private static Page CreateSafePage(Func<Page> pageFactory, string pageName)
    {
        try
        {
            return pageFactory();
        }
        catch (XamlParseException ex) when (IsMissingXamlResource(ex))
        {
            LogStartup($"shell-page-load:missing-xaml-resource:{pageName}", ex);
            return CreateStartupFallbackPage(
                $"The {pageName} UI resource is missing. Rebuild and reinstall the app.");
        }
        catch (Exception ex)
        {
            LogStartup($"shell-page-load:failed:{pageName}", ex);
            return CreateStartupFallbackPage(
                $"Could not open {pageName}. Please restart the app.");
        }
    }

    private static bool IsMissingXamlResource(XamlParseException ex) =>
        ex.Message.Contains("No embeddedresource found", StringComparison.OrdinalIgnoreCase);

    private static Page CreateStartupFallbackPage(string details) =>
        new ContentPage
        {
            Content = new VerticalStackLayout
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
            }
        };

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
}
