using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;
#if ANDROID
using Microsoft.Maui.Handlers;
#endif

namespace MauiApp_Mobile.Views;

public partial class MapPage : ContentPage
{
    private static readonly HttpClient SearchHttpClient = CreateSearchHttpClient();
    private static readonly JsonSerializerOptions CamelCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly SemaphoreSlim _locationSemaphore = new(1, 1);
    private readonly ObservableCollection<MapSearchSuggestion> _searchResults = new();
    private bool _isMapLoaded;
    private bool _isMapReady;
    private bool _hasAnimatedChrome;
    private MapSearchMode _searchMode = MapSearchMode.Poi;
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _mapLoadingCts;

    public MapPage()
    {
        InitializeComponent();

        SearchResultsView.ItemsSource = _searchResults;

        MapWebView.Navigated += OnMapWebViewNavigated;
        MapWebView.Navigating += OnMapWebViewNavigating;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged += OnSearchTextChanged;

        ApplyTexts();
        UpdateSearchModeVisuals();
        SetLocateButtonState(isBusy: false, isEnabled: false);

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        ThemeService.Instance.PropertyChanged += OnThemeChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasAnimatedChrome)
        {
            _hasAnimatedChrome = true;
            _ = UiEffectsService.AnimateEntranceAsync(MapTipChip, CurrentLocationButton);
        }

        if (!_isMapLoaded)
        {
            LoadMap();
            _isMapLoaded = true;
        }
        else
        {
            _ = ApplyMapThemeAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _searchDebounceCts?.Cancel();
        _mapLoadingCts?.Cancel();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        SearchEntry.Completed -= OnSearchCompleted;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged -= OnSearchTextChanged;
        SearchEntry.TextChanged += OnSearchTextChanged;
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
        ApplyTexts();
    }

