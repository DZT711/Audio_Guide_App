using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class OfflinePage : ContentPage
{
    private const double OfflineDetailOpenTopInset = 16;
    private const double OfflineDetailFallbackClosedOffset = 520;
    private const double OfflineDetailHalfVisibleRatio = 0.58;
    private const string AllCategoryValue = "__all__";
    private readonly ObservableCollection<OfflinePackItem> _allItems = new();
    private ObservableCollection<OfflinePackItem> _filteredItems = new();
    private string _selectedFilter = "All";
    private string _selectedCategoryValue = AllCategoryValue;
    private string _searchKeyword = string.Empty;
    private bool _isDeleteConfirmVisible;
    private bool _isOfflineDetailVisible;
    private bool _isOfflineAudioListExpanded;
    private OfflinePackItem? _pendingDeleteItem;
    private OfflinePackItem? _selectedPack;
    private bool _isBulkDeleteConfirm;
    private int _pendingBulkDeleteCount;
    private bool _hasInitializedView;
    private bool _hasAnimatedView;
    private bool _isRefreshingOfflinePacks;
    private double _offlineDetailSheetStartY;
    private double _offlineDetailExpandedY = OfflineDetailOpenTopInset;
    private double _offlineDetailHalfY = 180;
    private double _offlineDetailClosedY = OfflineDetailFallbackClosedOffset;
    private CancellationTokenSource? _loadingCts;

    public ObservableCollection<OfflinePackItem> FilteredItems
    {
        get => _filteredItems;
        set
        {
            _filteredItems = value;
            OnPropertyChanged();
        }
    }

    public ICommand DownloadCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RedownloadCommand { get; }
    public ICommand PlayTrackCommand { get; }
    public ICommand ToggleInlinePackCommand { get; }

    public string DownloadedCountText => string.Format(
        LocalizationService.Instance.T("Offline.ReadyCountFormat"),
        _allItems.Count(x => x.IsDownloaded),
        _allItems.Count);
    public string DownloadedSizeText => $"{_allItems.Where(x => x.IsDownloaded).Sum(x => x.SizeValue):0.#} MB";
    public double DownloadProgress => _allItems.Count == 0 ? 0 : (double)_allItems.Count(x => x.IsDownloaded) / _allItems.Count;
    public string DownloadProgressText => string.Format(
        LocalizationService.Instance.T("Offline.ProgressTextFormat"),
        _allItems.Count(x => x.IsDownloaded),
        _allItems.Count);
    public bool HasItems => FilteredItems.Count > 0;
    public bool ShowEmptyState => _hasInitializedView && FilteredItems.Count == 0;

    public Color AllTabBg => _selectedFilter == "All"
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("SurfaceAlt", "#F2F4F7");

    public Color DownloadedTabBg => _selectedFilter == "Downloaded"
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("SurfaceAlt", "#F2F4F7");

    public Color NotDownloadedTabBg => _selectedFilter == "NotDownloaded"
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("SurfaceAlt", "#F2F4F7");

    public Color AllTabTextColor => _selectedFilter == "All"
        ? ThemeService.Instance.GetColor("OnAccentText", "#FFFFFF")
        : ThemeService.Instance.GetColor("MutedText", "#475467");

    public Color DownloadedTabTextColor => _selectedFilter == "Downloaded"
        ? ThemeService.Instance.GetColor("OnAccentText", "#FFFFFF")
        : ThemeService.Instance.GetColor("MutedText", "#475467");

    public Color NotDownloadedTabTextColor => _selectedFilter == "NotDownloaded"
        ? ThemeService.Instance.GetColor("OnAccentText", "#FFFFFF")
        : ThemeService.Instance.GetColor("MutedText", "#475467");

    public bool IsDeleteConfirmVisible
    {
        get => _isDeleteConfirmVisible;
        set
        {
            _isDeleteConfirmVisible = value;
            OnPropertyChanged();
        }
    }

    public string DeleteConfirmTitle => _isBulkDeleteConfirm
        ? "Xóa tất cả audio đã tải?"
        : "Xóa audio pack này?";

    public string DeleteConfirmDescription => _isBulkDeleteConfirm
        ? $"{_pendingBulkDeleteCount} pack sẽ bị xóa khỏi thiết bị"
        : "Audio sẽ bị xóa khỏi thiết bị, bạn có thể tải lại sau";

    public OfflinePage()
    {
        InitializeComponent();
        BindingContext = this;

        DownloadCommand = new Command<OfflinePackItem>(OnDownload);
        DeleteCommand = new Command<OfflinePackItem>(OnDelete);
        RedownloadCommand = new Command<OfflinePackItem>(OnRedownload);
        PlayTrackCommand = new Command<OfflineAudioTrack>(OnPlayTrack);
        ToggleInlinePackCommand = new Command<OfflinePackItem>(OnToggleInlinePack);

        ApplyLocalizedText();
        SyncCategoryFilters([]);
        ApplyFilter();
        ApplyFilterSummary();

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            ApplyLocalizedText();
            ApplyFilter();
            OnPropertyChanged(nameof(DownloadedCountText));
            OnPropertyChanged(nameof(DownloadProgressText));
        };

        ThemeService.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AllTabBg));
            OnPropertyChanged(nameof(DownloadedTabBg));
            OnPropertyChanged(nameof(NotDownloadedTabBg));
            OnPropertyChanged(nameof(AllTabTextColor));
            OnPropertyChanged(nameof(DownloadedTabTextColor));
            OnPropertyChanged(nameof(NotDownloadedTabTextColor));
            foreach (var item in _allItems)
            {
                item.RefreshThemeState();
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasInitializedView)
        {
            _ = RunInitialLoadAsync();
            return;
        }

        if (!_hasAnimatedView)
        {
            _hasAnimatedView = true;
            _ = UiEffectsService.AnimateEntranceAsync(OfflineSummaryCard1, OfflineSummaryCard2, OfflineProgressCard, OfflineCollectionView);
        }

        _ = RefreshOfflinePacksSilentlyAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateOfflineDetailSheetLayout();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _loadingCts?.Cancel();
    }

    private async Task RunInitialLoadAsync()
    {
        _hasInitializedView = true;
        OnPropertyChanged(nameof(ShowEmptyState));

        _loadingCts?.Cancel();
        _loadingCts = new CancellationTokenSource();

        OfflineLoadingOverlay.IsVisible = true;
        OfflineLoadingOverlay.Opacity = 1;
        OfflineCollectionView.Opacity = 0;
        OfflineCollectionView.IsVisible = HasItems;

        _ = UiEffectsService.RunSkeletonPulseAsync(
            _loadingCts.Token,
            OfflineSkeletonCard1,
            OfflineSkeletonCard2,
            OfflineSkeletonCard3);

        try
        {
            await LoadOfflinePacksAsync(forceRefresh: false, _loadingCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
        }

        await Task.Delay(380);

        _loadingCts.Cancel();

        OfflineCollectionView.IsVisible = HasItems;

        await Task.WhenAll(
            OfflineLoadingOverlay.FadeToAsync(0, 180, Easing.CubicOut),
            OfflineCollectionView.FadeToAsync(1, 220, Easing.CubicOut));

        OfflineLoadingOverlay.IsVisible = false;
        OfflineLoadingOverlay.Opacity = 1;

        if (!_hasAnimatedView)
        {
            _hasAnimatedView = true;
            await UiEffectsService.AnimateEntranceAsync(OfflineSummaryCard1, OfflineSummaryCard2, OfflineProgressCard, OfflineCollectionView);
        }
    }

    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = new();

    public bool IsOfflineDetailVisible
    {
        get => _isOfflineDetailVisible;
        set
        {
            _isOfflineDetailVisible = value;
            OnPropertyChanged();
        }
    }

    public OfflinePackItem? SelectedPack
    {
        get => _selectedPack;
        set
        {
            _selectedPack = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPackLanguagesText));
        }
    }

    public string SelectedPackLanguagesText => SelectedPack == null
        ? string.Empty
        : string.Join(" • ", SelectedPack.AudioTracks.Select(track => track.LanguageCode));

    public bool IsOfflineAudioListExpanded
    {
        get => _isOfflineAudioListExpanded;
        set
        {
            if (_isOfflineAudioListExpanded == value)
                return;

            _isOfflineAudioListExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OfflineAudioListExpandIcon));
        }
    }

    public string OfflineAudioListExpandIcon => IsOfflineAudioListExpanded ? "triangle_up_filled.svg" : "triangle_down_filled.svg";

    private async Task RefreshOfflinePacksSilentlyAsync()
    {
        if (_isRefreshingOfflinePacks)
        {
            return;
        }

        try
        {
            _isRefreshingOfflinePacks = true;
            await LoadOfflinePacksAsync(forceRefresh: true);
        }
        catch
        {
        }
        finally
        {
            _isRefreshingOfflinePacks = false;
        }
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

    private async Task LoadOfflinePacksAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        var existingStates = _allItems.ToDictionary(
            item => item.Id,
            item => new OfflinePackState(item.IsDownloaded, item.DownloadedAt),
            StringComparer.OrdinalIgnoreCase);

        var catalogSnapshot = await PlaceCatalogService.Instance.GetCatalogAsync(forceRefresh, cancellationToken);
        var places = catalogSnapshot.Places;
        var mappedItems = places
            .Select(place => CreateOfflinePackItem(place, existingStates))
            .ToList();

        _allItems.Clear();
        foreach (var item in mappedItems)
        {
            _allItems.Add(item);
        }

        SyncCategoryFilters(catalogSnapshot.Categories);
        ApplyLocalizedText();
        ApplyFilter();
    }

    private static OfflinePackItem CreateOfflinePackItem(
        PlaceItem place,
        IReadOnlyDictionary<string, OfflinePackState> existingStates)
    {
        var audioCount = ExtractAudioCount(place.AudioCountText);
        var estimatedDurationSeconds = Math.Max(audioCount, 1) * 125;
        var sizeValue = Math.Round(Math.Max(audioCount, 1) * 2.4, 1, MidpointRounding.AwayFromZero);
        var state = existingStates.TryGetValue(place.Id, out var existingState)
            ? existingState
            : default;

        return new OfflinePackItem
        {
            Id = place.Id,
            Category = place.Category,
            DefaultTitle = place.Name,
            Title = place.Name,
            AudioCount = audioCount,
            Duration = FormatDuration(estimatedDurationSeconds),
            Size = $"{sizeValue:0.#} MB",
            SizeValue = sizeValue,
            Image = string.IsNullOrWhiteSpace(place.PreferenceImage) ? place.Image : place.PreferenceImage,
            Description = place.Description,
            Address = place.Address,
            Phone = place.Phone,
            Email = place.Email,
            Website = place.Website,
            EstablishedYear = place.EstablishedYear,
            RadiusText = place.RadiusText,
            GpsText = place.GpsText,
            RatingText = place.PriorityText,
            CategoryBg = place.CategoryColor,
            CategoryTextColor = place.CategoryTextColor,
            IsDownloaded = state.IsDownloaded,
            DownloadedAt = state.DownloadedAt,
            AudioTracks = BuildAudioTracksForPlace(place, audioCount),
            LocalizedTitles = BuildMirroredLocalizedTitles(place.Name)
        };
    }

    private void ApplyLocalizedText()
    {
        if (HeaderTitleLabel is not null)
        {
            HeaderTitleLabel.Text = LocalizationService.Instance.T("Offline.Title");
        }

        if (HeaderSubtitleLabel is not null)
        {
            HeaderSubtitleLabel.Text = LocalizationService.Instance.T("Offline.Subtitle");
        }

        if (DownloadAllButton is not null)
        {
            DownloadAllButton.Text = LocalizationService.Instance.T("Offline.DownloadAll");
        }

        if (SearchEntry is not null)
        {
            SearchEntry.Placeholder = LocalizationService.Instance.T("Offline.Search");
        }

        if (AllTabLabel is not null)
        {
            AllTabLabel.Text = LocalizationService.Instance.T("Offline.FilterAll");
        }

        if (DownloadedTabLabel is not null)
        {
            DownloadedTabLabel.Text = LocalizationService.Instance.T("Offline.FilterDownloaded");
        }

        if (NotDownloadedTabLabel is not null)
        {
            NotDownloadedTabLabel.Text = LocalizationService.Instance.T("Offline.FilterPending");
        }

        if (FilterPopupTitleLabel is not null)
        {
            FilterPopupTitleLabel.Text = LocalizationService.Instance.T("Filter.Title");
        }

        SyncCategoryFilters(PlaceCatalogService.Instance.GetCategories());

        foreach (var item in _allItems)
        {
            item.ApplyLanguage(LocalizationService.Instance.Language);
        }

        OnPropertyChanged(nameof(DownloadedCountText));
        OnPropertyChanged(nameof(DownloadProgressText));
    }

    private static ObservableCollection<LocalizedTitleItem> BuildMirroredLocalizedTitles(string title)
    {
        return new ObservableCollection<LocalizedTitleItem>
        {
            new() { LanguageCode = "VI", LanguageName = "Tiếng Việt", LocalizedName = title },
            new() { LanguageCode = "EN", LanguageName = "English", LocalizedName = title },
            new() { LanguageCode = "FR", LanguageName = "Francais", LocalizedName = title },
            new() { LanguageCode = "KO", LanguageName = "Korean", LocalizedName = title },
            new() { LanguageCode = "JA", LanguageName = "Japanese", LocalizedName = title },
            new() { LanguageCode = "ZH", LanguageName = "Chinese", LocalizedName = title }
        };
    }

    private static ObservableCollection<OfflineAudioTrack> BuildAudioTracksForPlace(PlaceItem place, int audioCount)
    {
        if (place.AudioTracks.Count > 0)
        {
            return new ObservableCollection<OfflineAudioTrack>(
                place.AudioTracks
                    .Take(Math.Max(audioCount, 1))
                    .Select(track => new OfflineAudioTrack
                    {
                        LanguageCode = ResolveLanguageBadge(track.Language),
                        LanguageName = track.LanguageName ?? track.Language,
                        LocaleCode = string.IsNullOrWhiteSpace(track.Language) ? "vi-VN" : track.Language,
                        Title = string.IsNullOrWhiteSpace(track.Title) ? place.Name : track.Title,
                        Duration = FormatDuration(track.Duration > 0 ? track.Duration : 125),
                        SourceType = string.IsNullOrWhiteSpace(track.SourceType) ? "TTS" : track.SourceType.Trim().ToUpperInvariant()
                    }));
        }

        var trackTemplates = new (string LanguageCode, string LanguageName, string LocaleCode, string Title, string SourceType)[]
        {
            ("VN", "Tiếng Việt", "vi-VN", "Giới thiệu tổng quan", "TTS"),
            ("GB", "English", "en-US", "Overview Introduction", "TTS"),
            ("JP", "Japanese", "ja-JP", "日本語での紹介", "TTS"),
            ("CN", "Chinese", "zh-CN", "总体介绍", "TTS"),
            ("KR", "Korean", "ko-KR", "한국어 소개", "Recorded"),
            ("FR", "Francais", "fr-FR", "Decouverte culturelle", "TTS")
        };

        var trackCount = Math.Clamp(audioCount, 1, trackTemplates.Length);
        return new ObservableCollection<OfflineAudioTrack>(
            trackTemplates
                .Take(trackCount)
                .Select(item => new OfflineAudioTrack
            {
                LanguageCode = item.LanguageCode,
                LanguageName = item.LanguageName,
                LocaleCode = item.LocaleCode,
                Title = item.Title,
                Duration = FormatDuration(125),
                SourceType = item.SourceType
            }));
    }

    private static string ResolveLanguageBadge(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "VN";
        }

        var normalized = locale.Trim().ToLowerInvariant();
        if (normalized.StartsWith("en"))
            return "GB";

        if (normalized.StartsWith("ja"))
            return "JP";

        if (normalized.StartsWith("zh"))
            return "CN";

        if (normalized.StartsWith("ko"))
            return "KR";

        if (normalized.StartsWith("fr"))
            return "FR";

        return "VN";
    }

    private static int ExtractAudioCount(string? audioCountText)
    {
        if (string.IsNullOrWhiteSpace(audioCountText))
        {
            return 1;
        }

        var digits = new string(audioCountText.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) && value > 0
            ? value
            : 1;
    }

    private static string FormatDuration(int totalSeconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(1, totalSeconds));
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private void ApplyFilter()
    {
        var query = _allItems.AsEnumerable();

        if (_selectedFilter == "Downloaded")
            query = query.Where(x => x.IsDownloaded);
        else if (_selectedFilter == "NotDownloaded")
            query = query.Where(x => !x.IsDownloaded);

        if (_selectedCategoryValue != AllCategoryValue)
        {
            query = query.Where(x => string.Equals(x.Category, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            var normalizedKeyword = NormalizeSearchText(_searchKeyword);
            query = query.Where(x =>
                ContainsSearchText(x.Title, normalizedKeyword) ||
                ContainsSearchText(x.DefaultTitle, normalizedKeyword) ||
                x.LocalizedTitles.Any(title => ContainsSearchText(title.LocalizedName, normalizedKeyword)));
        }

        FilteredItems = new ObservableCollection<OfflinePackItem>(query);

        OnPropertyChanged(nameof(DownloadedCountText));
        OnPropertyChanged(nameof(DownloadedSizeText));
        OnPropertyChanged(nameof(DownloadProgress));
        OnPropertyChanged(nameof(DownloadProgressText));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(AllTabBg));
        OnPropertyChanged(nameof(DownloadedTabBg));
        OnPropertyChanged(nameof(NotDownloadedTabBg));
        OnPropertyChanged(nameof(AllTabTextColor));
        OnPropertyChanged(nameof(DownloadedTabTextColor));
        OnPropertyChanged(nameof(NotDownloadedTabTextColor));
        ApplyFilterSummary();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchKeyword = e.NewTextValue?.Trim() ?? string.Empty;
        ApplyFilter();
    }

    private void ApplyFilterSummary()
    {
        if (CountLabel is not null)
        {
            CountLabel.Text = $"{FilteredItems.Count} {LocalizationService.Instance.T("Offline.CountSuffix")}";
        }

        if (CountHintLabel is not null)
        {
            CountHintLabel.Text = LocalizationService.Instance.T("Offline.CountHint");
        }

        if (FilterLabel is null)
        {
            return;
        }

        var statusText = _selectedFilter switch
        {
            "Downloaded" => LocalizationService.Instance.T("Offline.FilterDownloaded"),
            "NotDownloaded" => LocalizationService.Instance.T("Offline.FilterPending"),
            _ => LocalizationService.Instance.T("Offline.FilterAll")
        };

        var summaryParts = new List<string>();
        if (statusText != LocalizationService.Instance.T("Offline.FilterAll"))
        {
            summaryParts.Add(statusText);
        }

        var selectedCategory = CategoryFilters.FirstOrDefault(item => item.IsSelected && !item.IsAllOption);
        if (selectedCategory is not null)
        {
            summaryParts.Add(selectedCategory.DisplayName);
        }

        FilterLabel.Text = summaryParts.Count == 0
            ? LocalizationService.Instance.T("Offline.FilterLabel")
            : $"{LocalizationService.Instance.T("Offline.FilterLabel")}: {string.Join(" • ", summaryParts)}";
    }

    private static bool ContainsSearchText(string? source, string keyword)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        return NormalizeSearchText(source).Contains(keyword, StringComparison.Ordinal);
    }

    private static string NormalizeSearchText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character switch
            {
                'đ' => 'd',
                _ => character
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void OnDownload(OfflinePackItem? item)
    {
        if (item == null) return;

        item.IsDownloaded = true;
        item.DownloadedAt = DateTime.Now;
        item.IsExpanded = true;
        ApplyFilter();
    }

    private void OnDelete(OfflinePackItem? item)
    {
        if (item == null) return;

        _isBulkDeleteConfirm = false;
        _pendingBulkDeleteCount = 1;
        _pendingDeleteItem = item;
        OnPropertyChanged(nameof(DeleteConfirmTitle));
        OnPropertyChanged(nameof(DeleteConfirmDescription));
        IsDeleteConfirmVisible = true;
    }

    private void OnRedownload(OfflinePackItem? item)
    {
        if (item == null) return;

        item.IsDownloaded = true;
        item.DownloadedAt = DateTime.Now;
        item.IsExpanded = true;
        ApplyFilter();
    }

    private void OnAllTapped(object? sender, TappedEventArgs e)
    {
        _selectedFilter = "All";
        ApplyFilter();
    }

    private void OnDownloadedTapped(object? sender, TappedEventArgs e)
    {
        _selectedFilter = "Downloaded";
        ApplyFilter();
    }

    private void OnNotDownloadedTapped(object? sender, TappedEventArgs e)
    {
        _selectedFilter = "NotDownloaded";
        ApplyFilter();
    }

    private async void OnToggleFilterPopup(object? sender, TappedEventArgs e)
    {
        await UiEffectsService.TogglePopupAsync(FilterPopup, !FilterPopup.IsVisible);
    }

    private void OnCategoryFilterTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not CategoryFilterOption option)
            return;

        _selectedCategoryValue = _selectedCategoryValue == option.Value
            ? AllCategoryValue
            : option.Value;

        UpdateCategorySelectionState();
        _ = UiEffectsService.TogglePopupAsync(FilterPopup, false);
        ApplyFilter();
    }

    private void OnClearAllTapped(object? sender, TappedEventArgs e)
    {
        var downloadedItems = _allItems.Where(x => x.IsDownloaded).ToList();
        if (downloadedItems.Count == 0)
            return;

        _pendingDeleteItem = null;
        _isBulkDeleteConfirm = true;
        _pendingBulkDeleteCount = downloadedItems.Count;
        OnPropertyChanged(nameof(DeleteConfirmTitle));
        OnPropertyChanged(nameof(DeleteConfirmDescription));
        IsDeleteConfirmVisible = true;
    }

    private void OnDownloadAllClicked(object sender, EventArgs e)
    {
        foreach (var item in _allItems)
        {
            item.IsDownloaded = true;
            item.DownloadedAt ??= DateTime.Now;
        }

        ApplyFilter();
    }

    private void OnToggleInlinePack(OfflinePackItem? item)
    {
        if (item is null)
        {
            return;
        }

        var nextExpandedState = !item.IsExpanded;
        foreach (var pack in _allItems)
        {
            if (!ReferenceEquals(pack, item) && pack.IsExpanded)
            {
                pack.IsExpanded = false;
            }
        }

        item.IsExpanded = nextExpandedState;
    }

    private void OnPlayTrack(OfflineAudioTrack? track)
    {
        if (track == null)
            return;

        var nextState = !track.IsPlayed;
        foreach (var item in SelectedPack?.AudioTracks ?? [])
        {
            item.IsPlayed = ReferenceEquals(item, track) && nextState;
        }
    }

    private async void OnPackTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is OfflinePackItem item)
        {
            if (FilterPopup.IsVisible)
            {
                await UiEffectsService.TogglePopupAsync(FilterPopup, false);
            }

            SelectedPack = item;
            IsOfflineAudioListExpanded = false;
            IsOfflineDetailVisible = true;
            await ShowOfflineDetailAsync();
        }
    }

    private async void OnOfflineDetailBackdropTapped(object? sender, TappedEventArgs e)
    {
        await CloseOfflineDetailAsync();
    }

    private async void OnCloseOfflineDetailClicked(object? sender, EventArgs e)
    {
        await CloseOfflineDetailAsync();
    }

    private async void OnOfflineDetailPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsOfflineDetailVisible)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _offlineDetailSheetStartY = OfflineDetailSheet.TranslationY;
                break;

            case GestureStatus.Running:
                var nextY = Math.Clamp(_offlineDetailSheetStartY + e.TotalY, _offlineDetailExpandedY, _offlineDetailClosedY);
                OfflineDetailSheet.TranslationY = nextY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var targetY = ResolveOfflineDetailSnapTarget(OfflineDetailSheet.TranslationY, e.TotalY);
                if (targetY >= _offlineDetailClosedY - 1)
                {
                    await CloseOfflineDetailAsync();
                }
                else
                {
                    await OfflineDetailSheet.TranslateToAsync(0, targetY, 170, Easing.CubicOut);
                }
                break;
        }
    }

    private async Task ShowOfflineDetailAsync()
    {
        await Task.Yield();
        UpdateOfflineDetailSheetLayout();
        OfflineDetailSheet.TranslationY = _offlineDetailClosedY;
        await OfflineDetailSheet.TranslateToAsync(0, _offlineDetailHalfY, 300, Easing.CubicOut);
    }

    private async Task CloseOfflineDetailAsync()
    {
        if (OfflineDetailSheet is not null)
        {
            UpdateOfflineDetailSheetLayout();
            await OfflineDetailSheet.TranslateToAsync(0, _offlineDetailClosedY, 230, Easing.CubicIn);
        }

        IsOfflineDetailVisible = false;
        IsOfflineAudioListExpanded = false;
        SelectedPack = null;
    }

    private void OnToggleOfflineAudioListTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedPack is null)
            return;

        IsOfflineAudioListExpanded = !IsOfflineAudioListExpanded;
    }

    private async void OnViewOfflinePlaceOnMapTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedPack is null)
            return;

        try
        {
            PlaceNavigationService.Instance.RequestMapFocus(SelectedPack.Id);
            await CloseOfflineDetailAsync();

            if (Application.Current?.Windows.FirstOrDefault()?.Page is AppShell appShell)
            {
                await appShell.NavigateToMapTabAsync();
                return;
            }

            await Shell.Current.GoToAsync("//mainTabs/map");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigate offline pack to map error: {ex.Message}");
        }
    }

    private void UpdateOfflineDetailSheetLayout()
    {
        if (Height <= 0 || OfflineDetailSheet is null)
        {
            return;
        }

        var maxSheetHeight = Math.Max(360, Height * 0.88);
        OfflineDetailSheet.MaximumHeightRequest = maxSheetHeight;

        _offlineDetailExpandedY = OfflineDetailOpenTopInset;
        var halfVisibleHeight = Math.Max(320, Height * OfflineDetailHalfVisibleRatio);
        _offlineDetailHalfY = Math.Clamp(
            maxSheetHeight - halfVisibleHeight,
            _offlineDetailExpandedY + 72,
            _offlineDetailExpandedY + 300);
        _offlineDetailClosedY = Math.Max(OfflineDetailFallbackClosedOffset, maxSheetHeight + 48);

        if (!IsOfflineDetailVisible)
        {
            OfflineDetailSheet.TranslationY = _offlineDetailClosedY;
        }
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

    private double ResolveOfflineDetailSnapTarget(double currentY, double totalDragY)
    {
        var expandedHalfMid = (_offlineDetailExpandedY + _offlineDetailHalfY) / 2;
        var halfClosedMid = (_offlineDetailHalfY + _offlineDetailClosedY) / 2;

        if (totalDragY < -80)
        {
            return _offlineDetailExpandedY;
        }

        if (totalDragY > 160 && currentY > _offlineDetailHalfY + 24)
        {
            return _offlineDetailClosedY;
        }

        if (currentY <= expandedHalfMid)
        {
            return _offlineDetailExpandedY;
        }

        if (currentY <= halfClosedMid)
        {
            return _offlineDetailHalfY;
        }

        return _offlineDetailClosedY;
    }

    private void OnCancelDeleteClicked(object sender, EventArgs e)
    {
        _pendingDeleteItem = null;
        _isBulkDeleteConfirm = false;
        _pendingBulkDeleteCount = 0;
        IsDeleteConfirmVisible = false;
    }

    private void OnConfirmDeleteClicked(object sender, EventArgs e)
    {
        if (_isBulkDeleteConfirm)
        {
            foreach (var item in _allItems)
            {
                item.IsDownloaded = false;
                item.DownloadedAt = null;
                item.IsExpanded = false;
            }

            _pendingDeleteItem = null;
            _isBulkDeleteConfirm = false;
            _pendingBulkDeleteCount = 0;
            IsDeleteConfirmVisible = false;
            ApplyFilter();
            return;
        }

        if (_pendingDeleteItem == null)
        {
            _isBulkDeleteConfirm = false;
            _pendingBulkDeleteCount = 0;
            IsDeleteConfirmVisible = false;
            return;
        }

        _pendingDeleteItem.IsDownloaded = false;
        _pendingDeleteItem.DownloadedAt = null;
        _pendingDeleteItem.IsExpanded = false;

        _pendingDeleteItem = null;
        _isBulkDeleteConfirm = false;
        _pendingBulkDeleteCount = 0;
        IsDeleteConfirmVisible = false;
        ApplyFilter();
    }

}

