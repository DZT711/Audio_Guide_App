using System.Collections.ObjectModel;
using Microsoft.Maui.Dispatching;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

public partial class MainPage : ContentPage
{
    private const double PlaceDetailOpenTopInset = 16;
    private const double PlaceDetailFallbackClosedOffset = 520;
    private const double PlaceDetailHalfVisibleRatio = 0.58;

    private readonly List<PlaceItem> _allPlaces = new();
    private string _selectedCategory = "Tất cả";
    private bool _isPlaceDetailVisible;
    private PlaceItem? _selectedPlace;
    private double _detailSheetStartY;
    private double _placeDetailExpandedY = PlaceDetailOpenTopInset;
    private double _placeDetailHalfY = 180;
    private double _placeDetailClosedY = PlaceDetailFallbackClosedOffset;
    private bool _hasLoadedPlaces;
    private bool _hasAnimatedPage;
    private bool _isPlaceGalleryTransitionRunning;
    private bool _isRefreshing;
    private bool _isPlacesAtTop = true;
    private bool _canStartPullToRefresh;
    private double _pullRefreshDistance;
    private CancellationTokenSource? _placesLoadingCts;
    private IDispatcherTimer? _placeGalleryTimer;
    private const double PullRefreshThreshold = 78;
    private const double PullRefreshMaxDistance = 96;

    public ObservableCollection<PlaceItem> Places { get; set; } = new();
    public ObservableCollection<PlaceGallerySlide> SelectedPlaceGallery { get; } = new();

    public bool IsPlaceDetailVisible
    {
        get => _isPlaceDetailVisible;
        set
        {
            _isPlaceDetailVisible = value;
            OnPropertyChanged();
        }
    }

