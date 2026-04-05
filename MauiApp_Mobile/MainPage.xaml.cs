using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Maui.Dispatching;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile;

public partial class MainPage : ContentPage
{
    private const double PlaceDetailOpenTopInset = 16;
    private const double PlaceDetailFallbackClosedOffset = 520;
    private const double PlaceDetailHalfVisibleRatio = 0.58;
    private const string AllCategoryValue = "__all__";
    private const string AllVoiceValue = "__all_voice__";
    private const string MaleVoiceValue = "Male";
    private const string FemaleVoiceValue = "Female";
    private static readonly TimeSpan LiveReloadInterval = TimeSpan.FromSeconds(12);

    private readonly List<PlaceItem> _allPlaces = new();
    private string _selectedCategoryValue = AllCategoryValue;
    private string _selectedVoiceValue = AllVoiceValue;
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
    private bool _isSilentRefreshing;
    private bool _isAudioListExpanded;
    private bool _isPlacesAtTop = true;
    private bool _canStartPullToRefresh;
    private double _pullRefreshDistance;
    private string _placesSignature = string.Empty;
    private string _categoriesSignature = string.Empty;
    private CancellationTokenSource? _placesLoadingCts;
    private IDispatcherTimer? _placeGalleryTimer;
    private IDispatcherTimer? _liveReloadTimer;
    private const double PullRefreshThreshold = 78;
    private const double PullRefreshMaxDistance = 96;

    public ObservableCollection<PlaceItem> Places { get; set; } = new();
    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = new();
    public ObservableCollection<CategoryFilterOption> VoiceFilters { get; } = new();
    public ObservableCollection<PlaceGallerySlide> SelectedPlaceGallery { get; } = new();
    public ObservableCollection<PlaceDetailAudioTrack> SelectedPlaceAudioTracks { get; } = new();

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

