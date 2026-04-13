using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Networking;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.ViewModels;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile;

public partial class MainPage : ContentPage
{
    private const double PlaceDetailOpenTopInset = 16;
    private const double PlaceDetailFallbackClosedOffset = 520;
    private const double PlaceDetailHalfVisibleRatio = 0.58;
    private const string MaleVoiceValue = "Male";
    private const string FemaleVoiceValue = "Female";
    private static readonly TimeSpan LiveReloadInterval = TimeSpan.FromSeconds(45);

    private bool _isPlaceDetailVisible;
    private PlaceItem? _selectedPlace;
    private double _detailSheetStartY;
    private double _placeDetailExpandedY = PlaceDetailOpenTopInset;
    private double _placeDetailHalfY = 180;
    private double _placeDetailClosedY = PlaceDetailFallbackClosedOffset;
    private bool _hasLoadedPlaces;
    private bool _hasAnimatedPage;
    private bool _isPlaceGalleryTransitionRunning;
    private bool _isAudioListExpanded;
    private bool _isSilentRefreshing;
    private bool _subscriptionsAttached;
    private CancellationTokenSource? _placesLoadingCts;
    private IDispatcherTimer? _placeGalleryTimer;
    private IDispatcherTimer? _liveReloadTimer;

    public PlacesViewModel ViewModel { get; }
    public ICommand RefreshPlacesCommand { get; }

    public ObservableCollection<PlaceItem> Places => ViewModel.Places;
    public ObservableCollection<CategoryFilterOption> CategoryFilters => ViewModel.CategoryFilters;
    public ObservableCollection<CategoryFilterOption> VoiceFilters => ViewModel.VoiceFilters;
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

    public string AudioListExpandIcon => IsAudioListExpanded ? "triangle_up_filled.svg" : "triangle_down_filled.svg";

    public string AudioTrackSummaryText => SelectedPlaceAudioTracks.Count == 0
        ? "Chưa có audio"
        : $"{SelectedPlaceAudioTracks.Count} audio khả dụng";

    public string DetailPriorityText => SelectedPlace == null
        ? string.Empty
        : $"{LocalizationService.Instance.T("Places.DetailPriority")} {SelectedPlace.PriorityText}";

    public bool IsRefreshing
    {
        get => ViewModel.IsRefreshing;
        set
        {
            if (ViewModel.IsRefreshing == value)
                return;

            ViewModel.IsRefreshing = value;
            OnPropertyChanged();
            UpdatePullToRefreshVisualState();
        }
    }

    public MainPage()
    {
        ViewModel = new PlacesViewModel(PlaceCatalogService.Instance);
        RefreshPlacesCommand = new Command(async () => await RefreshPlacesAsync());
        InitializeComponent();

        BindingContext = this;
        ViewModel.PlaceRequested += ShowPlaceFromViewModelAsync;
        ViewModel.PlayRequested += PlayPlaceFromViewModelAsync;
        ApplyTexts();
        PlacesCollectionView.IsVisible = false;
        EmptyStateLayout.IsVisible = false;
        AttachSingletonSubscriptions();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdatePlaceDetailSheetLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachSingletonSubscriptions();
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
        DetachSingletonSubscriptions();
    }

    private void AttachSingletonSubscriptions()
    {
        if (_subscriptionsAttached)
        {
            return;
        }

        LocalizationService.Instance.PropertyChanged += OnLocalizationServicePropertyChanged;
        ThemeService.Instance.PropertyChanged += OnThemeServicePropertyChanged;
        AppDataModeService.Instance.PropertyChanged += OnAppDataModeChanged;
        AppSettingsService.Instance.PropertyChanged += OnAppSettingsChanged;
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _subscriptionsAttached = true;
        UpdateConnectionStatusChip();
    }

    private void DetachSingletonSubscriptions()
    {
        if (!_subscriptionsAttached)
        {
            return;
        }

        LocalizationService.Instance.PropertyChanged -= OnLocalizationServicePropertyChanged;
        ThemeService.Instance.PropertyChanged -= OnThemeServicePropertyChanged;
        AppDataModeService.Instance.PropertyChanged -= OnAppDataModeChanged;
        AppSettingsService.Instance.PropertyChanged -= OnAppSettingsChanged;
        AudioPlaybackService.Instance.PlaybackStateChanged -= OnPlaybackStateChanged;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _subscriptionsAttached = false;
    }