    public PlaceItem? SelectedPlace
    {
        get => _selectedPlace;
        set
        {
            _selectedPlace = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DetailPriorityText));
            RefreshSelectedPlaceGallery(resetPosition: true);
        }
    }

    public string DetailPriorityText => SelectedPlace == null
        ? string.Empty
        : $"{LocalizationService.Instance.T("Places.DetailPriority")} {SelectedPlace.PriorityText}";

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value)
                return;

            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public MainPage()
    {
        InitializeComponent();

        BindingContext = this;
        ApplyTexts();
        UpdateFilterSelectionUI();
        PlacesCollectionView.IsVisible = false;
        EmptyStateLayout.IsVisible = false;

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            ApplyTexts();
            UpdateCount();
            OnPropertyChanged(nameof(DetailPriorityText));
            RefreshSelectedPlaceGallery(resetPosition: false);
        };

        ThemeService.Instance.PropertyChanged += (_, _) =>
        {
            UpdateFilterSelectionUI();
            UpdateFilterHeader();
            RefreshSelectedPlaceGallery(resetPosition: false);
        };
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdatePlaceDetailSheetLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasLoadedPlaces)
        {
            _ = LoadPlacesAndHandleNavigationAsync();
        }
        else if (!_hasAnimatedPage)
        {
            _hasAnimatedPage = true;
            _ = UiEffectsService.AnimateEntranceAsync(CountLabel, PlacesCollectionView);
        }
        else
        {
            _ = TryOpenPendingPlaceAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _placesLoadingCts?.Cancel();
        StopPlaceGalleryAutoplay();
    }

    private async Task LoadPlacesAsync(bool forceRefresh = false)
    {
        if (_hasLoadedPlaces && !forceRefresh)
            return;

        _hasLoadedPlaces = true;
        PlacesLoadingOverlay.IsVisible = true;
        PlacesLoadingOverlay.Opacity = 1;
        PlacesCollectionView.Opacity = 0;
        PlacesCollectionView.IsVisible = false;
        EmptyStateLayout.IsVisible = false;

        _placesLoadingCts?.Cancel();
        _placesLoadingCts = new CancellationTokenSource();

        _ = UiEffectsService.RunSkeletonPulseAsync(
            _placesLoadingCts.Token,
            PlacesCountSkeleton,
            PlacesCardSkeleton1,
            PlacesCardSkeleton2,
            PlacesCardSkeleton3);

        try
        {
            await Task.Delay(420);

            _allPlaces.Clear();
            var places = await PlaceCatalogService.Instance.GetPlacesAsync(forceRefresh, _placesLoadingCts.Token);
            _allPlaces.AddRange(places);
            ApplyFilter();

            _placesLoadingCts.Cancel();

            PlacesCollectionView.IsVisible = Places.Count > 0;
            await Task.WhenAll(
                PlacesLoadingOverlay.FadeToAsync(0, 180, Easing.CubicOut),
                PlacesCollectionView.FadeToAsync(1, 220, Easing.CubicOut));

            PlacesLoadingOverlay.IsVisible = false;
            PlacesLoadingOverlay.Opacity = 1;

            if (!_hasAnimatedPage)
            {
                _hasAnimatedPage = true;
                await UiEffectsService.AnimateEntranceAsync(CountLabel, PlacesCollectionView);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _placesLoadingCts.Cancel();
            PlacesLoadingOverlay.IsVisible = false;
            PlacesLoadingOverlay.Opacity = 1;
            PlacesCollectionView.IsVisible = Places.Count > 0;
            EmptyStateLayout.IsVisible = _allPlaces.Count == 0;
            UpdateCount();
            UpdateFilterHeader();
        }
    }

    private async Task LoadPlacesAndHandleNavigationAsync()
    {
        await LoadPlacesAsync();
        await TryOpenPendingPlaceAsync();
    }

    private async Task RefreshPlacesAsync()
    {
        try
        {
            IsRefreshing = true;
            SetPullRefreshState(42, "Đang cập nhật...");
            await LoadPlacesAsync(forceRefresh: true);
            await TryOpenPendingPlaceAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (_allPlaces.Count == 0)
            {
                ApplyFilter();
            }
        }
        finally
        {
            IsRefreshing = false;
            await ResetPullRefreshHeaderAsync();
        }
    }

    private void OnPlacesScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        _isPlacesAtTop = e.VerticalOffset <= 0;
    }

    private async void OnPlacesPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (IsRefreshing)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _canStartPullToRefresh = _isPlacesAtTop;
                break;

            case GestureStatus.Running:
                if (!_canStartPullToRefresh)
                {
                    return;
                }

                if (e.TotalY <= 0)
                {
                    SetPullRefreshState(0, "Kéo xuống để cập nhật");
                    return;
                }

                var displayDistance = Math.Min(PullRefreshMaxDistance, e.TotalY * 0.45);
                SetPullRefreshState(
                    displayDistance,
                    displayDistance >= PullRefreshThreshold ? "Thả để cập nhật" : "Kéo xuống để cập nhật");
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!_canStartPullToRefresh)
                {
                    return;
                }

                _canStartPullToRefresh = false;

                if (_pullRefreshDistance >= PullRefreshThreshold)
                {
                    await RefreshPlacesAsync();
                }
                else
                {
                    await ResetPullRefreshHeaderAsync();
                }
                break;
        }
    }

    private void SetPullRefreshState(double height, string message)
    {
        _pullRefreshDistance = height;
        PullToRefreshLabel.Text = message;
        PullToRefreshHeader.IsVisible = height > 0;
        PullToRefreshHeader.HeightRequest = height;
        PullToRefreshHeader.Opacity = height <= 0 ? 0 : Math.Min(1, height / 28);
    }

    private async Task ResetPullRefreshHeaderAsync()
    {
        _pullRefreshDistance = 0;

        if (!PullToRefreshHeader.IsVisible)
        {
            PullToRefreshHeader.HeightRequest = 0;
            PullToRefreshHeader.Opacity = 0;
            return;
        }

        await Task.WhenAll(
            PullToRefreshHeader.FadeToAsync(0, 120, Easing.CubicOut),
            PullToRefreshHeader.LayoutToAsync(
                new Rect(PullToRefreshHeader.X, PullToRefreshHeader.Y, PullToRefreshHeader.Width, 0),
                120,
                Easing.CubicOut));

        PullToRefreshHeader.HeightRequest = 0;
        PullToRefreshHeader.IsVisible = false;
        PullToRefreshLabel.Text = "Kéo xuống để cập nhật";
        PullToRefreshHeader.Opacity = 1;
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Places.Title");
        SearchEntry.Placeholder = LocalizationService.Instance.T("Places.Search");
        EmptyTitleLabel.Text = LocalizationService.Instance.T("Places.EmptyTitle");
        EmptySubtitleLabel.Text = LocalizationService.Instance.T("Places.EmptySubtitle");

        FilterPopupTitleLabel.Text = LocalizationService.Instance.T("Filter.Title");
        FilterAllLabel.Text = LocalizationService.Instance.T("Filter.All");
        FilterSignatureDishLabel.Text = LocalizationService.Instance.T("Filter.SignatureDish");
        FilterFamousRestaurantLabel.Text = LocalizationService.Instance.T("Filter.FamousRestaurant");
        FilterDrinksLabel.Text = LocalizationService.Instance.T("Filter.Drinks");
        FilterFoodCultureLabel.Text = LocalizationService.Instance.T("Filter.FoodCulture");
        FilterUtilityLabel.Text = LocalizationService.Instance.T("Filter.Utility");

        UpdateFilterHeader();
        UpdateCount();
    }

    private void RefreshSelectedPlaceGallery(bool resetPosition)
    {
        SelectedPlaceGallery.Clear();

        if (SelectedPlace is null)
        {
            StopPlaceGalleryAutoplay();
            return;
        }

        foreach (var slide in BuildPlaceGallery(SelectedPlace))
        {
            SelectedPlaceGallery.Add(slide);
        }

        if (resetPosition && PlaceDetailCarousel is not null && SelectedPlaceGallery.Count > 0)
        {
            PlaceDetailCarousel.Position = 0;
        }

        if (IsPlaceDetailVisible)
        {
            StartPlaceGalleryAutoplay();
        }
    }

    private IReadOnlyList<PlaceGallerySlide> BuildPlaceGallery(PlaceItem place)
    {
        var primaryGreen = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
        var primaryGreenDark = ThemeService.Instance.GetColor("PrimaryGreenDark", "#148F40");
        var bodyText = ThemeService.Instance.GetColor("BodyText", "#1E3250");
        var infoText = ThemeService.Instance.GetColor("InfoText", "#2563EB");
        var warningText = ThemeService.Instance.GetColor("WarningText", "#CA8A04");
        var softOrange = ThemeService.Instance.GetColor("SoftOrange", "#FFE8D8");
        var softPurple = ThemeService.Instance.GetColor("SoftPurple", "#EFF6FF");
        var galleryImages = place.GalleryImages.Count > 0
            ? place.GalleryImages
            : [place.Image];

        return
        [
            CreateGallerySlide(
                GetGalleryImage(galleryImages, 0),
                place.Category,
                LocalizationService.Instance.T("Places.GalleryOverview"),
                place.Name,
                place.Description,
                DetailPriorityText,
                place.CategoryColor,
                primaryGreenDark,
                place.CategoryColor,
                place.CategoryTextColor),
            CreateGallerySlide(
                GetGalleryImage(galleryImages, 1),
                "GPS",
                LocalizationService.Instance.T("Places.GalleryVisit"),
                place.Address,
                $"{place.RadiusText} • {place.GpsText}",
                place.Phone,
                infoText,
                bodyText,
                softPurple,
                infoText),
            CreateGallerySlide(
                GetGalleryImage(galleryImages, 2),
                LocalizationService.Instance.T("Places.GalleryAudio"),
                LocalizationService.Instance.T("Places.GalleryAudio"),
                place.Name,
                place.AudioDescription,
                place.Website,
                warningText,
                primaryGreen,
                softOrange,
                warningText)
        ];
    }

    private static string GetGalleryImage(IReadOnlyList<string> images, int index)
    {
        if (images.Count == 0)
            return string.Empty;

        return images[index % images.Count];
    }

    private static PlaceGallerySlide CreateGallerySlide(
        string image,
        string badgeText,
        string eyebrow,
        string title,
        string subtitle,
        string footerText,
        Color gradientStart,
        Color gradientEnd,
        Color badgeBackground,
        Color badgeForeground)
    {
        return new PlaceGallerySlide
        {
            Image = image,
            BadgeText = badgeText,
            Eyebrow = eyebrow,
            Title = title,
            Subtitle = subtitle,
            FooterText = footerText,
            GradientStart = gradientStart,
            GradientEnd = gradientEnd,
            BadgeBackground = badgeBackground,
            BadgeForeground = badgeForeground
        };
    }

    private void StartPlaceGalleryAutoplay()
    {
        StopPlaceGalleryAutoplay();

        if (!IsPlaceDetailVisible || SelectedPlaceGallery.Count < 2 || Dispatcher is null)
            return;

        _placeGalleryTimer = Dispatcher.CreateTimer();
        _placeGalleryTimer.Interval = TimeSpan.FromSeconds(5);
        _placeGalleryTimer.Tick += OnPlaceGalleryTimerTick;
        _placeGalleryTimer.Start();
    }

    private void StopPlaceGalleryAutoplay()
    {
        if (_placeGalleryTimer is null)
            return;

        _placeGalleryTimer.Stop();
        _placeGalleryTimer.Tick -= OnPlaceGalleryTimerTick;
        _placeGalleryTimer = null;
    }

    private async void OnPlaceGalleryTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaceDetailVisible || SelectedPlaceGallery.Count < 2)
        {
            StopPlaceGalleryAutoplay();
            return;
        }

        if (_isPlaceGalleryTransitionRunning)
            return;

        _isPlaceGalleryTransitionRunning = true;

        var nextPosition = PlaceDetailCarousel.Position + 1;
        if (nextPosition >= SelectedPlaceGallery.Count)
        {
            nextPosition = 0;
        }

        try
        {
            PlaceDetailCarousel.ScrollTo(nextPosition, -1, ScrollToPosition.Center, true);
            await Task.Delay(320);
        }
        finally
        {
            _isPlaceGalleryTransitionRunning = false;
        }
    }

    private void OnPlaceDetailCarouselPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        if (!IsPlaceDetailVisible || _isPlaceGalleryTransitionRunning || _placeGalleryTimer is null)
            return;

        StartPlaceGalleryAutoplay();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = SearchEntry.Text?.Trim().ToLower() ?? string.Empty;

        var filtered = _allPlaces
            .Where(place =>
                (_selectedCategory == "Tất cả" || place.Category == _selectedCategory) &&
                (string.IsNullOrWhiteSpace(keyword) || place.Name.ToLower().Contains(keyword)))
            .ToList();

        Places.Clear();
        foreach (var item in filtered)
        {
            Places.Add(item);
        }

        PlacesCollectionView.ItemsSource = null;
        PlacesCollectionView.ItemsSource = Places;

        EmptyStateLayout.IsVisible = _allPlaces.Count > 0 && Places.Count == 0;
        PlacesCollectionView.IsVisible = _allPlaces.Count > 0 && Places.Count > 0 && !PlacesLoadingOverlay.IsVisible;

        UpdateCount();
        UpdateFilterSelectionUI();
        UpdateFilterHeader();
    }

    private void UpdateCount()
    {
        CountLabel.Text = $"{Places.Count} {LocalizationService.Instance.T("Places.CountSuffix")}";
    }

    private void UpdateFilterHeader()
    {
        if (_selectedCategory == "Tất cả")
        {
            FilterLabel.Text = LocalizationService.Instance.T("Places.Filter");
            FilterLabel.TextColor = ThemeService.Instance.GetColor("BodyText", "#243B5A");
            return;
        }

        FilterLabel.Text = $"{LocalizationService.Instance.T("Places.Filter")}: {_selectedCategory}";
        FilterLabel.TextColor = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
    }

    private async void OnToggleFilterPopup(object sender, TappedEventArgs e)
    {
        await UiEffectsService.TogglePopupAsync(FilterPopup, !FilterPopup.IsVisible);
    }

    private void ApplyCategory(string category)
    {
        _selectedCategory = _selectedCategory == category ? "Tất cả" : category;
        _ = UiEffectsService.TogglePopupAsync(FilterPopup, false);
        ApplyFilter();
    }

    private void UpdateFilterSelectionUI()
    {
        ResetFilterItem(FilterAllItem, FilterAllLabel, FilterAllIndicator);
        ResetFilterItem(FilterSignatureDishItem, FilterSignatureDishLabel, FilterSignatureDishIndicator);
        ResetFilterItem(FilterFamousRestaurantItem, FilterFamousRestaurantLabel, FilterFamousRestaurantIndicator);
        ResetFilterItem(FilterDrinksItem, FilterDrinksLabel, FilterDrinksIndicator);
        ResetFilterItem(FilterFoodCultureItem, FilterFoodCultureLabel, FilterFoodCultureIndicator);
        ResetFilterItem(FilterUtilityItem, FilterUtilityLabel, FilterUtilityIndicator);

        switch (_selectedCategory)
        {
            case "Tất cả":
                SelectFilterItem(FilterAllItem, FilterAllLabel, FilterAllIndicator);
                break;
            case "Món ăn đặc trưng":
                SelectFilterItem(FilterSignatureDishItem, FilterSignatureDishLabel, FilterSignatureDishIndicator);
                break;
            case "Quán nổi tiếng":
                SelectFilterItem(FilterFamousRestaurantItem, FilterFamousRestaurantLabel, FilterFamousRestaurantIndicator);
                break;
            case "Đồ uống":
                SelectFilterItem(FilterDrinksItem, FilterDrinksLabel, FilterDrinksIndicator);
                break;
            case "Văn hóa ẩm thực":
                SelectFilterItem(FilterFoodCultureItem, FilterFoodCultureLabel, FilterFoodCultureIndicator);
                break;
            case "Tiện ích":
                SelectFilterItem(FilterUtilityItem, FilterUtilityLabel, FilterUtilityIndicator);
                break;
        }
    }

    private void ResetFilterItem(Grid item, Label label, BoxView indicator)
    {
        item.BackgroundColor = Colors.Transparent;
        label.TextColor = ThemeService.Instance.GetColor("BodyText", "#243B5A");
        label.FontAttributes = FontAttributes.None;
        indicator.IsVisible = false;
    }

    private void SelectFilterItem(Grid item, Label label, BoxView indicator)
    {
        item.BackgroundColor = ThemeService.Instance.GetColor("SoftGreen", "#E8F7EE");
        label.TextColor = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
        label.FontAttributes = FontAttributes.Bold;
        indicator.IsVisible = true;
    }

    private void OnFilterAllTapped(object sender, TappedEventArgs e) => ApplyCategory("Tất cả");
    private void OnFilterSignatureDishTapped(object sender, TappedEventArgs e) => ApplyCategory("Món ăn đặc trưng");
    private void OnFilterFamousRestaurantTapped(object sender, TappedEventArgs e) => ApplyCategory("Quán nổi tiếng");
    private void OnFilterDrinksTapped(object sender, TappedEventArgs e) => ApplyCategory("Đồ uống");
    private void OnFilterFoodCultureTapped(object sender, TappedEventArgs e) => ApplyCategory("Văn hóa ẩm thực");
    private void OnFilterUtilityTapped(object sender, TappedEventArgs e) => ApplyCategory("Tiện ích");

    private async void OnPlaceTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceItem item)
            return;

        await ShowPlaceDetailAsync(item);
    }

    private async void OnPlaceDetailPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsPlaceDetailVisible)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _detailSheetStartY = PlaceDetailSheet.TranslationY;
                break;

            case GestureStatus.Running:
                var nextY = Math.Clamp(_detailSheetStartY + e.TotalY, _placeDetailExpandedY, _placeDetailClosedY);
                PlaceDetailSheet.TranslationY = nextY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var targetY = ResolvePlaceDetailSnapTarget(PlaceDetailSheet.TranslationY, e.TotalY);
                if (targetY >= _placeDetailClosedY - 1)
                {
                    await HidePlaceDetail();
                }
                else
                {
                    await PlaceDetailSheet.TranslateToAsync(0, targetY, 170, Easing.CubicOut);
                }
                break;
        }
    }

    private async void OnClosePlaceDetail(object sender, EventArgs e)
    {
        await HidePlaceDetail();
    }

    private async void OnPlaceDetailHandleTapped(object sender, TappedEventArgs e)
    {
        await HidePlaceDetail();
    }

    private async void OnPlaceDetailBackdropTapped(object sender, TappedEventArgs e)
    {
        await HidePlaceDetail();
    }

    private async Task HidePlaceDetail()
    {
        if (!IsPlaceDetailVisible)
            return;

        StopPlaceGalleryAutoplay();
        UpdatePlaceDetailSheetLayout();
        await PlaceDetailSheet.TranslateToAsync(0, _placeDetailClosedY, 230, Easing.CubicIn);
        IsPlaceDetailVisible = false;
        SelectedPlace = null;
    }

    private void UpdatePlaceDetailSheetLayout()
    {
        if (Height <= 0)
            return;

        var maxSheetHeight = Math.Max(360, Height * 0.88);
        PlaceDetailSheet.MaximumHeightRequest = maxSheetHeight;

        _placeDetailExpandedY = PlaceDetailOpenTopInset;
        var halfVisibleHeight = Math.Max(320, Height * PlaceDetailHalfVisibleRatio);
        _placeDetailHalfY = Math.Clamp(
            maxSheetHeight - halfVisibleHeight,
            _placeDetailExpandedY + 72,
            _placeDetailExpandedY + 300);
        _placeDetailClosedY = Math.Max(PlaceDetailFallbackClosedOffset, maxSheetHeight + 48);

        if (!IsPlaceDetailVisible)
        {
            PlaceDetailSheet.TranslationY = _placeDetailClosedY;
        }
    }

    private async Task TryOpenPendingPlaceAsync()
    {
        var pendingPlaceId = PlaceNavigationService.Instance.ConsumePendingPlaceId();
        if (string.IsNullOrWhiteSpace(pendingPlaceId))
            return;

        var place = PlaceCatalogService.Instance.FindById(pendingPlaceId);
        if (place is null)
            return;

        if (FilterPopup.IsVisible)
        {
            await UiEffectsService.TogglePopupAsync(FilterPopup, false);
        }

        await ShowPlaceDetailAsync(place);
    }

    private async Task ShowPlaceDetailAsync(PlaceItem item)
    {
        SelectedPlace = item;
        IsPlaceDetailVisible = true;

        await Task.Yield();
        UpdatePlaceDetailSheetLayout();
        PlaceDetailSheet.TranslationY = _placeDetailClosedY;
        PlaceDetailCarousel.Position = 0;
        StartPlaceGalleryAutoplay();
        await PlaceDetailSheet.TranslateToAsync(0, _placeDetailHalfY, 300, Easing.CubicOut);
    }

    private double ResolvePlaceDetailSnapTarget(double currentY, double totalDragY)
    {
        var expandedHalfMid = (_placeDetailExpandedY + _placeDetailHalfY) / 2;
        var halfClosedMid = (_placeDetailHalfY + _placeDetailClosedY) / 2;

        if (totalDragY < -80)
            return _placeDetailExpandedY;

        if (totalDragY > 160 && currentY > _placeDetailHalfY + 24)
            return _placeDetailClosedY;

        if (currentY <= expandedHalfMid)
            return _placeDetailExpandedY;

        if (currentY <= halfClosedMid)
            return _placeDetailHalfY;

        return _placeDetailClosedY;
    }

    private void OnPlayTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceItem item)
            return;

        item.IsPlayed = !item.IsPlayed;
        if (item.IsPlayed)
        {
            HistoryService.Instance.AddToHistory(item);
        }
    }
}