    public bool IsAudioListExpanded
    {
        get => _isAudioListExpanded;
        set
        {
            if (_isAudioListExpanded == value)
                return;

            _isAudioListExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AudioListExpandIcon));
        }
    }

    public string AudioListExpandIcon => IsAudioListExpanded ? "˄" : "˅";

    public string AudioTrackSummaryText => SelectedPlaceAudioTracks.Count == 0
        ? "Chưa có audio"
        : $"{SelectedPlaceAudioTracks.Count} audio khả dụng";

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
        SyncCategoryFilters([]);
        SyncVoiceFilters();
        PlacesCollectionView.IsVisible = false;
        EmptyStateLayout.IsVisible = false;

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            ApplyTexts();
            UpdateCount();
            OnPropertyChanged(nameof(DetailPriorityText));
            OnPropertyChanged(nameof(AudioTrackSummaryText));
            RefreshSelectedPlaceGallery(resetPosition: false);
        };

        ThemeService.Instance.PropertyChanged += (_, _) =>
        {
            UpdateCategorySelectionState();
            UpdateVoiceSelectionState();
            UpdateFilterHeader();
            RefreshSelectedPlaceGallery(resetPosition: false);
        };

        AppDataModeService.Instance.PropertyChanged += OnAppDataModeChanged;

        AudioPlaybackService.Instance.PlaybackStateChanged += (_, currentTrack) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MarkPlayingTrack(currentTrack?.Id);
            });
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
        UpdateLiveReloadTimer();

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
            _ = RefreshPlacesAndHandlePendingAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _placesLoadingCts?.Cancel();
        StopPlaceGalleryAutoplay();
        StopLiveReloadTimer();
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

            var places = await PlaceCatalogService.Instance.GetPlacesAsync(forceRefresh, _placesLoadingCts.Token);
            var categories = await LoadCategoriesSafelyAsync(forceRefresh, _placesLoadingCts.Token);
            ApplyCatalogSnapshot(places, categories);

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

            if (!forceRefresh)
            {
                _ = RefreshPlacesSilentlyAsync();
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

    private async Task RefreshPlacesAndHandlePendingAsync()
    {
        await RefreshPlacesSilentlyAsync();
        await TryOpenPendingPlaceAsync();
    }

    private async Task RefreshCategoryFiltersAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await PlaceCatalogService.Instance.GetCategoriesAsync(forceRefresh, cancellationToken);
            SyncCategoryFilters(categories);
        }
        catch
        {
            SyncCategoryFilters(PlaceCatalogService.Instance.GetCategories());
        }
    }

    private async Task<IReadOnlyList<CategoryDto>> LoadCategoriesSafelyAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PlaceCatalogService.Instance.GetCategoriesAsync(forceRefresh, cancellationToken);
        }
        catch
        {
            return PlaceCatalogService.Instance.GetCategories();
        }
    }

    private async Task RefreshPlacesSilentlyAsync()
    {
        if (_isSilentRefreshing || IsRefreshing)
            return;

        try
        {
            _isSilentRefreshing = true;
            var places = await PlaceCatalogService.Instance.GetPlacesAsync(forceRefresh: AppDataModeService.Instance.IsApiEnabled);
            var categories = await LoadCategoriesSafelyAsync(forceRefresh: AppDataModeService.Instance.IsApiEnabled);

            if (!HasCatalogChanged(places, categories))
            {
                return;
            }

            ApplyCatalogSnapshot(places, categories);
        }
        catch
        {
        }
        finally
        {
            _isSilentRefreshing = false;
        }
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
        CountHintLabel.Text = LocalizationService.Instance.T("Places.CountHint");

        FilterPopupTitleLabel.Text = LocalizationService.Instance.T("Filter.Title");
        VoiceFilterPopupTitleLabel.Text = LocalizationService.Instance.T("Filter.VoiceTitle");
        SyncCategoryFilters(PlaceCatalogService.Instance.GetCategories());
        SyncVoiceFilters();

        UpdateFilterHeader();
        UpdateCount();
    }

    private void RefreshSelectedPlaceGallery(bool resetPosition)
    {
        SelectedPlaceGallery.Clear();

        if (SelectedPlace is null)
        {
            StopPlaceGalleryAutoplay();
            PlaceDetailIndicator.IsVisible = false;
            return;
        }

        foreach (var slide in BuildPlaceGallery(SelectedPlace))
        {
            SelectedPlaceGallery.Add(slide);
        }

        if (PlaceDetailIndicator is not null)
        {
            PlaceDetailIndicator.IsVisible = SelectedPlaceGallery.Count > 1;
        }

        if (PlaceDetailCarousel is not null)
        {
            PlaceDetailCarousel.Loop = SelectedPlaceGallery.Count > 1;
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

    private void RefreshSelectedPlaceAudioTracks()
    {
        SelectedPlaceAudioTracks.Clear();

        if (SelectedPlace is null)
        {
            IsAudioListExpanded = false;
            return;
        }

        foreach (var track in BuildPlaceAudioTracks(SelectedPlace))
        {
            SelectedPlaceAudioTracks.Add(track);
        }

        IsAudioListExpanded = false;
        OnPropertyChanged(nameof(AudioTrackSummaryText));
    }

    private IReadOnlyList<PlaceDetailAudioTrack> BuildPlaceAudioTracks(PlaceItem place)
    {
        var preferredVoice = GetPreferredVoiceFilterValue();

        return place.AudioTracks
            .OrderByDescending(item => DoesTrackMatchPreferredVoice(item, preferredVoice))
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.Id)
            .Select(item => new PlaceDetailAudioTrack
            {
                Id = item.Id,
                LanguageCode = item.Language,
                LanguageName = item.LanguageName ?? item.Language,
                Title = item.Title,
                Duration = FormatDuration(item.Duration),
                SourceType = item.SourceType,
                Priority = item.Priority,
                AudioUrl = item.AudioURL,
                Script = item.Script,
                IsDefault = item.IsDefault
            })
            .ToList();
    }

    private static string FormatDuration(int durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return "00:00";
        }

        var duration = TimeSpan.FromSeconds(durationSeconds);
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private static int ResolveSourceTypeOrder(string? sourceType) =>
        sourceType?.Trim().ToUpperInvariant() switch
        {
            "RECORDED" => 0,
            "HYBRID" => 1,
            _ => 2
        };

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
        var slides = new List<PlaceGallerySlide>(galleryImages.Count);

        for (var index = 0; index < galleryImages.Count; index++)
        {
            slides.Add(index switch
            {
                0 => CreateGallerySlide(
                    galleryImages[index],
                    place.Category,
                    LocalizationService.Instance.T("Places.GalleryOverview"),
                    place.Name,
                    place.Description,
                    DetailPriorityText,
                    place.CategoryColor,
                    primaryGreenDark,
                    place.CategoryColor,
                    place.CategoryTextColor),
                1 => CreateGallerySlide(
                    galleryImages[index],
                    "GPS",
                    LocalizationService.Instance.T("Places.GalleryVisit"),
                    place.Address,
                    $"{place.RadiusText} • {place.GpsText}",
                    place.Phone,
                    infoText,
                    bodyText,
                    softPurple,
                    infoText),
                2 => CreateGallerySlide(
                    galleryImages[index],
                    LocalizationService.Instance.T("Places.GalleryAudio"),
                    LocalizationService.Instance.T("Places.GalleryAudio"),
                    place.Name,
                    place.AudioDescription,
                    place.Website,
                    warningText,
                    primaryGreen,
                    softOrange,
                    warningText),
                _ => CreateGallerySlide(
                    galleryImages[index],
                    place.Category,
                    LocalizationService.Instance.T("Places.GalleryOverview"),
                    place.Name,
                    place.Description,
                    DetailPriorityText,
                    place.CategoryColor,
                    primaryGreenDark,
                    place.CategoryColor,
                    place.CategoryTextColor)
            });
        }

        return slides;
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
                (_selectedCategoryValue == AllCategoryValue || string.Equals(place.Category, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase)) &&
                MatchesVoiceFilter(place) &&
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
        UpdateFilterHeader();
    }

    private void UpdateCount()
    {
        CountLabel.Text = $"{Places.Count} {LocalizationService.Instance.T("Places.CountSuffix")}";
    }

    private void UpdateFilterHeader()
    {
        var selectedCategory = CategoryFilters.FirstOrDefault(item => item.IsSelected);
        var selectedVoice = VoiceFilters.FirstOrDefault(item => item.IsSelected);
        var hasCategoryFilter = selectedCategory is not null && !selectedCategory.IsAllOption;
        var hasVoiceFilter = selectedVoice is not null && !selectedVoice.IsAllOption;

        if (!hasCategoryFilter && !hasVoiceFilter)
        {
            FilterLabel.Text = LocalizationService.Instance.T("Places.Filter");
            FilterLabel.TextColor = ThemeService.Instance.GetColor("BodyText", "#243B5A");
            return;
        }

        var parts = new List<string>();
        if (hasCategoryFilter)
        {
            parts.Add(selectedCategory!.DisplayName);
        }

        if (hasVoiceFilter)
        {
            parts.Add(selectedVoice!.DisplayName);
        }

        FilterLabel.Text = $"{LocalizationService.Instance.T("Places.Filter")}: {string.Join(" • ", parts)}";
        FilterLabel.TextColor = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
    }

    private async void OnToggleFilterPopup(object sender, TappedEventArgs e)
    {
        await UiEffectsService.TogglePopupAsync(FilterPopup, !FilterPopup.IsVisible);
    }

    private void ApplyCategory(string categoryValue)
    {
        _selectedCategoryValue = _selectedCategoryValue == categoryValue
            ? AllCategoryValue
            : categoryValue;
        UpdateCategorySelectionState();
        ApplyFilter();
        _ = UiEffectsService.TogglePopupAsync(FilterPopup, false);
    }

    private void ApplyVoice(string voiceValue)
    {
        _selectedVoiceValue = _selectedVoiceValue == voiceValue
            ? AllVoiceValue
            : voiceValue;
        UpdateVoiceSelectionState();
        ApplyFilter();
        RefreshSelectedPlaceAudioTracks();
        _ = UiEffectsService.TogglePopupAsync(FilterPopup, false);
    }

    private void SyncCategoryFilters(IEnumerable<Project_SharedClassLibrary.Contracts.CategoryDto> categories)
    {
        var categoryNames = categories
            .Where(item => item.Status == 1 && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => item.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var selectedExists = _selectedCategoryValue == AllCategoryValue ||
            categoryNames.Any(item => string.Equals(item, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase));

        if (!selectedExists)
        {
            _selectedCategoryValue = AllCategoryValue;
        }

        CategoryFilters.Clear();
        CategoryFilters.Add(new CategoryFilterOption
        {
            Value = AllCategoryValue,
            DisplayName = LocalizationService.Instance.T("Filter.All"),
            Icon = "📍",
            IsAllOption = true
        });

        foreach (var categoryName in categoryNames)
        {
            CategoryFilters.Add(new CategoryFilterOption
            {
                Value = categoryName,
                DisplayName = categoryName,
                Icon = ResolveCategoryIcon(categoryName)
            });
        }

        UpdateCategorySelectionState();
    }

    private void UpdateCategorySelectionState()
    {
        foreach (var option in CategoryFilters)
        {
            option.IsSelected = string.Equals(option.Value, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SyncVoiceFilters()
    {
        var selectedExists = _selectedVoiceValue == AllVoiceValue ||
            string.Equals(_selectedVoiceValue, MaleVoiceValue, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_selectedVoiceValue, FemaleVoiceValue, StringComparison.OrdinalIgnoreCase);

        if (!selectedExists)
        {
            _selectedVoiceValue = AllVoiceValue;
        }

        VoiceFilters.Clear();
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = AllVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.All"),
            Icon = "🎙",
            IsAllOption = true
        });
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = MaleVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.VoiceMale"),
            Icon = "♂"
        });
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = FemaleVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.VoiceFemale"),
            Icon = "♀"
        });

        UpdateVoiceSelectionState();
    }

    private void UpdateVoiceSelectionState()
    {
        foreach (var option in VoiceFilters)
        {
            option.IsSelected = string.Equals(option.Value, _selectedVoiceValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool MatchesVoiceFilter(PlaceItem place)
    {
        if (_selectedVoiceValue == AllVoiceValue)
        {
            return true;
        }

        return place.AvailableVoiceGenders.Any(item =>
            string.Equals(NormalizeVoiceGender(item), _selectedVoiceValue, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveCategoryIcon(string category)
    {
        var normalized = category.Trim().ToLowerInvariant();

        if (normalized.Contains("food") || normalized.Contains("ăn"))
            return "🍜";

        if (normalized.Contains("drink") || normalized.Contains("uống") || normalized.Contains("cafe"))
            return "🥤";

        if (normalized.Contains("bus") || normalized.Contains("stop") || normalized.Contains("transit"))
            return "🚌";

        if (normalized.Contains("history") || normalized.Contains("heritage") || normalized.Contains("historical"))
            return "🏛";

        if (normalized.Contains("landmark"))
            return "📍";

        return "🏷";
    }

    private void OnCategoryFilterTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not CategoryFilterOption option)
            return;

        ApplyCategory(option.Value);
    }

    private void OnVoiceFilterTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not CategoryFilterOption option)
            return;

        ApplyVoice(option.Value);
    }

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
        await LoadSelectedPlaceAudioTracksAsync(item);
        IsPlaceDetailVisible = true;

        await Task.Yield();
        UpdatePlaceDetailSheetLayout();
        PlaceDetailSheet.TranslationY = _placeDetailClosedY;
        PlaceDetailCarousel.Position = 0;
        StartPlaceGalleryAutoplay();
        await PlaceDetailSheet.TranslateToAsync(0, _placeDetailHalfY, 300, Easing.CubicOut);
    }

    private async Task LoadSelectedPlaceAudioTracksAsync(PlaceItem place, bool forceRefresh = false)
    {
        var previousAudioSignature = ComputeAudioSignature(place.AudioTracks);
        var audioTracks = await PlaceCatalogService.Instance.GetAudioTracksAsync(
            place.Id,
            forceRefresh && AppDataModeService.Instance.IsApiEnabled);

        var nextAudioSignature = ComputeAudioSignature(audioTracks);
        if (string.Equals(previousAudioSignature, nextAudioSignature, StringComparison.Ordinal))
        {
            return;
        }

        place.AudioTracks = audioTracks;
        place.AudioCountText = $"{audioTracks.Count} audio";
        place.AvailableVoiceGenders = audioTracks
            .Select(item => NormalizeVoiceGender(item.VoiceGender))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        RefreshSelectedPlaceAudioTracks();
        ApplyFilter();
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

    private async void OnPlayTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceItem item)
            return;

        await PlayDefaultAudioAsync(item);
    }

    private void OnToggleAudioListTapped(object sender, TappedEventArgs e)
    {
        if (SelectedPlace is null)
            return;

        IsAudioListExpanded = !IsAudioListExpanded;
    }

    private async void OnAudioTrackPlayTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceDetailAudioTrack track)
            return;

        await PlaySelectedTrackAsync(track);
    }

    private async void OnViewPlaceOnMapTapped(object sender, TappedEventArgs e)
    {
        if (SelectedPlace is null)
            return;

        try
        {
            PlaceNavigationService.Instance.RequestMapFocus(SelectedPlace.Id);
            await HidePlaceDetail();

            if (Application.Current?.Windows.FirstOrDefault()?.Page is AppShell appShell)
            {
                await appShell.NavigateToMapTabAsync();
                return;
            }

            await Shell.Current.GoToAsync("//mainTabs/map");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigate to map error: {ex.Message}");
        }
    }

    private async Task PlayDefaultAudioAsync(PlaceItem place)
    {
        try
        {
            var audioTracks = await PlaceCatalogService.Instance.GetAudioTracksAsync(
                place.Id,
                forceRefresh: AppDataModeService.Instance.IsApiEnabled);
            var preferredTrack = SelectPreferredTrack(audioTracks);

            if (preferredTrack is null)
            {
                await DisplayAlert("Audio", "POI này chưa có audio khả dụng.", "OK");
                return;
            }

            await AudioPlaybackService.Instance.PlayAsync(preferredTrack);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                place.IsPlayed = true;
                HistoryService.Instance.AddToHistory(place);
            });

            MarkPlayingTrack(preferredTrack.Id);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Audio", ex.Message, "OK");
        }
    }

    private async Task PlaySelectedTrackAsync(PlaceDetailAudioTrack track)
    {
        try
        {
            if (SelectedPlace is null)
            {
                return;
            }

            if (track.IsPlaying)
            {
                await AudioPlaybackService.Instance.StopAsync();
                MarkPlayingTrack(null);
                return;
            }

            var selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            if (selectedTrack is null)
            {
                await LoadSelectedPlaceAudioTracksAsync(SelectedPlace, forceRefresh: true);
                selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            }

            if (selectedTrack is null)
            {
                await DisplayAlert("Audio", "Không tìm thấy audio đã chọn.", "OK");
                return;
            }

            await AudioPlaybackService.Instance.PlayAsync(selectedTrack);
            SelectedPlace.IsPlayed = true;
            HistoryService.Instance.AddToHistory(SelectedPlace);
            MarkPlayingTrack(selectedTrack.Id);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Audio", ex.Message, "OK");
        }
    }

    private void MarkPlayingTrack(int? trackId)
    {
        foreach (var item in SelectedPlaceAudioTracks)
        {
            item.IsPlaying = trackId.HasValue && item.Id == trackId.Value;
        }
    }

    private void OnAppDataModeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AppDataModeService.IsApiEnabled), StringComparison.Ordinal))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateLiveReloadTimer();
            await RefreshPlacesSilentlyAsync();

            if (SelectedPlace is not null)
            {
                await LoadSelectedPlaceAudioTracksAsync(SelectedPlace, forceRefresh: true);
            }
        });
    }

    private void UpdateLiveReloadTimer()
    {
        if (AppDataModeService.Instance.IsApiEnabled)
        {
            StartLiveReloadTimer();
            return;
        }

        StopLiveReloadTimer();
    }

    private void StartLiveReloadTimer()
    {
        if (Dispatcher is null)
        {
            return;
        }

        if (_liveReloadTimer is not null)
        {
            _liveReloadTimer.Start();
            return;
        }

        _liveReloadTimer = Dispatcher.CreateTimer();
        _liveReloadTimer.Interval = LiveReloadInterval;
        _liveReloadTimer.Tick += OnLiveReloadTimerTick;
        _liveReloadTimer.Start();
    }

    private void StopLiveReloadTimer()
    {
        if (_liveReloadTimer is null)
        {
            return;
        }

        _liveReloadTimer.Stop();
    }

    private async void OnLiveReloadTimerTick(object? sender, EventArgs e)
    {
        if (!AppDataModeService.Instance.IsApiEnabled || IsRefreshing || _isSilentRefreshing)
        {
            return;
        }

        if (FilterPopup.IsVisible || SearchEntry.IsFocused || IsPlaceDetailVisible)
        {
            return;
        }

        await RefreshPlacesSilentlyAsync();

        if (SelectedPlace is not null)
        {
            await LoadSelectedPlaceAudioTracksAsync(SelectedPlace, forceRefresh: true);
        }
    }

    private void ApplyCatalogSnapshot(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories)
    {
        _placesSignature = ComputePlacesSignature(places);
        _categoriesSignature = ComputeCategoriesSignature(categories);

        _allPlaces.Clear();
        _allPlaces.AddRange(places);
        SyncCategoryFilters(categories);
        ApplyFilter();
        RefreshSelectedPlaceReference();
    }

    private bool HasCatalogChanged(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories)
    {
        var nextPlacesSignature = ComputePlacesSignature(places);
        var nextCategoriesSignature = ComputeCategoriesSignature(categories);

        return !string.Equals(_placesSignature, nextPlacesSignature, StringComparison.Ordinal)
            || !string.Equals(_categoriesSignature, nextCategoriesSignature, StringComparison.Ordinal);
    }

    private void RefreshSelectedPlaceReference()
    {
        if (SelectedPlace is null)
        {
            return;
        }

        var refreshedPlace = _allPlaces.FirstOrDefault(item =>
            string.Equals(item.Id, SelectedPlace.Id, StringComparison.OrdinalIgnoreCase));

        if (refreshedPlace is null)
        {
            return;
        }

        refreshedPlace.AudioTracks = SelectedPlace.AudioTracks;
        refreshedPlace.AudioCountText = SelectedPlace.AudioCountText;
        refreshedPlace.AvailableVoiceGenders = SelectedPlace.AvailableVoiceGenders;
        SelectedPlace = refreshedPlace;
    }

    private PublicAudioTrackDto? SelectPreferredTrack(IEnumerable<PublicAudioTrackDto> audioTracks)
    {
        var preferredVoice = GetPreferredVoiceFilterValue();

        return audioTracks
            .OrderByDescending(item => DoesTrackMatchPreferredVoice(item, preferredVoice))
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.Id)
            .FirstOrDefault();
    }

    private string? GetPreferredVoiceFilterValue() =>
        _selectedVoiceValue == AllVoiceValue
            ? null
            : _selectedVoiceValue;

    private static bool DoesTrackMatchPreferredVoice(PublicAudioTrackDto track, string? preferredVoice)
    {
        if (string.IsNullOrWhiteSpace(preferredVoice))
        {
            return false;
        }

        return string.Equals(
            NormalizeVoiceGender(track.VoiceGender),
            preferredVoice,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputePlacesSignature(IEnumerable<PlaceItem> places)
    {
        return string.Join(
            "||",
            places
                .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                {
                    var voices = string.Join(",", item.AvailableVoiceGenders.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
                    return string.Join(
                        "|",
                        item.Id,
                        item.Name,
                        item.Category,
                        item.Description,
                        item.Image,
                        item.AudioCountText,
                        voices,
                        item.PriorityText,
                        item.StatusText,
                        item.Latitude.ToString("F6", CultureInfo.InvariantCulture),
                        item.Longitude.ToString("F6", CultureInfo.InvariantCulture));
                }));
    }

    private static string ComputeCategoriesSignature(IEnumerable<CategoryDto> categories)
    {
        return string.Join(
            "||",
            categories
                .OrderBy(item => item.Id)
                .Select(item => $"{item.Id}|{item.Name}|{item.Status}"));
    }

    private static string ComputeAudioSignature(IEnumerable<PublicAudioTrackDto> audioTracks)
    {
        return string.Join(
            "||",
            audioTracks
                .OrderBy(item => item.Id)
                .Select(item => string.Join(
                    "|",
                    item.Id,
                    item.Title,
                    item.SourceType,
                    item.Priority.ToString(CultureInfo.InvariantCulture),
                    item.Language,
                    item.VoiceGender,
                    item.AudioURL,
                    item.IsDefault)));
    }

    private static string NormalizeVoiceGender(string? voiceGender)
    {
        if (string.IsNullOrWhiteSpace(voiceGender))
        {
            return string.Empty;
        }

        return voiceGender.Trim().ToUpperInvariant() switch
        {
            "MALE" => MaleVoiceValue,
            "NAM" => MaleVoiceValue,
            "FEMALE" => FemaleVoiceValue,
            "NU" => FemaleVoiceValue,
            "NỮ" => FemaleVoiceValue,
            _ => voiceGender.Trim()
        };
    }
}

public class PlaceDetailAudioTrack : BindableObject
{
    public int Id { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public string? AudioUrl { get; set; }
    public string? Script { get; set; }
    public string MetaText => IsDefault
        ? $"{LanguageName} • {SourceType} • P{Priority} • mặc định"
        : $"{LanguageName} • {SourceType} • P{Priority} • {Duration}";

    private bool _isPlaying;

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value)
                return;

            _isPlaying = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayIcon));
        }
    }

    public string PlayIcon => IsPlaying ? "🔊" : "▶";
}
