using MauiApp_Mobile.Services;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
#if ANDROID
using Microsoft.Maui.Handlers;
#endif

namespace MauiApp_Mobile.Views;

public partial class MapPage : ContentPage
{
    private static readonly HttpClient SearchHttpClient = CreateSearchHttpClient();
    private bool _isMapLoaded = false;
    private bool _isMapReady;
    private CancellationTokenSource? _searchDebounceCts;

    public MapPage()
    {
        InitializeComponent();

        MapWebView.Navigated += OnMapWebViewNavigated;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged += OnSearchTextChanged;
        ApplyTexts();
        LocalizationService.Instance.PropertyChanged += (_, _) => ApplyTexts();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        if (!_isMapLoaded)
        {
            LoadMap();
            _isMapLoaded = true;
        }
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Map.Title");
        SearchEntry.Placeholder = LocalizationService.Instance.T("Map.Search");
    }

    private async void LoadMap()
    {
        try 
        {
            // Cách 1: Sử dụng Leaflet + CartoDB (nhẹ, không yêu cầu API Key)
            string fileName = "leaflet_map.html";
            string htmlContent = string.Empty;

            // Load HTML từ Resources/Raw
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            using var reader = new StreamReader(stream);
            htmlContent = await reader.ReadToEndAsync();

            // Cách 3: Sửa lỗi "Referer" - Thêm custom User-Agent cho WebView
#if ANDROID
            // Chỉ áp dụng cho Android
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("CustomUserAgent", (handler, view) =>
            {
                if (handler.PlatformView is Android.Webkit.WebView webView)
                {
                    string ua = webView.Settings.UserAgentString;
                    // Gán một tên bản đạo hàng để server OSM không chặn
                    webView.Settings.UserAgentString = "TourGuideApp_POC_User";
                }
            });
#endif

            // Set HTML content cho WebView
            MapWebView.Source = new HtmlWebViewSource { Html = htmlContent };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading Leaflet map: {ex.Message}");
            await DisplayAlert("Lỗi", "Không thể tải bản đồ: " + ex.Message, "OK");
        }
    }

    private async void OnMapWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _isMapReady = e.Result == WebNavigationResult.Success;

        if (_isMapReady && !string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            await SearchMapAsync(SearchEntry.Text, showNotFoundMessage: false);
        }
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        _searchDebounceCts?.Cancel();
        await SearchMapAsync(SearchEntry.Text, showNotFoundMessage: true);
    }

    private async void OnSearchIconTapped(object? sender, TappedEventArgs e)
    {
        _searchDebounceCts?.Cancel();
        await SearchMapAsync(SearchEntry.Text, showNotFoundMessage: true);
    }

    private async void OnCurrentLocationTapped(object? sender, TappedEventArgs e)
    {
        await FocusCurrentLocationAsync();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Ensure search still works even if the XAML event wiring is stale in the running build.
        SearchEntry.Completed -= OnSearchCompleted;
        SearchEntry.Completed += OnSearchCompleted;
        SearchEntry.TextChanged -= OnSearchTextChanged;
        SearchEntry.TextChanged += OnSearchTextChanged;
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
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
            string jsKeyword = JsonSerializer.Serialize(keyword);
            string rawResult = await MapWebView.EvaluateJavaScriptAsync(
                $"window.focusPlaceByKeyword && window.focusPlaceByKeyword({jsKeyword});");

            var searchResult = ParseSearchResult(rawResult);

            if (searchResult.Found)
            {
                string status = searchResult.MatchCount > 1
                    ? $"Đã tìm thấy {searchResult.MatchCount} kết quả. Đang mở: {searchResult.Title}"
                    : $"Đã tìm thấy: {searchResult.Title}";
                UpdateSearchStatus(status);
                return;
            }

            var onlineResult = await SearchOnlineAsync(keyword);
            if (onlineResult is not null)
            {
                string titleJson = JsonSerializer.Serialize(onlineResult.Name);
                string descriptionJson = JsonSerializer.Serialize(onlineResult.Address);
                string script =
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

    private async Task FocusCurrentLocationAsync()
    {
        if (!_isMapReady)
        {
            UpdateSearchStatus("Bản đồ đang tải, vui lòng thử lại sau.");
            return;
        }

        try
        {
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

            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            if (location is null)
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                location = await Geolocation.Default.GetLocationAsync(request);
            }

            if (location is null)
            {
                UpdateSearchStatus("Không lấy được vị trí hiện tại.");
                return;
            }

            string script =
                $"window.showCurrentLocation && window.showCurrentLocation({location.Latitude.ToString(CultureInfo.InvariantCulture)}, {location.Longitude.ToString(CultureInfo.InvariantCulture)});";
            await MapWebView.EvaluateJavaScriptAsync(script);
            UpdateSearchStatus("Đã lấy vị trí hiện tại.");
        }
        catch (FeatureNotEnabledException)
        {
            UpdateSearchStatus("Vui lòng bật GPS để lấy vị trí hiện tại.");
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

        string normalized = rawResult.Trim();
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
        string requestUri =
            $"https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&accept-language=vi&q={Uri.EscapeDataString(keyword)}";

        var results = await SearchHttpClient.GetFromJsonAsync<List<NominatimResult>>(requestUri);
        var first = results?.FirstOrDefault();
        if (first is null)
            return null;

        if (!double.TryParse(first.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude) ||
            !double.TryParse(first.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
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