    private void OnLocalizationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ApplyTexts();
            OnPropertyChanged(nameof(DetailPriorityText));
            OnPropertyChanged(nameof(AudioTrackSummaryText));
            RefreshSelectedPlaceGallery(resetPosition: false);
        });
    }

    private void OnThemeServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateCategorySelectionState();
            UpdateVoiceSelectionState();
            UpdateFilterHeader();
            RefreshSelectedPlaceGallery(resetPosition: false);
            UpdateConnectionStatusChip();
        });
    }

    private void OnAppSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AppSettingsService.ApiModeEnabled), StringComparison.Ordinal))
            return;

        MainThread.BeginInvokeOnMainThread(UpdateConnectionStatusChip);
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateConnectionStatusChip);

    private void OnPlaybackStateChanged(object? sender, PublicAudioTrackDto? currentTrack)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdatePlaybackIndicators(currentTrack, AudioPlaybackService.Instance.IsLoading);
        });
    }

    private Task ShowPlaceFromViewModelAsync(PlaceItem place) => ShowPlaceDetailAsync(place);

    private Task PlayPlaceFromViewModelAsync(PlaceItem place) => PlayDefaultAudioAsync(place);

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
            await ViewModel.LoadCatalogAsync(forceRefresh, _placesLoadingCts.Token);
            RefreshSelectedPlaceReference();
            UpdatePlaybackIndicators(AudioPlaybackService.Instance.CurrentTrack, AudioPlaybackService.Instance.IsLoading);

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
            EmptyStateLayout.IsVisible = ViewModel.AllPlaces.Count == 0;
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
            var forceRefresh = AppDataModeService.Instance.IsApiEnabled &&
                ViewModel.NeedsRemoteRefresh(TimeSpan.FromMinutes(1));

            if (!await ViewModel.RefreshIfChangedAsync(forceRefresh))
            {
                return;
            }

            RefreshSelectedPlaceReference();
            UpdatePlaybackIndicators(AudioPlaybackService.Instance.CurrentTrack, AudioPlaybackService.Instance.IsLoading);
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
            PullToRefreshLabel.Text = "Đang cập nhật dữ liệu...";
            await LoadPlacesAsync(forceRefresh: true);
            await TryOpenPendingPlaceAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (ViewModel.AllPlaces.Count == 0)
            {
                ApplyFilter();
            }
        }
        finally
        {
            IsRefreshing = false;
            PullToRefreshLabel.Text = "Kéo xuống để cập nhật";
        }
    }

    private async void OnPlacesRefreshing(object? sender, EventArgs e)
    {
        await RefreshPlacesAsync();
    }

    private void UpdatePullToRefreshVisualState()
    {
        if (PullToRefreshHeader is null)
        {
            return;
        }

        PullToRefreshHeader.IsVisible = IsRefreshing;
        PullToRefreshHeader.HeightRequest = IsRefreshing ? 34 : 0;
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
        ViewModel.ApplyTexts();
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
        UpdatePlaybackIndicators(AudioPlaybackService.Instance.CurrentTrack, AudioPlaybackService.Instance.IsLoading);
        _ = SyncSelectedPlaceAudioDownloadStatesAsync();
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

    private async Task SyncSelectedPlaceAudioDownloadStatesAsync()
    {
        if (SelectedPlace is null || SelectedPlaceAudioTracks.Count == 0)
        {
            return;
        }

        var sourceTracks = SelectedPlace.AudioTracks.ToDictionary(item => item.Id);

        foreach (var track in SelectedPlaceAudioTracks.ToList())
        {
            if (!sourceTracks.TryGetValue(track.Id, out var sourceTrack))
            {
                continue;
            }

            var snapshot = await AudioDownloadService.Instance.GetSnapshotAsync(sourceTrack);
            if (!SelectedPlaceAudioTracks.Contains(track))
            {
                continue;
            }

            MainThread.BeginInvokeOnMainThread(() => track.ApplyDownloadSnapshot(snapshot));
        }
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
        ViewModel.SearchText = e.NewTextValue ?? string.Empty;
    }

    private void ApplyFilter()
    {
        ViewModel.ApplyFilter();
        EmptyStateLayout.IsVisible = ViewModel.ShowEmptyState;
        PlacesCollectionView.IsVisible = ViewModel.AllPlaces.Count > 0 && Places.Count > 0 && !PlacesLoadingOverlay.IsVisible;
    }

    private void UpdateCount()
    {
        ViewModel.RefreshDisplayState();
    }

    private void UpdateFilterHeader()
    {
        ViewModel.RefreshDisplayState();
    }

    private async void OnToggleFilterPopup(object sender, TappedEventArgs e)
    {
        await UiEffectsService.TogglePopupAsync(FilterPopup, !FilterPopup.IsVisible);
    }

    private void ApplyCategory(string categoryValue)
    {
        ViewModel.ApplyCategory(categoryValue);
        ApplyFilter();
        _ = UiEffectsService.TogglePopupAsync(FilterPopup, false);
    }

    private void ApplyVoice(string voiceValue)
    {
        ViewModel.ApplyVoice(voiceValue);
        ApplyFilter();
        RefreshSelectedPlaceAudioTracks();
        _ = UiEffectsService.TogglePopupAsync(FilterPopup, false);
    }

    private void SyncCategoryFilters(IEnumerable<Project_SharedClassLibrary.Contracts.CategoryDto> categories)
    {
        ViewModel.SyncCategoryFilters(categories);
    }

    private void UpdateCategorySelectionState()
    {
        ViewModel.UpdateCategorySelectionState();
    }

    private void SyncVoiceFilters()
    {
        ViewModel.SyncVoiceFilters();
    }

    private void UpdateVoiceSelectionState()
    {
        ViewModel.UpdateVoiceSelectionState();
    }

    private bool MatchesVoiceFilter(PlaceItem place)
        => ViewModel.MatchesVoiceFilter(place);

    private static bool HasPlayableAudio(PlaceItem place)
    {
        if (place.AudioTracks.Count > 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(place.AudioCountText))
        {
            return false;
        }

        var numericPrefix = new string(place.AudioCountText
            .TakeWhile(character => char.IsDigit(character))
            .ToArray());

        return int.TryParse(numericPrefix, out var audioCount) && audioCount > 0;
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

        var place = ViewModel.FindPlaceById(pendingPlaceId);
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
        RefreshSelectedPlaceAudioTracks();
        IsPlaceDetailVisible = true;

        await Task.Yield();
        UpdatePlaceDetailSheetLayout();
        PlaceDetailSheet.TranslationY = _placeDetailClosedY;
        PlaceDetailCarousel.Position = 0;
        StartPlaceGalleryAutoplay();
        await PlaceDetailSheet.TranslateToAsync(0, _placeDetailHalfY, 300, Easing.CubicOut);

        _ = LoadSelectedPlaceAudioTracksSafelyAsync(item);
    }

    private async Task LoadSelectedPlaceAudioTracksAsync(PlaceItem place, bool forceRefresh = false)
    {
        if (!forceRefresh && place.AudioTracks.Count > 0)
        {
            RefreshSelectedPlaceAudioTracks();
            ApplyFilter();
            return;
        }

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

    private async Task LoadSelectedPlaceAudioTracksSafelyAsync(PlaceItem place, bool forceRefresh = false)
    {
        try
        {
            await LoadSelectedPlaceAudioTracksAsync(place, forceRefresh);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSelectedPlaceAudioTracksAsync failed for place {place.Id}: {ex}");
        }
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

        if (item.IsAudioLoading)
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

        if (track.IsLoading)
            return;

        await PlaySelectedTrackAsync(track);
    }

    private async void OnAudioTrackDownloadTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceDetailAudioTrack track)
            return;

        if (track.IsDownloading || track.IsDownloadDisabled)
            return;

        await DownloadSelectedTrackAsync(track);
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
        if (place.IsAudioLoading)
        {
            return;
        }

        if (place.IsPlaying)
        {
            await AudioPlaybackService.Instance.StopAsync();
            return;
        }

        MarkPendingPlayback(place.Id, null);

        try
        {
            var audioTracks = place.AudioTracks;
            if (audioTracks.Count == 0)
            {
                audioTracks = await PlaceCatalogService.Instance.GetAudioTracksAsync(place.Id);
            }

            if (audioTracks.Count == 0 && AppDataModeService.Instance.IsApiEnabled)
            {
                audioTracks = await PlaceCatalogService.Instance.GetAudioTracksAsync(place.Id, forceRefresh: true);
            }

            var preferredTrack = SelectPreferredTrack(audioTracks);

            if (preferredTrack is null)
            {
                MarkPendingPlayback(null, null);
                await DisplayAlertAsync("Audio", "POI này chưa có audio khả dụng.", "OK");
                return;
            }

            MarkPendingPlayback(place.Id, preferredTrack.Id);
            var playbackTrack = await AudioDownloadService.Instance.ResolvePlayableTrackAsync(preferredTrack);
            await MainThread.InvokeOnMainThreadAsync(() => HistoryService.Instance.AddToHistory(place));
            await AudioPlaybackService.Instance.PlayAsync(playbackTrack);
        }
        catch (OperationCanceledException)
        {
            MarkPendingPlayback(null, null);
        }
        catch (Exception ex)
        {
            MarkPendingPlayback(null, null);
            await DisplayAlertAsync("Audio", ex.Message, "OK");
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
                MarkPlayingTrack(null, isLoading: false);
                return;
            }

            MarkPendingPlayback(SelectedPlace.Id, track.Id);
            var selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            if (selectedTrack is null)
            {
                await LoadSelectedPlaceAudioTracksAsync(SelectedPlace);
                selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            }

            if (selectedTrack is null && AppDataModeService.Instance.IsApiEnabled)
            {
                await LoadSelectedPlaceAudioTracksAsync(SelectedPlace, forceRefresh: true);
                selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            }

            if (selectedTrack is null)
            {
                MarkPendingPlayback(null, null);
                await DisplayAlertAsync("Audio", "Không tìm thấy audio đã chọn.", "OK");
                return;
            }

            var playbackTrack = await AudioDownloadService.Instance.ResolvePlayableTrackAsync(selectedTrack);
            HistoryService.Instance.AddToHistory(SelectedPlace);
            await AudioPlaybackService.Instance.PlayAsync(playbackTrack);
        }
        catch (OperationCanceledException)
        {
            MarkPendingPlayback(null, null);
        }
        catch (Exception ex)
        {
            MarkPendingPlayback(null, null);
            await DisplayAlertAsync("Audio", ex.Message, "OK");
        }
    }

    private async Task DownloadSelectedTrackAsync(PlaceDetailAudioTrack track)
    {
        try
        {
            if (SelectedPlace is null)
            {
                return;
            }

            var selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            if (selectedTrack is null)
            {
                await LoadSelectedPlaceAudioTracksAsync(SelectedPlace);
                selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            }

            if (selectedTrack is null && AppDataModeService.Instance.IsApiEnabled)
            {
                await LoadSelectedPlaceAudioTracksAsync(SelectedPlace, forceRefresh: true);
                selectedTrack = SelectedPlace.AudioTracks.FirstOrDefault(item => item.Id == track.Id);
            }

            if (selectedTrack is null)
            {
                track.ApplyDownloadFailure("Không tìm thấy audio đã chọn để tải.");
                return;
            }

            track.BeginDownload();
            var progress = new Progress<AudioDownloadProgressUpdate>(update =>
            {
                MainThread.BeginInvokeOnMainThread(() => track.UpdateDownloadProgress(update));
            });

            var snapshot = await AudioDownloadService.Instance.DownloadAsync(selectedTrack, progress);
            await MainThread.InvokeOnMainThreadAsync(() => track.ApplyDownloadSnapshot(snapshot));
        }
        catch (OperationCanceledException)
        {
            track.ApplyDownloadFailure("Tải audio đã bị hủy.");
        }
        catch (Exception ex)
        {
            track.ApplyDownloadFailure(ex.Message);
            System.Diagnostics.Debug.WriteLine($"Audio download failed for track {track.Id}: {ex}");
        }
    }

    private void UpdatePlaybackIndicators(PublicAudioTrackDto? currentTrack, bool isLoading)
    {
        var playingPlaceId = currentTrack?.LocationId.ToString(CultureInfo.InvariantCulture);

        foreach (var place in ViewModel.AllPlaces)
        {
            var isActivePlace = !string.IsNullOrWhiteSpace(playingPlaceId)
                && string.Equals(place.Id, playingPlaceId, StringComparison.OrdinalIgnoreCase);

            place.IsAudioLoading = isActivePlace && isLoading;
            place.IsPlaying = isActivePlace && !isLoading;
        }

        MarkPlayingTrack(currentTrack?.Id, isLoading);
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

    private void MarkPendingPlayback(string? placeId, int? trackId)
    {
        foreach (var place in ViewModel.AllPlaces)
        {
            var isPendingPlace = !string.IsNullOrWhiteSpace(placeId)
                && string.Equals(place.Id, placeId, StringComparison.OrdinalIgnoreCase);

            place.IsAudioLoading = isPendingPlace;
            place.IsPlaying = false;
        }

        foreach (var item in SelectedPlaceAudioTracks)
        {
            var isPendingTrack = trackId.HasValue && item.Id == trackId.Value;
            item.IsLoading = isPendingTrack;
            item.IsPlaying = false;
        }
    }

    private void MarkPlayingTrack(int? trackId, bool isLoading)
    {
        foreach (var item in SelectedPlaceAudioTracks)
        {
            var isActiveTrack = trackId.HasValue && item.Id == trackId.Value;
            item.IsLoading = isActiveTrack && isLoading;
            item.IsPlaying = isActiveTrack && !isLoading;
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

            if (SelectedPlace is not null && SelectedPlace.AudioTracks.Count == 0)
            {
                await LoadSelectedPlaceAudioTracksSafelyAsync(SelectedPlace);
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
    }

    private void ApplyCatalogSnapshot(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories)
    {
        ViewModel.ApplyCatalogSnapshot(places, categories);
        RefreshSelectedPlaceReference();
        UpdatePlaybackIndicators(AudioPlaybackService.Instance.CurrentTrack, AudioPlaybackService.Instance.IsLoading);
    }

    private bool HasCatalogChanged(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories) =>
        ViewModel.HasCatalogChanged(places, categories);

    private void RefreshSelectedPlaceReference()
    {
        if (SelectedPlace is null)
        {
            return;
        }

        var refreshedPlace = ViewModel.FindPlaceById(SelectedPlace.Id);

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

    private string? GetPreferredVoiceFilterValue() => ViewModel.PreferredVoiceFilterValue;

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
    private bool _isLoading;
    private TrackDownloadVisualState _downloadState;
    private double _downloadProgress;
    private string _downloadDetailText = string.Empty;

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

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsDownloading => _downloadState == TrackDownloadVisualState.Downloading;

    public bool IsDownloadStatusVisible => _downloadState != TrackDownloadVisualState.None;

    public bool IsDownloadProgressVisible => IsDownloading;

    public bool HasDownloadDetail => !string.IsNullOrWhiteSpace(DownloadDetailText);

    public bool IsDownloadedTrack => _downloadState == TrackDownloadVisualState.Downloaded;

    public bool IsDownloadDisabled => IsDownloadedTrack || IsDownloading || string.IsNullOrWhiteSpace(AudioUrl);

    public double DownloadButtonOpacity => IsDownloadDisabled ? 0.62d : 1d;

    public string DownloadButtonGlyph => IsDownloadedTrack ? "✓" : "↓";

    public double DownloadProgress
    {
        get => _downloadProgress;
        private set
        {
            if (Math.Abs(_downloadProgress - value) < 0.0001d)
                return;

            _downloadProgress = value;
            OnPropertyChanged();
        }
    }

    public string DownloadStatusIcon => _downloadState switch
    {
        TrackDownloadVisualState.Downloaded => "✓",
        TrackDownloadVisualState.Failed => "✕",
        TrackDownloadVisualState.Downloading => "↓",
        _ => string.Empty
    };

    public string DownloadStatusText => _downloadState switch
    {
        TrackDownloadVisualState.Downloaded => "Đã tải xong",
        TrackDownloadVisualState.Failed => "Tải thất bại",
        TrackDownloadVisualState.Downloading => "Đang tải audio",
        _ => string.Empty
    };

    public string DownloadDetailText
    {
        get => _downloadDetailText;
        private set
        {
            if (string.Equals(_downloadDetailText, value, StringComparison.Ordinal))
                return;

            _downloadDetailText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDownloadDetail));
        }
    }

    public Color DownloadStatusBackgroundColor => _downloadState switch
    {
        TrackDownloadVisualState.Downloaded => Color.FromArgb("#E8F7EE"),
        TrackDownloadVisualState.Failed => Color.FromArgb("#FEECEC"),
        TrackDownloadVisualState.Downloading => Color.FromArgb("#EEF5FF"),
        _ => Colors.Transparent
    };

    public Color DownloadStatusTextColor => _downloadState switch
    {
        TrackDownloadVisualState.Downloaded => Color.FromArgb("#148F40"),
        TrackDownloadVisualState.Failed => Color.FromArgb("#C62828"),
        TrackDownloadVisualState.Downloading => Color.FromArgb("#1D4ED8"),
        _ => Color.FromArgb("#344054")
    };

    public Color DownloadButtonBackgroundColor => IsDownloadedTrack
        ? Color.FromArgb("#E8F7EE")
        : Color.FromArgb("#FFFFFF");

    public Color DownloadButtonStrokeColor => IsDownloadedTrack
        ? Color.FromArgb("#18A94B")
        : Color.FromArgb("#D0D5DD");

    public string PlayIcon => IsPlaying ? "❚❚" : "▶";

    public void BeginDownload()
    {
        SetDownloadState(TrackDownloadVisualState.Downloading);
        DownloadProgress = 0;
        DownloadDetailText = "0% • 0 MB / ? MB";
    }

    public void UpdateDownloadProgress(AudioDownloadProgressUpdate update)
    {
        SetDownloadState(TrackDownloadVisualState.Downloading);
        DownloadProgress = update.ProgressRatio > 0 ? update.ProgressRatio : DownloadProgress;

        var percent = update.TotalBytes.HasValue && update.TotalBytes.Value > 0
            ? $"{Math.Round(update.ProgressRatio * 100d):0}%"
            : "?%";
        DownloadDetailText = $"{percent} • {FormatBytes(update.DownloadedBytes)} / {FormatBytes(update.TotalBytes)}";
    }

    public void ApplyDownloadSnapshot(AudioDownloadSnapshot snapshot)
    {
        if (!snapshot.IsDownloaded)
        {
            SetDownloadState(TrackDownloadVisualState.None);
            DownloadProgress = 0;
            DownloadDetailText = string.Empty;
            return;
        }

        SetDownloadState(TrackDownloadVisualState.Downloaded);
        DownloadProgress = 1;
        DownloadDetailText = $"100% • {FormatBytes(snapshot.DownloadedBytes)} / {FormatBytes(snapshot.TotalBytes)}";
    }

    public void ApplyDownloadFailure(string reason)
    {
        SetDownloadState(TrackDownloadVisualState.Failed);
        DownloadProgress = 0;
        DownloadDetailText = string.IsNullOrWhiteSpace(reason)
            ? "Tải audio thất bại do lỗi không xác định."
            : reason;
    }

    private void SetDownloadState(TrackDownloadVisualState value)
    {
        if (_downloadState == value)
            return;

        _downloadState = value;
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsDownloadStatusVisible));
        OnPropertyChanged(nameof(IsDownloadProgressVisible));
        OnPropertyChanged(nameof(IsDownloadedTrack));
        OnPropertyChanged(nameof(IsDownloadDisabled));
        OnPropertyChanged(nameof(DownloadButtonOpacity));
        OnPropertyChanged(nameof(DownloadButtonGlyph));
        OnPropertyChanged(nameof(DownloadStatusIcon));
        OnPropertyChanged(nameof(DownloadStatusText));
        OnPropertyChanged(nameof(DownloadStatusBackgroundColor));
        OnPropertyChanged(nameof(DownloadStatusTextColor));
        OnPropertyChanged(nameof(DownloadButtonBackgroundColor));
        OnPropertyChanged(nameof(DownloadButtonStrokeColor));
    }

    private static string FormatBytes(long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return "? MB";
        }

        return $"{value.Value / 1024d / 1024d:0.0} MB";
    }

    private enum TrackDownloadVisualState
    {
        None,
        Downloading,
        Downloaded,
        Failed
    }
}