public class OfflinePackItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public string DefaultTitle { get; set; } = "";
    public string Title { get; set; } = "";
    public int AudioCount { get; set; }
    public string Duration { get; set; } = "";
    public string Size { get; set; } = "";
    public double SizeValue { get; set; }
    public string Image { get; set; } = "dotnet_bot.png";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string EstablishedYear { get; set; } = "";
    public string RadiusText { get; set; } = "";
    public string GpsText { get; set; } = "";
    public string RatingText { get; set; } = "";
    public Color CategoryBg { get; set; } = Colors.LightGray;
    public Color CategoryTextColor { get; set; } = Colors.Black;
    public ObservableCollection<LocalizedTitleItem> LocalizedTitles { get; set; } = new();
    public ObservableCollection<OfflineAudioTrack> AudioTracks { get; set; } = new();

    private bool _isDownloaded;
    private DateTime? _downloadedAt;
    private bool _isExpanded;

    public string AudioCountText => $"{AudioCount} audio";
    public string LanguagesText => string.Join(" • ", AudioTracks.Select(track => track.LanguageCode));
    public string DownloadedDateText => DownloadedAt is DateTime downloadedAt
        ? $"✓ Đã tải {downloadedAt:dd/MM/yyyy}"
        : "Chưa tải về";
    public Color CardStrokeColor => IsDownloaded
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("BorderColor", "#E5E7EB");
    public Color TrackPanelBackgroundColor => IsDownloaded
        ? ThemeService.Instance.GetColor("SuccessBg", "#F3FFF6")
        : ThemeService.Instance.GetColor("SurfaceAlt", "#F8FAFC");
    public Color TrackPanelStrokeColor => IsDownloaded
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("BorderColor", "#E5E7EB");
    public Color ExpandButtonBackgroundColor => ThemeService.Instance.GetColor("SurfaceAlt", "#F3F4F6");
    public Color StatusTextColor => IsDownloaded
        ? ThemeService.Instance.GetColor("SuccessText", "#18A94B")
        : ThemeService.Instance.GetColor("MutedText", "#98A2B3");
    public string ExpandIconSource => IsExpanded ? "triangle_up_filled.svg" : "triangle_down_filled.svg";

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            _isDownloaded = value;
            foreach (var track in AudioTracks)
            {
                track.IsDownloaded = value;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotDownloaded));
            OnPropertyChanged(nameof(CardStrokeColor));
            OnPropertyChanged(nameof(DownloadedDateText));
            OnPropertyChanged(nameof(StatusTextColor));
            OnPropertyChanged(nameof(TrackPanelBackgroundColor));
            OnPropertyChanged(nameof(TrackPanelStrokeColor));
        }
    }

    public bool IsNotDownloaded => !IsDownloaded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExpandIconSource));
        }
    }

    public DateTime? DownloadedAt
    {
        get => _downloadedAt;
        set
        {
            _downloadedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DownloadedDateText));
        }
    }

    public void RefreshThemeState()
    {
        OnPropertyChanged(nameof(CardStrokeColor));
        OnPropertyChanged(nameof(CategoryBg));
        OnPropertyChanged(nameof(CategoryTextColor));
        OnPropertyChanged(nameof(TrackPanelBackgroundColor));
        OnPropertyChanged(nameof(TrackPanelStrokeColor));
        OnPropertyChanged(nameof(ExpandButtonBackgroundColor));
        OnPropertyChanged(nameof(StatusTextColor));

        foreach (var track in AudioTracks)
        {
            track.RefreshThemeState();
        }
    }

    public void ApplyLanguage(string language)
    {
        var languageCode = language switch
        {
            "en" => "EN",
            "fr" => "FR",
            "kr" => "KO",
            "jp" => "JA",
            "cn" => "ZH",
            _ => "VI"
        };

        var localizedTitle = LocalizedTitles
            .FirstOrDefault(item => string.Equals(item.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            ?.LocalizedName;

        Title = string.IsNullOrWhiteSpace(localizedTitle)
            ? DefaultTitle
            : localizedTitle;

        OnPropertyChanged(nameof(Title));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class LocalizedTitleItem
{
    public string LanguageCode { get; set; } = "";
    public string LanguageName { get; set; } = "";
    public string LocalizedName { get; set; } = "";
}

public class OfflineAudioTrack : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string LanguageCode { get; set; } = "";
    public string LanguageName { get; set; } = "";
    public string LocaleCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Duration { get; set; } = "";
    public string SourceType { get; set; } = "TTS";
    public string MetaText => $"{LocaleCode} - {SourceType}";
    public string StatusSymbol => IsDownloaded ? "✓" : "";
    public Color StatusBackgroundColor => IsDownloaded
        ? ThemeService.Instance.GetColor("SuccessBg", "#E9FFF0")
        : ThemeService.Instance.GetColor("CardBg", "#FFFFFF");
    public Color StatusStrokeColor => IsDownloaded
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("BorderColor", "#D0D5DD");
    public Color StatusTextColor => IsDownloaded
        ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
        : ThemeService.Instance.GetColor("MutedText", "#D0D5DD");

    private bool _isPlayed;
    private bool _isDownloaded;

    public bool IsPlayed
    {
        get => _isPlayed;
        set
        {
            if (_isPlayed == value)
                return;

            _isPlayed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayIcon));
        }
    }

    public string PlayIcon => IsPlayed ? "❚❚" : "▶";

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (_isDownloaded == value)
            {
                return;
            }

            _isDownloaded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusSymbol));
            OnPropertyChanged(nameof(StatusBackgroundColor));
            OnPropertyChanged(nameof(StatusStrokeColor));
            OnPropertyChanged(nameof(StatusTextColor));
        }
    }

    public void RefreshThemeState()
    {
        OnPropertyChanged(nameof(StatusBackgroundColor));
        OnPropertyChanged(nameof(StatusStrokeColor));
        OnPropertyChanged(nameof(StatusTextColor));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public readonly record struct OfflinePackState(bool IsDownloaded, DateTime? DownloadedAt);
