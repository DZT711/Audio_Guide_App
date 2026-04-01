using System.Collections.ObjectModel;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

public partial class MainPage : ContentPage
{
    private const double PlaceDetailOpenTopInset = 16;
    private const double PlaceDetailFallbackClosedOffset = 520;
    private const double PlaceDetailHalfVisibleRatio = 0.58;

    private readonly List<PlaceItem> _allPlaces = new();
    private ObservableCollection<PlaceAudioTrack> _selectedPlaceTracks = new();
    private string _selectedCategory = "Tất cả";
    private bool _isPlaceDetailVisible;
    private PlaceItem? _selectedPlace;
    private double _detailSheetStartY;
    private double _placeDetailExpandedY = PlaceDetailOpenTopInset;
    private double _placeDetailHalfY = 180;
    private double _placeDetailClosedY = PlaceDetailFallbackClosedOffset;
    private bool _hasLoadedPlaces;
    private bool _hasAnimatedPage;
    private CancellationTokenSource? _placesLoadingCts;

    public ObservableCollection<PlaceItem> Places { get; set; } = new();

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
        }
    }

    public ObservableCollection<PlaceAudioTrack> SelectedPlaceTracks
    {
        get => _selectedPlaceTracks;
        set
        {
            _selectedPlaceTracks = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AudioTracksTitle));
        }
    }

    public string DetailPriorityText => SelectedPlace == null ? string.Empty : $"Độ ưu tiên {SelectedPlace.Rating}";
    public string AudioTracksTitle => $"🔊 Danh sách Audio ({SelectedPlaceTracks.Count})";

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
        };

        ThemeService.Instance.PropertyChanged += (_, _) =>
        {
            UpdateFilterSelectionUI();
            UpdateFilterHeader();
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
            _ = LoadPlacesAsync();
        }
        else if (!_hasAnimatedPage)
        {
            _hasAnimatedPage = true;
            _ = UiEffectsService.AnimateEntranceAsync(CountLabel, PlacesCollectionView);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _placesLoadingCts?.Cancel();
    }

    private async Task LoadPlacesAsync()
    {
        if (_hasLoadedPlaces)
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

        await Task.Delay(420);

        _allPlaces.Clear();
        _allPlaces.AddRange(BuildSamplePlacesNearCurrentLocation());
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
        if (sender is not Frame card || card.BindingContext is not PlaceItem item)
            return;

        SelectedPlace = item;
        SelectedPlaceTracks = BuildPlaceTracks(item);
        IsPlaceDetailVisible = true;

        await Task.Yield();
        UpdatePlaceDetailSheetLayout();
        PlaceDetailSheet.TranslationY = _placeDetailClosedY;
        await PlaceDetailSheet.TranslateToAsync(0, _placeDetailHalfY, 300, Easing.CubicOut);
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

        UpdatePlaceDetailSheetLayout();
        await PlaceDetailSheet.TranslateToAsync(0, _placeDetailClosedY, 230, Easing.CubicIn);
        IsPlaceDetailVisible = false;
        SelectedPlace = null;
        SelectedPlaceTracks = new ObservableCollection<PlaceAudioTrack>();
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

    private static IEnumerable<PlaceItem> BuildSamplePlacesNearCurrentLocation()
    {
        return new List<PlaceItem>
        {
            new()
            {
                Name = "Cơm Tấm Góc Sài Gòn",
                Description = "Quán cơm tấm đông khách, vị đậm đà gần trung tâm",
                AudioDescription = "Cơm Tấm Góc Sài Gòn nổi bật với sườn nướng thơm, bì chả đầy đặn và nước mắm pha vừa vị.",
                Category = "Món ăn đặc trưng",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                Address = "58 Võ Văn Tần, Quận 3, TP.HCM",
                Phone = "(028) 3820 1122",
                Email = "comtamgocsaigon@example.vn",
                Website = "comtamgocsaigon.vn",
                EstablishedYear = "2016",
                RadiusText = "75m",
                GpsText = "10.779120, 106.683900",
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new()
            {
                Name = "Phở Bò Nguyễn Đình Chiểu",
                Description = "Tô phở nóng với nước dùng thanh và bò mềm",
                AudioDescription = "Phở Bò Nguyễn Đình Chiểu phục vụ phở truyền thống với nước dùng trong, thơm mùi quế hồi.",
                Category = "Món ăn đặc trưng",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                Address = "124 Nguyễn Đình Chiểu, Quận 3, TP.HCM",
                Phone = "(028) 3930 2233",
                Email = "phobondc@example.vn",
                Website = "phobondc.vn",
                EstablishedYear = "2018",
                RadiusText = "90m",
                GpsText = "10.777950, 106.685150",
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new()
            {
                Name = "Bún Bò Huế Chị Mai",
                Description = "Bún bò cay nhẹ, topping đầy đủ và nước dùng đậm",
                AudioDescription = "Bún Bò Huế Chị Mai nổi tiếng với nước lèo đậm vị, chả cua thơm và thịt bò mềm.",
                Category = "Món ăn đặc trưng",
                Rating = "8/10",
                Image = "dotnet_bot.png",
                Address = "36 Trần Quốc Thảo, Quận 3, TP.HCM",
                Phone = "(028) 3932 4455",
                Email = "bunbochimai@example.vn",
                Website = "bunbochimai.vn",
                EstablishedYear = "2019",
                RadiusText = "110m",
                GpsText = "10.780020, 106.686050",
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new()
            {
                Name = "Quán Mộc Garden Sài Gòn",
                Description = "Không gian sân vườn mát, phù hợp ăn uống nhóm nhỏ",
                AudioDescription = "Quán Mộc Garden Sài Gòn có không gian xanh và thực đơn Việt hiện đại, phù hợp gặp gỡ bạn bè.",
                Category = "Quán nổi tiếng",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                Address = "22 Pasteur, Quận 3, TP.HCM",
                Phone = "(028) 3829 6677",
                Email = "mocgarden@example.vn",
                Website = "mocgardensaigon.vn",
                EstablishedYear = "2017",
                RadiusText = "120m",
                GpsText = "10.776980, 106.683480",
                CategoryColor = Color.FromArgb("#FFF7D6"),
                CategoryTextColor = Color.FromArgb("#CA8A04")
            },
            new()
            {
                Name = "Cafe Sông Xanh",
                Description = "Quán cà phê yên tĩnh, thích hợp nghỉ chân buổi chiều",
                AudioDescription = "Cafe Sông Xanh phục vụ cà phê rang mộc và nhiều loại đồ uống nhẹ trong không gian thư giãn.",
                Category = "Đồ uống",
                Rating = "8/10",
                Image = "dotnet_bot.png",
                Address = "75 Nam Kỳ Khởi Nghĩa, Quận 3, TP.HCM",
                Phone = "(028) 3911 7788",
                Email = "cafesongxanh@example.vn",
                Website = "cafesongxanh.vn",
                EstablishedYear = "2020",
                RadiusText = "95m",
                GpsText = "10.779640, 106.682950",
                CategoryColor = Color.FromArgb("#E6F4FF"),
                CategoryTextColor = Color.FromArgb("#2563EB")
            }
        };
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

    private static ObservableCollection<PlaceAudioTrack> BuildPlaceTracks(PlaceItem item)
    {
        return new ObservableCollection<PlaceAudioTrack>
        {
            new() { LanguageCode = "VI", Title = $"Lịch sử {item.Name}", Description = "Tự động", Duration = "4:27" },
            new() { LanguageCode = "EN", Title = $"History of {item.Name}", Description = "Recorded", Duration = "4:13" },
            new() { LanguageCode = "JA", Title = $"{item.Name} の紹介", Description = "Thủ công", Duration = "3:09" },
            new() { LanguageCode = "ZH", Title = $"{item.Name} 简介", Description = "TTS", Duration = "4:01" }
        };
    }

    private void OnPlayTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not PlaceItem item)
            return;

        item.IsPlayed = !item.IsPlayed;
        if (item.IsPlayed)
        {
            HistoryService.Instance.AddToHistory(item);
        }
    }
}

public class PlaceAudioTrack
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}
