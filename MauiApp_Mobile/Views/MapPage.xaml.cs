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
    private static readonly JsonSerializerOptions JsonInteropOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private readonly SemaphoreSlim _locationSemaphore = new(1, 1);
    private readonly SemaphoreSlim _mapScriptSemaphore = new(1, 1);
    private readonly ObservableCollection<MapSearchSuggestion> _searchResults = new();
    private readonly Dictionary<string, IReadOnlyList<OnlineSearchResult>> _addressSearchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _mapImageDataCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isMapLoaded;
    private bool _isMapReady;
    private bool _hasAnimatedChrome;
    private MapSearchMode _searchMode = MapSearchMode.Poi;
    private CancellationTokenSource? _activeSearchCts;
    private CancellationTokenSource? _mapLoadingCts;
    private int _searchRequestVersion;

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
            _ = RefreshMapPlacesAsync();
            _ = TryFocusPendingPlaceAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _activeSearchCts?.Cancel();
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
        await ApplyMapStringsAsync();
        await CompleteMapLoadingStateAsync();
        await TryFocusPendingPlaceAsync();

        if (!string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            UpdateTypingHint(SearchEntry.Text);
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
            await ApplyMapStringsAsync();

            var script =
                $"window.showCurrentLocation && window.showCurrentLocation({location.Latitude.ToString(CultureInfo.InvariantCulture)}, {location.Longitude.ToString(CultureInfo.InvariantCulture)});";
            var rawNearest = await EvaluateMapScriptAsync(script);
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

    private void UpdateSearchStatus(string message)
    {
        var isVisible = !string.IsNullOrWhiteSpace(message);
        if (SearchStatusLabel.Text == message && SearchStatusLabel.IsVisible == isVisible)
            return;

        SearchStatusLabel.Text = message;
        SearchStatusLabel.IsVisible = isVisible;
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
        if (!_isMapReady)
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
        public string Image { get; init; } = string.Empty;
        public IReadOnlyList<string> GalleryImages { get; init; } = Array.Empty<string>();
    }

    private sealed class MapStringPayload
    {
        public string ViewDetails { get; init; } = string.Empty;
        public string NearestLabel { get; init; } = string.Empty;
        public string CurrentLocationTitle { get; init; } = string.Empty;
        public string SearchResultTitle { get; init; } = string.Empty;
        public string NearestPrefix { get; init; } = string.Empty;
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
