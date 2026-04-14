using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Devices.Sensors;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;
using Project_SharedClassLibrary.Contracts;
#if ANDROID
using Microsoft.Maui.Handlers;
#endif

namespace MauiApp_Mobile.Views;

public partial class MapPage : ContentPage
{
    private static readonly HttpClient SearchHttpClient = CreateSearchHttpClient();
    private static readonly JsonSerializerOptions JsonInteropOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _locationSemaphore = new(1, 1);
    private readonly SemaphoreSlim _mapScriptSemaphore = new(1, 1);
    private readonly ObservableCollection<MapSearchSuggestion> _searchResults = new();
    private readonly ObservableCollection<MapTourViewModel> _availableTours = new();
    private readonly Dictionary<string, IReadOnlyList<OnlineSearchResult>> _addressSearchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _mapImageDataCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isMapLoaded;
    private bool _isMapReady;
    private bool _hasAnimatedChrome;
    private bool _isDeveloperModeEnabled;
    private bool _isPageDisposing;
    private bool _isTourPanelVisible;
    private bool _isTourListVisible = true;
    private bool _isTourBusy;
    private MapSearchMode _searchMode = MapSearchMode.Poi;
    private MapTourViewModel? _selectedTour;
    private MapTourStopViewModel? _activeTourStop;
    private MapTourStopViewModel? _nextTourStop;
    private CancellationTokenSource? _activeSearchCts;
    private CancellationTokenSource? _mapLoadingCts;
    private CancellationTokenSource? _mapTimeoutCts;
    private Location? _lastMapLocationRendered;
    private int _searchRequestVersion;
    private bool _isRefreshingMap;
    private const int MapLoadTimeoutSeconds = 25;

