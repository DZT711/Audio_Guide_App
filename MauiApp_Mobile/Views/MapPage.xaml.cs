using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using MauiApp_Mobile.Services;
#if ANDROID
using Microsoft.Maui.Handlers;
#endif

namespace MauiApp_Mobile.Views;

public partial class MapPage : ContentPage
{
    private static readonly HttpClient SearchHttpClient = CreateSearchHttpClient();
    private readonly SemaphoreSlim _locationSemaphore = new(1, 1);
    private bool _isMapLoaded;
    private bool _isMapReady;
    private bool _hasAnimatedChrome;
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _mapLoadingCts;

    public MapPage()
    {
        InitializeComponent();

        MapWebView.Navigated += OnMapWebViewNavigated;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged += OnSearchTextChanged;

        ApplyTexts();
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
        SearchEntry.Placeholder = LocalizationService.Instance.T("Map.Search");
        MapTipLabel.Text = LocalizationService.Instance.T("Map.LocateHint");
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        ApplyTexts();
    }

    private void OnThemeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
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

        await ApplyMapThemeAsync();
        await CompleteMapLoadingStateAsync();

        if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            await SearchMapAsync(SearchEntry.Text, showNotFoundMessage: false);
        }
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
        await SearchMapAsync(SearchEntry.Text, showNotFoundMessage: true);
    }

    private async void OnSearchIconTapped(object? sender, TappedEventArgs e)
    {
        _searchDebounceCts?.Cancel();
        await SearchMapAsync(SearchEntry.Text, showNotFoundMessage: true);
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
            await SearchMapAsync(e.NewTextValue, showNotFoundMessage: false);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task SearchMapAsync(string? keyword, bool showNotFoundMessage)
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
            await MapWebView.EvaluateJavaScriptAsync("window.resetMapView && window.resetMapView();");
            UpdateSearchStatus(string.Empty);
            return;
        }

        try
        {
            var jsKeyword = JsonSerializer.Serialize(keyword);
            var rawResult = await MapWebView.EvaluateJavaScriptAsync(
                $"window.focusPlaceByKeyword && window.focusPlaceByKeyword({jsKeyword});");

            var searchResult = ParseSearchResult(rawResult);

            if (searchResult.Found)
            {
                var status = searchResult.MatchCount > 1
                    ? $"Đã tìm thấy {searchResult.MatchCount} kết quả. Đang mở: {searchResult.Title}"
                    : $"Đã tìm thấy: {searchResult.Title}";
                UpdateSearchStatus(status);
                return;
            }

            var onlineResult = await SearchOnlineAsync(keyword);
            if (onlineResult is not null)
            {
                var titleJson = JsonSerializer.Serialize(onlineResult.Name);
                var descriptionJson = JsonSerializer.Serialize(onlineResult.Address);
                var script =
                    $"window.showSearchResult && window.showSearchResult({onlineResult.Latitude.ToString(CultureInfo.InvariantCulture)}, {onlineResult.Longitude.ToString(CultureInfo.InvariantCulture)}, {titleJson}, {descriptionJson});";
                await MapWebView.EvaluateJavaScriptAsync(script);
                UpdateSearchStatus($"Đã tìm thấy địa điểm: {onlineResult.Name}");
                return;
            }

            UpdateSearchStatus(showNotFoundMessage
                ? $"Không tìm thấy địa điểm phù hợp với \"{keyword}\"."
                : string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map search error: {ex.Message}");
            UpdateSearchStatus("Không thể thực hiện tìm kiếm lúc này.");
        }
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
            await MapWebView.EvaluateJavaScriptAsync(script);
            UpdateSearchStatus("Đã định vị vị trí hiện tại trên bản đồ.");
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

    private static MapSearchResult ParseSearchResult(string? rawResult)
    {
        if (string.IsNullOrWhiteSpace(rawResult))
            return new MapSearchResult();

        var normalized = rawResult.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = JsonSerializer.Deserialize<string>(normalized) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(normalized))
            return new MapSearchResult();

        return JsonSerializer.Deserialize<MapSearchResult>(normalized) ?? new MapSearchResult();
    }

    private static async Task<OnlineSearchResult?> SearchOnlineAsync(string keyword)
    {
        var requestUri =
            $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&accept-language=vi&q={Uri.EscapeDataString(keyword)}";

        var results = await SearchHttpClient.GetFromJsonAsync<List<NominatimResult>>(requestUri);
        var first = results?.FirstOrDefault();
        if (first is null)
            return null;

        if (!double.TryParse(first.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(first.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        return new OnlineSearchResult
        {
            Latitude = latitude,
            Longitude = longitude,
            Name = first.NameOrTitle,
            Address = first.DisplayName
        };
    }

    private static HttpClient CreateSearchHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartTourismMaui/1.0");
        return client;
    }

    private sealed class MapSearchResult
    {
        public bool Found { get; set; }
        public int MatchCount { get; set; }
        public string Title { get; set; } = string.Empty;
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