    private void OnThemeChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateSearchModeVisuals();

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_isMapReady)
            {
                await ApplyMapThemeAsync();
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
            await DisplayAlertAsync("Lỗi", "Không thể tải bản đồ: " + ex.Message, "OK");
        }
    }

    private void StartMapLoadingState()
    {
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
    }

    private async Task CompleteMapLoadingStateAsync()
    {
        _mapLoadingCts?.Cancel();

        if (!MapLoadingOverlay.IsVisible)
            return;

        await Task.WhenAll(
            MapWebView.FadeToAsync(1, 240, Easing.CubicOut),
            MapLoadingOverlay.FadeToAsync(0, 180, Easing.CubicOut));

        MapLoadingOverlay.IsVisible = false;
        MapLoadingOverlay.Opacity = 1;
    }

    private async void OnMapWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _isMapReady = e.Result == WebNavigationResult.Success;
        SetLocateButtonState(isBusy: false, isEnabled: _isMapReady);

        if (!_isMapReady)
        {
            UpdateSearchStatus("Bản đồ chưa sẵn sàng. Hãy thử tải lại trang.");
            return;
        }

        await SyncPlacesToMapAsync();
        await ApplyMapThemeAsync();
        await CompleteMapLoadingStateAsync();

        if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            await SearchMapAsync(SearchEntry.Text, autoFocusSingle: false, showNotFoundMessage: false);
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

        var mapPlacesJson = JsonSerializer.Serialize(PlaceCatalogService.Instance.GetMapPoints(), CamelCaseJsonOptions);
        await MapWebView.EvaluateJavaScriptAsync($"window.setPlaces && window.setPlaces({mapPlacesJson});");
    }

    private async Task ApplyMapThemeAsync()
    {
        if (!_isMapReady)
            return;

        var themeJson = JsonSerializer.Serialize(ThemeService.Instance.MapThemeKey);
        await MapWebView.EvaluateJavaScriptAsync($"window.applyMapTheme && window.applyMapTheme({themeJson});");
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        _searchDebounceCts?.Cancel();
        await SearchMapAsync(SearchEntry.Text, autoFocusSingle: true, showNotFoundMessage: true);
    }

    private async void OnSearchIconTapped(object? sender, TappedEventArgs e)
    {
        _searchDebounceCts?.Cancel();
        await SearchMapAsync(SearchEntry.Text, autoFocusSingle: true, showNotFoundMessage: true);
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        try
        {
            await Task.Delay(350, token);
            await SearchMapAsync(e.NewTextValue, autoFocusSingle: false, showNotFoundMessage: false);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task SearchMapAsync(string? keyword, bool autoFocusSingle, bool showNotFoundMessage)
    {
        keyword = keyword?.Trim() ?? string.Empty;

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
            await MapWebView.EvaluateJavaScriptAsync("window.resetMapView && window.resetMapView();");
            UpdateSearchStatus(string.Empty);
            return;
        }

        try
        {
            if (_searchMode == MapSearchMode.Poi)
            {
                var poiResults = PlaceCatalogService.Instance.SearchByName(keyword)
                    .Select(CreatePoiSuggestion)
                    .ToList();

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
                    await SelectSearchResultAsync(poiResults[0]);
                    return;
                }

                UpdateSearchStatus(poiResults.Count == 1
                    ? $"Đã tìm thấy POI: {poiResults[0].Title}"
                    : $"Đã tìm thấy {poiResults.Count} POI. Hãy chọn một kết quả.");
                return;
            }

            var addressResults = (await SearchOnlineAsync(keyword, 6))
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
                await SelectSearchResultAsync(addressResults[0]);
                return;
            }

            UpdateSearchStatus(addressResults.Count == 1
                ? $"Đã tìm thấy địa chỉ: {addressResults[0].Title}"
                : $"Đã tìm thấy {addressResults.Count} địa chỉ. Hãy chọn một kết quả.");
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

        await SelectSearchResultAsync(result);
    }

    private async Task SelectSearchResultAsync(MapSearchSuggestion result)
    {
        if (!_isMapReady)
            return;

        ClearSearchResults();

        if (result.Kind == MapSearchSuggestionKind.Poi)
        {
            var placeIdJson = JsonSerializer.Serialize(result.PlaceId);
            var rawResult = await MapWebView.EvaluateJavaScriptAsync(
                $"window.focusPlaceById && window.focusPlaceById({placeIdJson});");

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
        await MapWebView.EvaluateJavaScriptAsync(script);
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

    private async Task SwitchSearchModeAsync(MapSearchMode mode)
    {
        if (_searchMode == mode)
            return;

        _searchMode = mode;
        ApplyTexts();
        UpdateSearchModeVisuals();
        ClearSearchResults();

        if (string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            await MapWebView.EvaluateJavaScriptAsync("window.resetMapView && window.resetMapView();");
            UpdateSearchStatus(string.Empty);
            return;
        }

        await SearchMapAsync(SearchEntry.Text, autoFocusSingle: false, showNotFoundMessage: false);
    }

    private async void OnCurrentLocationTapped(object? sender, TappedEventArgs e)
    {
        await FocusCurrentLocationAsync();
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

            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permissionStatus != PermissionStatus.Granted)
            {
                permissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (permissionStatus != PermissionStatus.Granted)
            {
                UpdateSearchStatus("Chưa cấp quyền vị trí nên không thể định vị.");
                return;
            }

            var location = await TryGetCurrentLocationAsync();
            if (location is null)
            {
                UpdateSearchStatus("Không lấy được vị trí hiện tại. Hãy thử ở khu vực thoáng hơn.");
                return;
            }

            await ApplyMapThemeAsync();

            var script =
                $"window.showCurrentLocation && window.showCurrentLocation({location.Latitude.ToString(CultureInfo.InvariantCulture)}, {location.Longitude.ToString(CultureInfo.InvariantCulture)});";
            var rawNearest = await MapWebView.EvaluateJavaScriptAsync(script);
            var focusResult = ParseFocusResult(rawNearest);

            if (focusResult.Found && !string.IsNullOrWhiteSpace(focusResult.Title) && focusResult.DistanceMeters > 0)
            {
                UpdateSearchStatus(
                    $"Đã định vị vị trí hiện tại. Gần nhất: {focusResult.Title} ({focusResult.DistanceMeters:0}m).");
            }
            else
            {
                UpdateSearchStatus("Đã định vị vị trí hiện tại trên bản đồ.");
            }
        }
        catch (FeatureNotEnabledException)
        {
            UpdateSearchStatus("Vui lòng bật GPS để lấy vị trí hiện tại.");
        }
        catch (FeatureNotSupportedException)
        {
            UpdateSearchStatus("Thiết bị này chưa hỗ trợ định vị GPS.");
        }
        catch (PermissionException)
        {
            UpdateSearchStatus("Ứng dụng không có quyền truy cập vị trí.");
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

    private async Task OpenPlaceDetailAsync(string placeId)
    {
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

    private static async Task<Location?> TryGetCurrentLocationAsync()
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(12));
            var currentLocation = await Geolocation.Default.GetLocationAsync(request);
            if (currentLocation is not null)
            {
                return currentLocation;
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException)
        {
        }

        return await Geolocation.Default.GetLastKnownLocationAsync();
    }

    private void SetLocateButtonState(bool isBusy, bool isEnabled)
    {
        CurrentLocationSpinner.IsVisible = isBusy;
        CurrentLocationSpinner.IsRunning = isBusy;
        CurrentLocationIcon.IsVisible = !isBusy;
        CurrentLocationButton.InputTransparent = !isEnabled;
        CurrentLocationButton.Opacity = isEnabled ? (isBusy ? 0.92 : 1) : 0.62;
    }

    private void UpdateSearchStatus(string message)
    {
        SearchStatusLabel.Text = message;
        SearchStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
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

        return JsonSerializer.Deserialize<MapFocusResult>(normalized) ?? new MapFocusResult();
    }

    private static async Task<List<OnlineSearchResult>> SearchOnlineAsync(string keyword, int limit)
    {
        var requestUri =
            $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit={Math.Max(1, limit)}&accept-language=vi&q={Uri.EscapeDataString(keyword)}";

        var results = await SearchHttpClient.GetFromJsonAsync<List<NominatimResult>>(requestUri) ?? [];

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
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartTourismMaui/1.0");
        return client;
    }

    private enum MapSearchMode
    {
        Poi,
        Address
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
}