    public ObservableCollection<MapTourViewModel> AvailableTours => _availableTours;
    public bool IsTourPanelVisible { get => _isTourPanelVisible; private set { if (_isTourPanelVisible == value) return; _isTourPanelVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTourDetailVisible)); } }
    public bool IsTourListVisible { get => _isTourListVisible; private set { if (_isTourListVisible == value) return; _isTourListVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTourDetailVisible)); OnPropertyChanged(nameof(TourPanelHeaderTitle)); OnPropertyChanged(nameof(TourPanelHeaderSubtitle)); } }
    public bool IsTourBusy { get => _isTourBusy; private set { if (_isTourBusy == value) return; _isTourBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTourEmptyVisible)); } }
    public bool IsTourDetailVisible => IsTourPanelVisible && !IsTourListVisible && SelectedTour is not null;
    public bool HasTours => AvailableTours.Count > 0;
    public bool IsTourEmptyVisible => !IsTourBusy && AvailableTours.Count == 0;
    public string TourPanelHeaderTitle => IsTourListVisible || SelectedTour is null ? "Tour Guide" : SelectedTour.Name;
    public string TourPanelHeaderSubtitle => IsTourListVisible || SelectedTour is null ? "Chọn hành trình giữa các POI để khám phá theo tuyến." : SelectedTour.RouteStatusText;
    public MapTourViewModel? SelectedTour { get => _selectedTour; private set { if (_selectedTour == value) return; _selectedTour = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsTourDetailVisible)); OnPropertyChanged(nameof(TourPanelHeaderTitle)); OnPropertyChanged(nameof(TourPanelHeaderSubtitle)); OnPropertyChanged(nameof(SelectedTourSummaryText)); OnPropertyChanged(nameof(SelectedTourRoadHintText)); } }
    public MapTourStopViewModel? ActiveTourStop { get => _activeTourStop; private set { if (_activeTourStop == value) return; _activeTourStop = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveTourProgressText)); OnPropertyChanged(nameof(HasNextTourStop)); } }
    public MapTourStopViewModel? NextTourStop { get => _nextTourStop; private set { if (_nextTourStop == value) return; _nextTourStop = value; OnPropertyChanged(); OnPropertyChanged(nameof(NextTourStopText)); OnPropertyChanged(nameof(HasNextTourStop)); } }
    public string SelectedTourSummaryText => SelectedTour is null ? string.Empty : $"{SelectedTour.StopCount} điểm dừng • {SelectedTour.DurationText} • {SelectedTour.DistanceText}";
    public string SelectedTourRoadHintText => SelectedTour?.RouteStatusText ?? string.Empty;
    public string ActiveTourProgressText => SelectedTour is null || ActiveTourStop is null ? string.Empty : $"Điểm {ActiveTourStop.SequenceOrder}/{SelectedTour.StopCount}";
    public bool HasNextTourStop => NextTourStop is not null;
    public string NextTourStopText => SelectedTour is null || ActiveTourStop is null ? string.Empty : NextTourStop is null ? "Bạn đã đến điểm cuối của hành trình này." : $"{NextTourStop.Name} • {SelectedTour.ResolveDistanceToStop(NextTourStop.SequenceOrder):0.0} km • {SelectedTour.ResolveDurationToStop(NextTourStop.SequenceOrder)} phút";

    public MapPage()
    {
        InitializeComponent();
        BindingContext = this;

        SearchResultsView.ItemsSource = _searchResults;
        AvailableTours.CollectionChanged += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(HasTours));
                OnPropertyChanged(nameof(IsTourEmptyVisible));
            });
        };

        MapWebView.Navigated += OnMapWebViewNavigated;
        MapWebView.Navigating += OnMapWebViewNavigating;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged += OnSearchTextChanged;

        ApplyTexts();
        UpdateSearchModeVisuals();
        UpdateDeveloperModeVisuals();
        UpdateDeveloperModeAvailability();
        UpdateConnectionStatusChip();
        SetLocateButtonState(isBusy: false, isEnabled: false);

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        ThemeService.Instance.PropertyChanged += OnThemeChanged;
        AppSettingsService.Instance.PropertyChanged += OnAppSettingsChanged;
        AppSettingsService.Instance.SettingsSaved += OnSettingsSaved;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        UserLocationService.Instance.LocationUpdated += OnUserLocationUpdated;
        UserLocationService.Instance.HeadingUpdated += OnHeadingUpdated;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isPageDisposing = false;

        if (!_hasAnimatedChrome)
        {
            _hasAnimatedChrome = true;
            _ = UiEffectsService.AnimateEntranceAsync(MapTipChip, CurrentLocationButton, DeveloperModeButton);
        }

        UpdateDeveloperModeAvailability();
        UpdateConnectionStatusChip();

        if (!_isMapLoaded)
        {
            LoadMap();
            _isMapLoaded = true;
        }
        else
        {
            _ = RefreshMapPlacesAsync();
            _ = TryFocusPendingPlaceAsync();
            _ = LoadToursAsync();
        }

        UserLocationService.Instance.EnsureHeadingTracking();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isPageDisposing = true;
        CancelPendingOperations();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        SearchEntry.Completed -= OnSearchCompleted;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged -= OnSearchTextChanged;
        SearchEntry.TextChanged += OnSearchTextChanged;
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.NewHandler is null)
        {
            _isPageDisposing = true;
            CancelPendingOperations();
            DetachEventHandlers();
            UserLocationService.Instance.LocationUpdated -= OnUserLocationUpdated;
            UserLocationService.Instance.HeadingUpdated -= OnHeadingUpdated;
        }

        base.OnHandlerChanging(args);
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Map.Title");
        SearchEntry.Placeholder = _searchMode == MapSearchMode.Poi
            ? LocalizationService.Instance.T("Map.SearchPoi")
            : LocalizationService.Instance.T("Map.SearchAddress");
        MapTipLabel.Text = LocalizationService.Instance.T("Map.LocateHint");
        PoiSearchModeLabel.Text = LocalizationService.Instance.T("Map.ModePoi");
        AddressSearchModeLabel.Text = LocalizationService.Instance.T("Map.ModeAddress");
        SearchResultsTitleLabel.Text = _searchMode == MapSearchMode.Poi
            ? LocalizationService.Instance.T("Map.ResultsPoi")
            : LocalizationService.Instance.T("Map.ResultsAddress");
    }

    private void UpdateSearchModeVisuals()
    {
        ApplyModeChipState(
            PoiSearchModeChip,
            PoiSearchModeLabel,
            _searchMode == MapSearchMode.Poi);

        ApplyModeChipState(
            AddressSearchModeChip,
            AddressSearchModeLabel,
            _searchMode == MapSearchMode.Address);

        SearchResultsTitleLabel.Text = _searchMode == MapSearchMode.Poi
            ? LocalizationService.Instance.T("Map.ResultsPoi")
            : LocalizationService.Instance.T("Map.ResultsAddress");
    }

    private void ApplyModeChipState(Border chip, Label label, bool isSelected)
    {
        chip.BackgroundColor = isSelected
            ? ThemeService.Instance.GetColor("SoftGreen", "#E8F7EE")
            : ThemeService.Instance.GetColor("InputBg", "#FFFFFF");
        chip.Stroke = new SolidColorBrush(
            isSelected
                ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
                : ThemeService.Instance.GetColor("BorderColor", "#E5E7EB"));
        chip.StrokeThickness = isSelected ? 1.4 : 1;
        label.TextColor = isSelected
            ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
            : ThemeService.Instance.GetColor("BodyText", "#243B5A");
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isPageDisposing)
            return;

        ApplyTexts();
        _addressSearchCache.Clear();

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_isMapReady)
            {
                await ApplyMapStringsAsync();
            }
        });
    }

    private void OnThemeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isPageDisposing)
            return;

        UpdateSearchModeVisuals();
        UpdateDeveloperModeVisuals();
        UpdateConnectionStatusChip();

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_isMapReady)
            {
                await ApplyMapThemeAsync();
                await ApplyMapBehaviorAsync();
            }
        });
    }

    private void OnAppSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AppSettingsService.DeveloperModeEnabled), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(AppSettingsService.ApiModeEnabled), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(AppSettingsService.ShowPoiRadiusEnabled), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(AppSettingsService.AutoFocusIdleSeconds), StringComparison.Ordinal) &&
            !string.Equals(e.PropertyName, nameof(AppSettingsService.TriggerRadiusMeters), StringComparison.Ordinal))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateDeveloperModeAvailability();
            UpdateConnectionStatusChip();
            if (_isMapReady)
            {
                await SyncPlacesToMapAsync();
                await ApplyMapBehaviorAsync();
            }
        });
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateConnectionStatusChip);

    private void OnSettingsSaved(object? sender, AppSettingsSnapshot snapshot)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateDeveloperModeAvailability();
            UpdateConnectionStatusChip();
            if (_isMapReady)
            {
                await SyncPlacesToMapAsync();
                await ApplyMapThemeAsync();
                await ApplyMapStringsAsync();
                await ApplyMapBehaviorAsync();
                await ApplyDeveloperModeAsync();
                await LoadToursAsync(forceRefresh: AppDataModeService.Instance.IsApiEnabled);
            }
        });
    }

    private async void LoadMap()
    {
        try
        {
            StartMapLoadingState();

            using var stream = await FileSystem.OpenAppPackageFileAsync("leaflet_map.html");
            using var reader = new StreamReader(stream);
            var htmlContent = await reader.ReadToEndAsync();

            using var leafletCssStream = await FileSystem.OpenAppPackageFileAsync("vendor/leaflet/leaflet.css");
            using var leafletCssReader = new StreamReader(leafletCssStream);
            var leafletCss = await leafletCssReader.ReadToEndAsync();

            using var leafletJsStream = await FileSystem.OpenAppPackageFileAsync("vendor/leaflet/leaflet.js");
            using var leafletJsReader = new StreamReader(leafletJsStream);
            var leafletJs = await leafletJsReader.ReadToEndAsync();

            htmlContent = htmlContent
                .Replace("__LEAFLET_CSS__", leafletCss, StringComparison.Ordinal)
                .Replace("__LEAFLET_JS__", leafletJs, StringComparison.Ordinal);

#if ANDROID
            WebViewHandler.Mapper.AppendToMapping("CustomUserAgent", (handler, view) =>
            {
                if (handler.PlatformView is Android.Webkit.WebView webView)
                {
                    webView.Settings.UserAgentString = "SmartTourismMaui/1.0";
                }
            });
#endif

            MapWebView.Source = new HtmlWebViewSource
            {
                Html = htmlContent
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading Leaflet map: {ex.Message}");
            UpdateSearchStatus("Không thể tải bản đồ lúc này.");
            ShowMapRetryPanel("Không thể tải bản đồ.", FriendlyMessageService.Resolve(ex, "Server connect failure"));
        }
    }

    private void StartMapLoadingState()
    {
        if (_isPageDisposing)
            return;

        MapRetryPanel.IsVisible = false;
        MapSkeletonRows.IsVisible = true;
        MapLoadingOverlay.IsVisible = true;
        MapLoadingOverlay.Opacity = 1;
        MapWebView.Opacity = 0;

        _mapLoadingCts?.Cancel();
        _mapLoadingCts?.Dispose();
        _mapLoadingCts = new CancellationTokenSource();

        _ = UiEffectsService.RunSkeletonPulseAsync(
            _mapLoadingCts.Token,
            MapHeaderSkeleton,
            MapCanvasSkeleton,
            MapInfoSkeleton,
            MapActionSkeleton,
            MapRowSkeleton1,
            MapRowSkeleton2);

        _mapTimeoutCts?.Cancel();
        _mapTimeoutCts?.Dispose();
        _mapTimeoutCts = new CancellationTokenSource();
        var timeoutToken = _mapTimeoutCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(MapLoadTimeoutSeconds), timeoutToken);
                if (timeoutToken.IsCancellationRequested)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_isMapReady && !_isPageDisposing)
                    {
                        ShowMapRetryPanel("Tải bản đồ quá lâu.", "Kéo xuống để làm mới hoặc nhấn Tải lại.");
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, timeoutToken);
    }

    private async Task CompleteMapLoadingStateAsync()
    {
        _mapLoadingCts?.Cancel();
        _mapTimeoutCts?.Cancel();

        if (!MapLoadingOverlay.IsVisible)
            return;

        if (!CanAnimateMapLoadingChrome())
        {
            FinishMapLoadingStateWithoutAnimation();
            return;
        }

        try
        {
            await Task.WhenAll(
                MapWebView.FadeToAsync(1, 240, Easing.CubicOut),
                MapLoadingOverlay.FadeToAsync(0, 180, Easing.CubicOut));
        }
        catch (ObjectDisposedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map loading animation skipped: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map loading animation unavailable: {ex.Message}");
        }

        FinishMapLoadingStateWithoutAnimation();
    }

    private async void OnMapWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        try
        {
            if (_isPageDisposing)
                return;

            _isMapReady = e.Result == WebNavigationResult.Success;
            SetLocateButtonState(isBusy: false, isEnabled: _isMapReady);
            UpdateDeveloperModeVisuals();

            if (!_isMapReady)
            {
                UpdateSearchStatus("Bản đồ chưa sẵn sàng. Hãy thử tải lại trang.");
                ShowMapRetryPanel("Bản đồ chưa tải được.", "Kéo xuống để làm mới hoặc thử tải lại.");
                return;
            }

            await SyncPlacesToMapAsync();
            await ApplyMapThemeAsync();
            await ApplyMapStringsAsync();
            await ApplyMapBehaviorAsync();
            await ApplyDeveloperModeAsync();
            await CompleteMapLoadingStateAsync();
            await TryFocusPendingPlaceAsync();
            await LoadToursAsync();

            if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
            {
                UpdateTypingHint(SearchEntry.Text);
            }
        }
        catch (ObjectDisposedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map page navigation ignored after dispose: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map page navigation halted: {ex.Message}");
        }
    }

    private async void OnMapWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Url))
            return;

        if (!e.Url.StartsWith("smarttour://place/", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        var placeId = e.Url["smarttour://place/".Length..];
        if (string.IsNullOrWhiteSpace(placeId))
            return;

        await OpenPlaceDetailAsync(Uri.UnescapeDataString(placeId));
    }

    private async Task SyncPlacesToMapAsync()
    {
        if (!_isMapReady)
            return;

        var mapPlacesJson = JsonSerializer.Serialize(await BuildMapInteropPointsAsync(), JsonInteropOptions);
        await EvaluateMapScriptAsync($"window.setPlaces && window.setPlaces({mapPlacesJson});");
    }

    private async Task TryFocusPendingPlaceAsync()
    {
        if (!_isMapReady)
            return;

        var pendingPlaceId = PlaceNavigationService.Instance.ConsumePendingMapPlaceId();
        if (string.IsNullOrWhiteSpace(pendingPlaceId))
            return;

        try
        {
            var placeIdJson = JsonSerializer.Serialize(pendingPlaceId);
            var rawResult = await EvaluateMapScriptAsync(
                $"window.focusPlaceById && window.focusPlaceById({placeIdJson});");

            var focusResult = ParseFocusResult(rawResult);
            if (focusResult.Found)
            {
                UpdateSearchStatus($"Đang xem vị trí: {focusResult.Title}");
            }
            else
            {
                UpdateSearchStatus("Đã mở tab bản đồ nhưng chưa thể focus đúng POI.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pending map focus error: {ex.Message}");
            UpdateSearchStatus("Đã mở tab bản đồ nhưng không thể focus POI lúc này.");
        }
    }

    private async Task ApplyMapThemeAsync()
    {
        if (!_isMapReady)
            return;

        var themeJson = JsonSerializer.Serialize(ThemeService.Instance.MapThemeKey);
        await EvaluateMapScriptAsync($"window.applyMapTheme && window.applyMapTheme({themeJson});");
    }

    private async Task ApplyMapStringsAsync()
    {
        if (!_isMapReady)
            return;

        var stringsJson = JsonSerializer.Serialize(new MapStringPayload
        {
            ViewDetails = LocalizationService.Instance.T("Map.ViewDetails"),
            NearestLabel = LocalizationService.Instance.T("Map.NearestLabel"),
            CurrentLocationTitle = LocalizationService.Instance.T("Map.CurrentLocationTitle"),
            SearchResultTitle = LocalizationService.Instance.T("Map.SearchResultTitle"),
            NearestPrefix = LocalizationService.Instance.T("Map.NearestPrefix")
        }, JsonInteropOptions);

        await EvaluateMapScriptAsync($"window.setMapStrings && window.setMapStrings({stringsJson});");
    }

    private async Task ApplyMapBehaviorAsync()
    {
        if (!_isMapReady)
        {
            return;
        }

        var behaviorJson = JsonSerializer.Serialize(new MapBehaviorPayload
        {
            ShowPoiRadius = AppSettingsService.Instance.ShowPoiRadiusEnabled,
            PoiRadiusMeters = AppSettingsService.Instance.TriggerRadiusMeters,
            AutoFocusIdleSeconds = AppSettingsService.Instance.AutoFocusIdleSeconds,
            CameraMoveThresholdMeters = 5
        }, JsonInteropOptions);

        await EvaluateMapScriptAsync($"window.applyMapBehavior && window.applyMapBehavior({behaviorJson});");
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
        var searchOperation = BeginSearchOperation();
        UpdateSearchStatus(LocalizationService.Instance.T(_searchMode == MapSearchMode.Poi
            ? "Map.SearchingPoi"
            : "Map.SearchingAddress"));
        await SearchMapAsync(SearchEntry.Text, SearchTrigger.ManualSubmit, searchOperation.RequestId, searchOperation.Token);
    }

    private async void OnSearchIconTapped(object? sender, TappedEventArgs e)
    {
        SearchEntry.Unfocus();
        var searchOperation = BeginSearchOperation();
        UpdateSearchStatus(LocalizationService.Instance.T(_searchMode == MapSearchMode.Poi
            ? "Map.SearchingPoi"
            : "Map.SearchingAddress"));
        await SearchMapAsync(SearchEntry.Text, SearchTrigger.ManualSubmit, searchOperation.RequestId, searchOperation.Token);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        CancelActiveSearchOperation();
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            ClearSearchResults();
            UpdateSearchStatus(string.Empty);
            return;
        }

        ClearSearchResults();
        UpdateTypingHint(e.NewTextValue);
    }

    private async Task SearchMapAsync(string? keyword, SearchTrigger trigger, int requestId, CancellationToken cancellationToken)
    {
        keyword = keyword?.Trim() ?? string.Empty;
        var showNotFoundMessage = trigger == SearchTrigger.ManualSubmit;
        var autoFocusSingle = trigger == SearchTrigger.ManualSubmit;

        if (!_isMapReady)
        {
            UpdateSearchStatus(string.IsNullOrWhiteSpace(keyword)
                ? string.Empty
                : "Bản đồ đang tải, sẽ tìm ngay khi sẵn sàng.");
            return;
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            ClearSearchResults();
            UpdateSearchStatus(string.Empty);
            return;
        }

        try
        {
            if (_searchMode == MapSearchMode.Poi)
            {
                if (keyword.Length < 2)
                {
                    ClearSearchResults();
                    UpdateSearchStatus(LocalizationService.Instance.T("Map.TypeMorePoi"));
                    return;
                }

                await PlaceCatalogService.Instance.EnsureLoadedAsync(cancellationToken: cancellationToken);
                var poiResults = PlaceCatalogService.Instance.SearchByName(keyword)
                    .Select(CreatePoiSuggestion)
                    .ToList();

                if (!IsSearchRequestCurrent(requestId) || cancellationToken.IsCancellationRequested)
                    return;

                UpdateSearchResults(poiResults);

                if (poiResults.Count == 0)
                {
                    UpdateSearchStatus(showNotFoundMessage
                        ? $"Không tìm thấy POI phù hợp với \"{keyword}\"."
                        : string.Empty);
                    return;
                }

                if (autoFocusSingle && poiResults.Count == 1)
                {
                    await SelectSearchResultAsync(poiResults[0], cancellationToken);
                    return;
                }

                UpdateSearchStatus(poiResults.Count == 1
                    ? $"Đã tìm thấy POI: {poiResults[0].Title}"
                    : $"Đã tìm thấy {poiResults.Count} POI. Hãy chọn một kết quả.");
                return;
            }

            if (keyword.Length < 3)
            {
                ClearSearchResults();
                UpdateSearchStatus(LocalizationService.Instance.T("Map.TypeMoreAddress"));
                return;
            }

            if (trigger != SearchTrigger.ManualSubmit)
            {
                ClearSearchResults();
                UpdateSearchStatus(LocalizationService.Instance.T("Map.AddressTapSearchHint"));
                return;
            }

            var cacheKey = $"{GetPreferredSearchLanguageTag()}::{keyword}";
            if (!_addressSearchCache.TryGetValue(cacheKey, out var cachedAddressResults))
            {
                cachedAddressResults = await SearchOnlineAsync(keyword, 6, GetPreferredSearchLanguageTag(), cancellationToken);
                _addressSearchCache[cacheKey] = cachedAddressResults;
            }

            if (!IsSearchRequestCurrent(requestId) || cancellationToken.IsCancellationRequested)
                return;

            var addressResults = cachedAddressResults
                .Select(CreateAddressSuggestion)
                .ToList();

            UpdateSearchResults(addressResults);

            if (addressResults.Count == 0)
            {
                UpdateSearchStatus(showNotFoundMessage
                    ? $"Không tìm thấy địa chỉ phù hợp với \"{keyword}\"."
                    : string.Empty);
                return;
            }

            if (autoFocusSingle && addressResults.Count == 1)
            {
                await SelectSearchResultAsync(addressResults[0], cancellationToken);
                return;
            }

            UpdateSearchStatus(addressResults.Count == 1
                ? $"Đã tìm thấy địa chỉ: {addressResults[0].Title}"
                : $"Đã tìm thấy {addressResults.Count} địa chỉ. Hãy chọn một kết quả.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map search error: {ex.Message}");
            UpdateSearchStatus("Không thể thực hiện tìm kiếm lúc này.");
        }
    }

    private MapSearchSuggestion CreatePoiSuggestion(PlaceItem place)
    {
        return new MapSearchSuggestion
        {
            Kind = MapSearchSuggestionKind.Poi,
            PlaceId = place.Id,
            Title = place.Name,
            Subtitle = $"{place.Category} • {place.Address}",
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            BadgeText = "POI",
            AccentBackground = ThemeService.Instance.GetColor("SoftGreen", "#E8F7EE"),
            AccentForeground = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        };
    }

    private MapSearchSuggestion CreateAddressSuggestion(OnlineSearchResult result)
    {
        return new MapSearchSuggestion
        {
            Kind = MapSearchSuggestionKind.Address,
            Title = result.Name,
            Subtitle = result.Address,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            BadgeText = "ADDR",
            AccentBackground = ThemeService.Instance.GetColor("SoftPurple", "#EFF6FF"),
            AccentForeground = ThemeService.Instance.GetColor("InfoText", "#2563EB")
        };
    }

    private void UpdateSearchResults(IReadOnlyList<MapSearchSuggestion> results)
    {
        _searchResults.Clear();
        foreach (var result in results)
        {
            _searchResults.Add(result);
        }

        SearchResultsPanel.IsVisible = _searchResults.Count > 0;
    }

    private void ClearSearchResults()
    {
        _searchResults.Clear();
        SearchResultsPanel.IsVisible = false;
    }

    private async void OnSearchResultTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.BindingContext is not MapSearchSuggestion result)
            return;

        SearchEntry.Unfocus();
        await SelectSearchResultAsync(result);
    }

    private async Task SelectSearchResultAsync(MapSearchSuggestion result, CancellationToken cancellationToken = default)
    {
        if (!_isMapReady)
            return;

        ClearSearchResults();
        SearchEntry.Unfocus();

        if (result.Kind == MapSearchSuggestionKind.Poi)
        {
            var placeIdJson = JsonSerializer.Serialize(result.PlaceId);
            var rawResult = await EvaluateMapScriptAsync(
                $"window.focusPlaceById && window.focusPlaceById({placeIdJson});",
                cancellationToken);

            var focusResult = ParseFocusResult(rawResult);
            if (focusResult.Found)
            {
                UpdateSearchStatus($"Đang mở POI: {focusResult.Title}");
            }

            return;
        }

        var titleJson = JsonSerializer.Serialize(result.Title);
        var descriptionJson = JsonSerializer.Serialize(result.Subtitle);
        var script =
            $"window.showSearchResult && window.showSearchResult({result.Latitude.ToString(CultureInfo.InvariantCulture)}, {result.Longitude.ToString(CultureInfo.InvariantCulture)}, {titleJson}, {descriptionJson});";
        await EvaluateMapScriptAsync(script, cancellationToken);
        UpdateSearchStatus($"Đã định vị địa chỉ: {result.Title}");
    }

    private void OnPoiSearchModeTapped(object? sender, TappedEventArgs e)
    {
        _ = SwitchSearchModeAsync(MapSearchMode.Poi);
    }

    private void OnAddressSearchModeTapped(object? sender, TappedEventArgs e)
    {
        _ = SwitchSearchModeAsync(MapSearchMode.Address);
    }

    private Task SwitchSearchModeAsync(MapSearchMode mode)
    {
        if (_searchMode == mode)
            return Task.CompletedTask;

        CancelActiveSearchOperation();
        _searchMode = mode;
        ApplyTexts();
        UpdateSearchModeVisuals();
        ClearSearchResults();

        if (string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            UpdateSearchStatus(string.Empty);
            return Task.CompletedTask;
        }

        UpdateTypingHint(SearchEntry.Text);
        return Task.CompletedTask;
    }

    private async void OnCurrentLocationTapped(object? sender, TappedEventArgs e)
    {
        await FocusCurrentLocationAsync();
    }

    private async void OnDeveloperModeTapped(object? sender, TappedEventArgs e)
    {
        if (!AppSettingsService.Instance.DeveloperModeEnabled)
            return;

        if (!_isMapReady)
            return;

        _isDeveloperModeEnabled = !_isDeveloperModeEnabled;
        UpdateDeveloperModeVisuals();
        await ApplyDeveloperModeAsync();

        UpdateSearchStatus(_isDeveloperModeEnabled
            ? "Đã bật chế độ dev. Chạm lên bản đồ để đặt vị trí thử nghiệm."
            : "Đã tắt chế độ dev.");
    }

    private async Task FocusCurrentLocationAsync()
    {
        if (!_isMapReady)
        {
            UpdateSearchStatus("Bản đồ đang tải, vui lòng thử lại sau.");
            return;
        }

        if (!await _locationSemaphore.WaitAsync(0))
        {
            UpdateSearchStatus("Đang lấy vị trí hiện tại...");
            return;
        }

        try
        {
            SetLocateButtonState(isBusy: true, isEnabled: true);
            await CurrentLocationButton.ScaleToAsync(0.92, 70, Easing.CubicIn);
            await CurrentLocationButton.ScaleToAsync(1, 150, Easing.CubicOut);

            if (!await EnsureForegroundLocationAccessForMapAsync())
            {
                UpdateSearchStatus("Chưa cấp quyền vị trí nên không thể định vị.");
                return;
            }

            var location = await UserLocationService.Instance.RefreshLocationAsync(requestPermission: false);
            if (location is null)
            {
                UpdateSearchStatus("Không lấy được vị trí hiện tại. Hãy thử ở khu vực thoáng hơn.");
                await PromptEnableLocationServicesAsync(
                    "Không lấy được vị trí hiện tại. Hãy kiểm tra GPS hoặc mở cài đặt vị trí để thử lại.");
                return;
            }

            UserLocationService.Instance.UpdateLocation(location);
            await LocationTrackingService.Instance.StartTrackingFromSettingsAsync(requestBackgroundUpgrade: true);
            UserLocationService.Instance.EnsureHeadingTracking();
            await ApplyMapThemeAsync();
            await ApplyMapStringsAsync();
            await ApplyMapBehaviorAsync();
            await ShowCurrentLocationOnMapAsync(location, shouldUpdateStatus: true);
        }
        catch (FeatureNotEnabledException)
        {
            UpdateSearchStatus("Vui lòng bật GPS để lấy vị trí hiện tại.");
            await PromptEnableLocationServicesAsync();
        }
        catch (FeatureNotSupportedException)
        {
            UpdateSearchStatus("Thiết bị này chưa hỗ trợ định vị GPS.");
        }
        catch (PermissionException)
        {
            if (!await EnsureForegroundLocationAccessForMapAsync())
            {
                UpdateSearchStatus("Ứng dụng không có quyền truy cập vị trí.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Current location error: {ex.Message}");
            UpdateSearchStatus("Không thể lấy vị trí hiện tại lúc này.");
        }
        finally
        {
            SetLocateButtonState(isBusy: false, isEnabled: true);
            _locationSemaphore.Release();
        }
    }

    private async Task PromptEnableLocationServicesAsync(string? message = null)
    {
        var openSettings = await DisplayAlertAsync(
            "Bật vị trí",
            message ?? "GPS hoặc dịch vụ vị trí đang tắt. Mở cài đặt vị trí ngay để tiếp tục định vị?",
            "Mở cài đặt",
            "Để sau");

        if (!openSettings)
            return;

        OpenLocationSettings();
        UpdateSearchStatus("Đã mở cài đặt vị trí. Hãy bật GPS rồi quay lại ứng dụng.");
    }

    private async Task PromptOpenAppSettingsAsync(string title, string message, string acceptText)
    {
        var openSettings = await DisplayAlertAsync(
            title,
            message,
            acceptText,
            "Để sau");

        if (!openSettings)
            return;

        AppInfo.ShowSettingsUI();
        UpdateSearchStatus("Đã mở cài đặt ứng dụng. Hãy cấp quyền vị trí rồi quay lại ứng dụng.");
    }

    private async Task<bool> EnsureForegroundLocationAccessForMapAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

#if ANDROID
        if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
        {
            var retry = await DisplayAlertAsync(
                "Quyền vị trí",
                "Ứng dụng cần quyền vị trí chính xác để lấy đúng vị trí hiện tại trên bản đồ.",
                "Yêu cầu lại",
                "Để sau");

            if (!retry)
            {
                return false;
            }

            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }
#endif

        var openSettings = await DisplayAlertAsync(
            "Quyền vị trí",
            "Android đang chặn hộp thoại cấp quyền vị trí cho ứng dụng này. Bạn có muốn mở cài đặt ứng dụng để bật lại không?",
            "Mở cài đặt",
            "Để sau");

        if (openSettings)
        {
            AppInfo.ShowSettingsUI();
            UpdateSearchStatus("Đã mở cài đặt ứng dụng. Hãy bật lại quyền vị trí rồi quay lại ứng dụng.");
        }

        return false;
    }

    private async void OnUserLocationUpdated(object? sender, Location? location)
    {
        if (_isPageDisposing || !_isMapReady || location is null)
        {
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_isPageDisposing || !_isMapReady)
                {
                    return;
                }

                await ShowCurrentLocationOnMapAsync(location, shouldUpdateStatus: false);
            });
        }
        catch
        {
        }
    }

    private async void OnHeadingUpdated(object? sender, double? heading)
    {
        if (_isPageDisposing || !_isMapReady || heading is null)
        {
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_isPageDisposing || !_isMapReady)
                {
                    return;
                }

                await EvaluateMapScriptAsync(
                    $"window.updateCurrentLocationHeading && window.updateCurrentLocationHeading({heading.Value.ToString(CultureInfo.InvariantCulture)});");
            });
        }
        catch
        {
        }
    }

    private async Task ShowCurrentLocationOnMapAsync(Location location, bool shouldUpdateStatus)
    {
        if (!shouldUpdateStatus &&
            _lastMapLocationRendered is not null &&
            Location.CalculateDistance(_lastMapLocationRendered, location, DistanceUnits.Kilometers) * 1000d < 2d)
        {
            return;
        }

        var heading = UserLocationService.Instance.HeadingDegrees ?? 0d;
        var script =
            $"window.showCurrentLocation && window.showCurrentLocation({location.Latitude.ToString(CultureInfo.InvariantCulture)}, {location.Longitude.ToString(CultureInfo.InvariantCulture)}, {heading.ToString(CultureInfo.InvariantCulture)}, {(shouldUpdateStatus ? "true" : "false")});";
        var rawNearest = await EvaluateMapScriptAsync(script);
        _lastMapLocationRendered = location;
        var focusResult = ParseFocusResult(rawNearest);

        if (!shouldUpdateStatus)
        {
            return;
        }

        if (focusResult.Found && !string.IsNullOrWhiteSpace(focusResult.Title) && focusResult.DistanceMeters > 0)
        {
            UpdateSearchStatus(
                $"Đã định vị vị trí hiện tại. Gần nhất: {focusResult.Title} ({focusResult.DistanceMeters:0}m).");
            return;
        }

        UpdateSearchStatus("Đã định vị vị trí hiện tại trên bản đồ.");
    }

    private static void OpenLocationSettings()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is not null)
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionLocationSourceSettings);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            activity.StartActivity(intent);
            return;
        }
#endif

        AppInfo.ShowSettingsUI();
    }

    private async Task OpenPlaceDetailAsync(string placeId)
    {
        await PlaceCatalogService.Instance.EnsureLoadedAsync();
        var place = PlaceCatalogService.Instance.FindById(placeId);
        if (place is null)
        {
            UpdateSearchStatus("Không tìm thấy dữ liệu chi tiết của POI này.");
            return;
        }

        PlaceNavigationService.Instance.RequestPlaceDetail(placeId);
        UpdateSearchStatus($"Đang mở chi tiết: {place.Name}");

        if (Application.Current?.Windows.FirstOrDefault()?.Page is AppShell appShell)
        {
            await appShell.NavigateToPlacesTabAsync();
            return;
        }

        await Shell.Current.GoToAsync("//mainTabs/places");
    }

    private async Task<IReadOnlyList<MapPlaceInteropPoint>> BuildMapInteropPointsAsync()
    {
        var sourcePoints = await PlaceCatalogService.Instance.GetMapPointsAsync();
        var preparedPoints = new List<MapPlaceInteropPoint>(sourcePoints.Count);

        foreach (var point in sourcePoints)
        {
            preparedPoints.Add(new MapPlaceInteropPoint
            {
                Id = point.Id,
                Title = point.Title,
                Description = point.Description,
                Address = point.Address,
                Category = point.Category,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                RadiusMeters = point.RadiusMeters,
                Image = await ResolveMapImageSourceAsync(point.Image),
                GalleryImages = await ResolveMapImageSourcesAsync(point.GalleryImages)
            });
        }

        return preparedPoints;
    }

    private async Task RefreshMapPlacesAsync()
    {
        try
        {
            await PlaceCatalogService.Instance.EnsureLoadedAsync(forceRefresh: true);
            await SyncPlacesToMapAsync();
            await ApplyMapThemeAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnMapRefreshRequested(object? sender, EventArgs e)
    {
        try
        {
            await RefreshMapAsync(forceReloadWebView: !_isMapReady || MapRetryPanel.IsVisible);
        }
        finally
        {
            MapRefreshView.IsRefreshing = false;
        }
    }

    private async void OnTourButtonTapped(object? sender, TappedEventArgs e)
    {
        IsTourPanelVisible = !IsTourPanelVisible;
        if (IsTourPanelVisible)
        {
            IsTourListVisible = true;
            await LoadToursAsync();
        }
    }

    private async Task LoadToursAsync(bool forceRefresh = false)
    {
        if (IsTourBusy)
        {
            return;
        }

        try
        {
            IsTourBusy = true;
            var tours = await TourCatalogService.Instance.GetPublicToursAsync(forceRefresh);
            AvailableTours.Clear();
            foreach (var tour in tours.Where(item => item.Stops.Count > 0).Select(CreateTourViewModel))
            {
                AvailableTours.Add(tour);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tour load error: {ex.Message}");
            UpdateSearchStatus("Không thể tải tour lúc này.");
        }
        finally
        {
            IsTourBusy = false;
        }
    }

    private async Task RefreshMapAsync(bool forceReloadWebView)
    {
        if (_isRefreshingMap || _isPageDisposing)
        {
            return;
        }

        try
        {
            _isRefreshingMap = true;
            UpdateSearchStatus("Đang làm mới bản đồ...");

            if (forceReloadWebView)
            {
                await ReloadMapAsync();
                return;
            }

            await RefreshMapPlacesAsync();
            await LoadToursAsync(forceRefresh: AppDataModeService.Instance.IsApiEnabled);

            if (UserLocationService.Instance.LastKnownLocation is { } lastKnownLocation)
            {
                await ShowCurrentLocationOnMapAsync(lastKnownLocation, shouldUpdateStatus: false);
            }

            UpdateSearchStatus("Đã làm mới dữ liệu bản đồ.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map refresh error: {ex.Message}");
            ShowMapRetryPanel("Không thể làm mới bản đồ.", FriendlyMessageService.Resolve(ex, "Server connect failure"));
            UpdateSearchStatus("Làm mới bản đồ thất bại.");
        }
        finally
        {
            _isRefreshingMap = false;
        }
    }

    private Task ReloadMapAsync()
    {
        _isMapReady = false;
        _isMapLoaded = false;
        _lastMapLocationRendered = null;
        MapRetryPanel.IsVisible = false;
        MapSkeletonRows.IsVisible = true;
        LoadMap();
        _isMapLoaded = true;
        return Task.CompletedTask;
    }

    private MapTourViewModel CreateTourViewModel(MobileTourDescriptor tour)
    {
        var orderedStops = tour.Stops
            .OrderBy(item => item.SequenceOrder)
            .Select(stop => new MapTourStopViewModel
            {
                PlaceId = stop.PlaceId,
                Name = stop.Name,
                Address = stop.Address ?? "Chưa có địa chỉ",
                Category = stop.Category,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                SequenceOrder = stop.SequenceOrder,
                ImageUrl = TourCatalogService.Instance.ResolveImageUrl(stop.ImageUrl) ?? "location.png"
            })
            .ToList();

        var routePreview = tour.RoutePreview;
        return new MapTourViewModel
        {
            Id = tour.Id,
            Name = tour.Name,
            Description = string.IsNullOrWhiteSpace(tour.Description) ? "Hành trình tham quan nhiều điểm nổi bật." : tour.Description.Trim(),
            StopCount = tour.StopCount > 0 ? tour.StopCount : orderedStops.Count,
            DistanceKm = routePreview.TotalDistanceKm,
            EstimatedDurationMinutes = routePreview.EstimatedDurationMinutes,
            UsesRoadRouting = routePreview.UsesRoadRouting,
            Stops = orderedStops,
            RoutePreview = routePreview,
            CoverImageUrl = orderedStops.FirstOrDefault()?.ImageUrl ?? "location.png"
        };
    }

    private async void OnTourItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.BindingContext is not MapTourViewModel tour)
        {
            return;
        }

        await SelectTourAsync(tour);
    }

    private async Task SelectTourAsync(MapTourViewModel tour)
    {
        SelectedTour = tour;
        IsTourPanelVisible = true;
        IsTourListVisible = false;
        ActiveTourStop = tour.Stops.OrderBy(item => item.SequenceOrder).FirstOrDefault();
        NextTourStop = ResolveNextTourStop(tour, ActiveTourStop);
        await ShowSelectedTourRouteAsync();
    }

    private async Task ShowSelectedTourRouteAsync()
    {
        if (!_isMapReady || SelectedTour is null)
        {
            return;
        }

        var routeState = await BuildTourInteropStateAsync(SelectedTour, ActiveTourStop?.SequenceOrder ?? 1);
        var routeJson = JsonSerializer.Serialize(routeState, JsonInteropOptions);
        await EvaluateMapScriptAsync($"window.showTourRoute && window.showTourRoute({routeJson});");
    }

    private async Task<MapTourInteropState> BuildTourInteropStateAsync(MapTourViewModel tour, int activeSequenceOrder)
    {
        var stops = new List<MapTourInteropStop>(tour.Stops.Count);
        foreach (var stop in tour.Stops)
        {
            stops.Add(new MapTourInteropStop
            {
                PlaceId = stop.PlaceId,
                Title = stop.Name,
                Category = stop.Category,
                Address = stop.Address,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                SequenceOrder = stop.SequenceOrder,
                Image = await ResolveMapImageSourceAsync(stop.ImageUrl)
            });
        }

        return new MapTourInteropState
        {
            TourId = tour.Id,
            ActiveSequenceOrder = activeSequenceOrder,
            UsesRoadRouting = tour.UsesRoadRouting,
            Stops = stops,
            Path = tour.RoutePreview?.Path ?? []
        };
    }

    private async Task ClearTourRouteAsync()
    {
        if (_isMapReady)
        {
            await EvaluateMapScriptAsync("window.clearTourRoute && window.clearTourRoute();");
        }
    }

    private async void OnCloseTourPanelTapped(object? sender, TappedEventArgs e)
    {
        IsTourPanelVisible = false;
        IsTourListVisible = true;
        SelectedTour = null;
        ActiveTourStop = null;
        NextTourStop = null;
        await ClearTourRouteAsync();
    }

    private async void OnTourBackTapped(object? sender, TappedEventArgs e)
    {
        IsTourListVisible = true;
        SelectedTour = null;
        ActiveTourStop = null;
        NextTourStop = null;
        await ClearTourRouteAsync();
    }

    private async void OnTourNextTapped(object? sender, EventArgs e)
    {
        if (SelectedTour is null || NextTourStop is null)
        {
            return;
        }

        ActiveTourStop = NextTourStop;
        NextTourStop = ResolveNextTourStop(SelectedTour, ActiveTourStop);
        await FocusActiveTourStopAsync();
    }

    private async Task FocusActiveTourStopAsync()
    {
        if (!_isMapReady || ActiveTourStop is null)
        {
            return;
        }

        await EvaluateMapScriptAsync($"window.focusTourStop && window.focusTourStop({ActiveTourStop.SequenceOrder.ToString(CultureInfo.InvariantCulture)});");
    }

    private async void OnTourListenTapped(object? sender, EventArgs e)
    {
        if (SelectedTour is null || ActiveTourStop is null)
        {
            return;
        }

        await PlayTourAsync(SelectedTour, ActiveTourStop.SequenceOrder);
    }

    private async Task PlayTourAsync(MapTourViewModel tour, int activeSequenceOrder)
    {
        try
        {
            var queueItems = new List<PlaybackQueueItem>();
            foreach (var stop in tour.Stops.OrderBy(item => item.SequenceOrder))
            {
                var track = await PlaceCatalogService.Instance.GetDefaultAudioTrackAsync(stop.PlaceId);
                if (track is null && AppDataModeService.Instance.IsApiEnabled)
                {
                    track = await PlaceCatalogService.Instance.GetDefaultAudioTrackAsync(stop.PlaceId, forceRefresh: true);
                }

                if (track is null)
                {
                    continue;
                }

                var playableTrack = await AudioDownloadService.Instance.ResolvePlayableTrackAsync(track);
                queueItems.Add(new PlaybackQueueItem(playableTrack, tour.Name, stop.Name));
            }

            if (queueItems.Count == 0)
            {
                await DisplayAlertAsync("Audio", "Tour này chưa có audio khả dụng.", "OK");
                return;
            }

            var queueIndex = Math.Max(0, tour.Stops.OrderBy(item => item.SequenceOrder).TakeWhile(item => item.SequenceOrder < activeSequenceOrder).Count());
            await PlaybackCoordinatorService.Instance.PlayQueueAsync(queueItems, Math.Min(queueIndex, queueItems.Count - 1));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tour audio play error: {ex.Message}");
            await DisplayAlertAsync("Audio", FriendlyMessageService.Resolve(ex, "Server connect failure"), "OK");
        }
    }

    private async void OnTourDetailTapped(object? sender, EventArgs e)
    {
        if (ActiveTourStop is not null)
        {
            await OpenPlaceDetailAsync(ActiveTourStop.PlaceId);
        }
    }

    private static MapTourStopViewModel? ResolveNextTourStop(MapTourViewModel tour, MapTourStopViewModel? currentStop) =>
        currentStop is null
            ? null
            : tour.Stops.OrderBy(item => item.SequenceOrder).FirstOrDefault(item => item.SequenceOrder == currentStop.SequenceOrder + 1);

    private async Task<IReadOnlyList<string>> ResolveMapImageSourcesAsync(IReadOnlyList<string> imageSources)
    {
        if (imageSources.Count == 0)
            return Array.Empty<string>();

        var resolvedSources = new List<string>(imageSources.Count);
        foreach (var imageSource in imageSources)
        {
            var resolvedSource = await ResolveMapImageSourceAsync(imageSource);
            if (!string.IsNullOrWhiteSpace(resolvedSource))
            {
                resolvedSources.Add(resolvedSource);
            }
        }

        return resolvedSources;
    }

    private async Task<string> ResolveMapImageSourceAsync(string imageSource)
    {
        if (string.IsNullOrWhiteSpace(imageSource))
            return string.Empty;

        if (Path.IsPathRooted(imageSource) && File.Exists(imageSource))
        {
            return new Uri(imageSource).AbsoluteUri;
        }

        if (imageSource.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            imageSource.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            imageSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return imageSource;
        }

        if (_mapImageDataCache.TryGetValue(imageSource, out var cachedSource))
            return cachedSource;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(imageSource);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            var mimeType = Path.GetExtension(imageSource).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "image/png"
            };

            var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(memoryStream.ToArray())}";
            _mapImageDataCache[imageSource] = dataUri;
            return dataUri;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map image fallback for {imageSource}: {ex.Message}");
            return string.Empty;
        }
    }

    private void SetLocateButtonState(bool isBusy, bool isEnabled)
    {
        CurrentLocationSpinner.IsVisible = isBusy;
        CurrentLocationSpinner.IsRunning = isBusy;
        CurrentLocationIcon.IsVisible = !isBusy;
        CurrentLocationButton.InputTransparent = !isEnabled;
        CurrentLocationButton.Opacity = isEnabled ? (isBusy ? 0.92 : 1) : 0.62;
    }

    private void UpdateDeveloperModeVisuals()
    {
        DeveloperModeButton.IsVisible = AppSettingsService.Instance.DeveloperModeEnabled;
        DeveloperModeButton.BackgroundColor = _isDeveloperModeEnabled
            ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
            : Color.FromArgb("#FFFFFF");
        DeveloperModeButton.Stroke = new SolidColorBrush(
            _isDeveloperModeEnabled
                ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
                : ThemeService.Instance.GetColor("MapButtonRing", "#D6FAE3"));
        DeveloperModeButton.Opacity = _isMapReady ? 1 : 0.68;
        DeveloperModeButton.InputTransparent = !_isMapReady;
    }

    private void UpdateDeveloperModeAvailability()
    {
        if (!AppSettingsService.Instance.DeveloperModeEnabled)
        {
            _isDeveloperModeEnabled = false;
        }

        UpdateDeveloperModeVisuals();
        _ = ApplyDeveloperModeAsync();
    }

    private async Task ApplyDeveloperModeAsync()
    {
        if (!_isMapReady)
            return;

        var enabledLiteral = _isDeveloperModeEnabled ? "true" : "false";
        await EvaluateMapScriptAsync($"window.setDeveloperMode && window.setDeveloperMode({enabledLiteral});");
    }

    private void UpdateSearchStatus(string message)
    {
        var isVisible = !string.IsNullOrWhiteSpace(message);
        if (SearchStatusLabel.Text == message && SearchStatusLabel.IsVisible == isVisible)
            return;

        SearchStatusLabel.Text = message;
        SearchStatusLabel.IsVisible = isVisible;
    }

    private void UpdateConnectionStatusChip()
    {
        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        var isOnline = AppDataModeService.Instance.IsApiEnabled && hasInternet;

        ConnectionStatusLabel.Text = isOnline ? "Online" : "Offline";
        ConnectionStatusDot.TextColor = isOnline
            ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
            : Color.FromArgb("#B54708");
        ConnectionStatusLabel.TextColor = isOnline
            ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
            : Color.FromArgb("#B54708");
        ConnectionStatusChip.BackgroundColor = isOnline
            ? Color.FromArgb("#E8F7EE")
            : Color.FromArgb("#FFF4E5");
    }

    private static MapFocusResult ParseFocusResult(string? rawResult)
    {
        if (string.IsNullOrWhiteSpace(rawResult))
            return new MapFocusResult();

        var normalized = rawResult.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = JsonSerializer.Deserialize<string>(normalized) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(normalized))
            return new MapFocusResult();

        return JsonSerializer.Deserialize<MapFocusResult>(normalized, JsonInteropOptions) ?? new MapFocusResult();
    }

    private static async Task<IReadOnlyList<OnlineSearchResult>> SearchOnlineAsync(string keyword, int limit, string acceptLanguage, CancellationToken cancellationToken)
    {
        var requestUri =
            $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit={Math.Max(1, limit)}&dedupe=1&addressdetails=1&accept-language={Uri.EscapeDataString(acceptLanguage)}&q={Uri.EscapeDataString(keyword)}";

        var results = await SearchHttpClient.GetFromJsonAsync<List<NominatimResult>>(requestUri, cancellationToken) ?? [];

        return results
            .Select(item =>
            {
                if (!double.TryParse(item.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                    !double.TryParse(item.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                {
                    return null;
                }

                return new OnlineSearchResult
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Name = item.NameOrTitle,
                    Address = item.DisplayName
                };
            })
            .Where(item => item is not null)
            .Cast<OnlineSearchResult>()
            .Take(limit)
            .ToList();
    }

    private static HttpClient CreateSearchHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartTourismMaui/1.0");
        return client;
    }

    private (int RequestId, CancellationToken Token) BeginSearchOperation()
    {
        CancelActiveSearchOperation();
        _activeSearchCts = new CancellationTokenSource();
        var requestId = Interlocked.Increment(ref _searchRequestVersion);
        return (requestId, _activeSearchCts.Token);
    }

    private void CancelActiveSearchOperation()
    {
        _activeSearchCts?.Cancel();
        _activeSearchCts?.Dispose();
        _activeSearchCts = null;
    }

    private bool IsSearchRequestCurrent(int requestId)
    {
        return requestId == Volatile.Read(ref _searchRequestVersion);
    }

    private void UpdateTypingHint(string? keyword)
    {
        keyword = keyword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            UpdateSearchStatus(string.Empty);
            return;
        }

        if (_searchMode == MapSearchMode.Poi)
        {
            UpdateSearchStatus(keyword.Length < 2
                ? LocalizationService.Instance.T("Map.TypeMorePoi")
                : LocalizationService.Instance.T("Map.PoiTapSearchHint"));
            return;
        }

        UpdateSearchStatus(keyword.Length < 3
            ? LocalizationService.Instance.T("Map.TypeMoreAddress")
            : LocalizationService.Instance.T("Map.AddressTapSearchHint"));
    }

    private async Task<string?> EvaluateMapScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        if (!_isMapReady || _isPageDisposing || MapWebView.Handler is null)
            return null;

        var lockTaken = false;

        try
        {
            await _mapScriptSemaphore.WaitAsync(cancellationToken);
            lockTaken = true;

            cancellationToken.ThrowIfCancellationRequested();
            return await MainThread.InvokeOnMainThreadAsync(() => MapWebView.EvaluateJavaScriptAsync(script));
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map script error: {ex.Message}");
            return null;
        }
        finally
        {
            if (lockTaken)
            {
                _mapScriptSemaphore.Release();
            }
        }
    }

    private void CancelPendingOperations()
    {
        _activeSearchCts?.Cancel();
        _mapLoadingCts?.Cancel();
        _mapTimeoutCts?.Cancel();
        _isMapReady = false;
    }

    private void DetachEventHandlers()
    {
        MapWebView.Navigated -= OnMapWebViewNavigated;
        MapWebView.Navigating -= OnMapWebViewNavigating;
        SearchEntry.Completed -= OnSearchCompleted;
        SearchEntry.TextChanged -= OnSearchTextChanged;
        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;
        ThemeService.Instance.PropertyChanged -= OnThemeChanged;
        AppSettingsService.Instance.PropertyChanged -= OnAppSettingsChanged;
        AppSettingsService.Instance.SettingsSaved -= OnSettingsSaved;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private bool CanAnimateMapLoadingChrome()
    {
        return !_isPageDisposing &&
               Handler is not null &&
               Window is not null &&
               MapWebView.Handler is not null &&
               MapLoadingOverlay.Handler is not null;
    }

    private void ShowMapRetryPanel(string title, string subtitle)
    {
        _mapLoadingCts?.Cancel();
        _mapTimeoutCts?.Cancel();

        MapSkeletonRows.IsVisible = false;
        MapRetryMessageLabel.Text = title;
        MapRetrySubLabel.Text = subtitle;
        MapRetryPanel.IsVisible = true;

        MapLoadingOverlay.IsVisible = true;
        MapLoadingOverlay.Opacity = 1;
        SetLocateButtonState(isBusy: false, isEnabled: false);
    }

    private async void OnRetryMapTapped(object? sender, TappedEventArgs e)
    {
        if (sender is VisualElement element)
        {
            await element.ScaleTo(0.92, 60, Easing.CubicIn);
            await element.ScaleTo(1d, 120, Easing.CubicOut);
        }

        await ReloadMapAsync();
    }

    private void FinishMapLoadingStateWithoutAnimation()
    {
        try
        {
            if (MapWebView.Handler is not null)
            {
                MapWebView.Opacity = 1;
            }

            MapLoadingOverlay.IsVisible = false;
            MapLoadingOverlay.Opacity = 1;
        }
        catch (ObjectDisposedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map loading state cleanup skipped: {ex.Message}");
        }
    }

    private static string GetPreferredSearchLanguageTag()
    {
        return LocalizationService.Instance.Language switch
        {
            "en" => "en-US",
            "cn" => "zh-CN",
            "jp" => "ja-JP",
            "kr" => "ko-KR",
            "fr" => "fr-FR",
            _ => "vi-VN"
        };
    }

    private enum MapSearchMode
    {
        Poi,
        Address
    }

    private enum SearchTrigger
    {
        ManualSubmit
    }

    private enum MapSearchSuggestionKind
    {
        Poi,
        Address
    }

    private sealed class MapSearchSuggestion
    {
        public MapSearchSuggestionKind Kind { get; init; }
        public string PlaceId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string BadgeText { get; init; } = string.Empty;
        public Color AccentBackground { get; init; } = Colors.Transparent;
        public Color AccentForeground { get; init; } = Colors.Black;
    }

    private sealed class MapFocusResult
    {
        public bool Found { get; set; }
        public int MatchCount { get; set; }
        public string Title { get; set; } = string.Empty;
        public double DistanceMeters { get; set; }
    }

    private sealed class OnlineSearchResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    private sealed class MapPlaceInteropPoint
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double RadiusMeters { get; init; }
        public string Image { get; init; } = string.Empty;
        public IReadOnlyList<string> GalleryImages { get; init; } = Array.Empty<string>();
    }

    private sealed class MapTourInteropState
    {
        public int TourId { get; init; }
        public int ActiveSequenceOrder { get; init; }
        public bool UsesRoadRouting { get; init; }
        public IReadOnlyList<MapTourInteropStop> Stops { get; init; } = Array.Empty<MapTourInteropStop>();
        public IReadOnlyList<TourRoutePointDto> Path { get; init; } = Array.Empty<TourRoutePointDto>();
    }

    private sealed class MapTourInteropStop
    {
        public string PlaceId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public int SequenceOrder { get; init; }
        public string Image { get; init; } = string.Empty;
    }

    private sealed class MapStringPayload
    {
        public string ViewDetails { get; init; } = string.Empty;
        public string NearestLabel { get; init; } = string.Empty;
        public string CurrentLocationTitle { get; init; } = string.Empty;
        public string SearchResultTitle { get; init; } = string.Empty;
        public string NearestPrefix { get; init; } = string.Empty;
    }

    private sealed class MapBehaviorPayload
    {
        public bool ShowPoiRadius { get; init; }
        public double PoiRadiusMeters { get; init; }
        public int AutoFocusIdleSeconds { get; init; }
        public double CameraMoveThresholdMeters { get; init; }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Latitude { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Longitude { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        public string NameOrTitle => Name ?? DisplayName;
    }

    public sealed class MapTourViewModel
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int StopCount { get; init; }
        public double DistanceKm { get; init; }
        public int EstimatedDurationMinutes { get; init; }
        public bool UsesRoadRouting { get; init; }
        public TourRoutePreviewDto? RoutePreview { get; init; }
        public string CoverImageUrl { get; init; } = string.Empty;
        public IReadOnlyList<MapTourStopViewModel> Stops { get; init; } = Array.Empty<MapTourStopViewModel>();
        public string DistanceText => $"{DistanceKm:0.0} km";
        public string DurationText => $"{Math.Max(EstimatedDurationMinutes, 1)} phút";
        public string RouteStatusText => UsesRoadRouting ? "Có tuyến đường đi bộ giữa các điểm" : "Tuyến tham quan theo POI";

        public double ResolveDistanceToStop(int sequenceOrder)
        {
            var segment = RoutePreview?.Segments.FirstOrDefault(item => item.SequenceOrder == sequenceOrder);
            return segment?.DistanceKm ?? 0d;
        }

        public int ResolveDurationToStop(int sequenceOrder)
        {
            var distanceKm = ResolveDistanceToStop(sequenceOrder);
            if (distanceKm <= 0 || RoutePreview?.WalkingSpeedKph is not > 0)
            {
                return 0;
            }

            return (int)Math.Ceiling(distanceKm / RoutePreview.WalkingSpeedKph * 60d);
        }
    }

    public sealed class MapTourStopViewModel
    {
        public string PlaceId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public int SequenceOrder { get; init; }
        public string ImageUrl { get; init; } = string.Empty;
    }
}
