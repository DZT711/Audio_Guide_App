using System.Collections.ObjectModel;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

public partial class MainPage : ContentPage
{
    private readonly List<PlaceItem> _allPlaces = new();
    private string _selectedCategory = "Tất cả";

    public ObservableCollection<PlaceItem> Places { get; set; } = new();

    public MainPage()
    {
        InitializeComponent();

        _allPlaces.AddRange(new List<PlaceItem>
        {
            new PlaceItem
            {
                Name = "Bún bò Huế",
                Description = "Món ăn đặc trưng nổi tiếng với vị cay nồng đậm đà",
                Category = "Món ăn đặc trưng",
                Rating = "10/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new PlaceItem
            {
                Name = "Phở Hà Nội",
                Description = "Món phở truyền thống với nước dùng thơm ngon",
                Category = "Món ăn đặc trưng",
                Rating = "10/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new PlaceItem
            {
                Name = "Cơm tấm Sài Gòn",
                Description = "Món cơm tấm quen thuộc với sườn nướng hấp dẫn",
                Category = "Món ăn đặc trưng",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new PlaceItem
            {
                Name = "Quán Mộc",
                Description = "Quán nổi tiếng với không gian đẹp và món Việt chất lượng",
                Category = "Quán nổi tiếng",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#FFF7D6"),
                CategoryTextColor = Color.FromArgb("#CA8A04")
            },
            new PlaceItem
            {
                Name = "Nhà hàng Ngon Garden",
                Description = "Địa điểm nổi tiếng với nhiều món Việt truyền thống",
                Category = "Quán nổi tiếng",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#FFF7D6"),
                CategoryTextColor = Color.FromArgb("#CA8A04")
            },
            new PlaceItem
            {
                Name = "Trà sữa Phúc Long",
                Description = "Chuỗi đồ uống nổi tiếng với trà và cà phê",
                Category = "Đồ uống",
                Rating = "8/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#E6F4FF"),
                CategoryTextColor = Color.FromArgb("#2563EB")
            },
            new PlaceItem
            {
                Name = "Cà phê sữa đá",
                Description = "Thức uống đặc trưng của Việt Nam, đậm vị cà phê",
                Category = "Đồ uống",
                Rating = "10/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#E6F4FF"),
                CategoryTextColor = Color.FromArgb("#2563EB")
            },
            new PlaceItem
            {
                Name = "Chợ Bến Thành",
                Description = "Nơi khám phá văn hóa ẩm thực đặc sắc của Sài Gòn",
                Category = "Văn hóa ẩm thực",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#E8F7EE"),
                CategoryTextColor = Color.FromArgb("#18A94B")
            },
            new PlaceItem
            {
                Name = "Phố ẩm thực Nguyễn Thượng Hiền",
                Description = "Khu phố nổi bật với nhiều món ăn đường phố",
                Category = "Văn hóa ẩm thực",
                Rating = "9/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#E8F7EE"),
                CategoryTextColor = Color.FromArgb("#18A94B")
            },
            new PlaceItem
            {
                Name = "Cửa hàng tiện lợi 24h",
                Description = "Điểm tiện ích mua sắm nhanh chóng gần khu du lịch",
                Category = "Tiện ích",
                Rating = "8/10",
                Image = "dotnet_bot.png",
                CategoryColor = Color.FromArgb("#F2E8FF"),
                CategoryTextColor = Color.FromArgb("#7C3AED")
            }
        });

        BindingContext = this;
        ApplyTexts();
        ApplyFilter();
        UpdateFilterSelectionUI();

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            ApplyTexts();
            UpdateCount();
        };
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
        string keyword = SearchEntry.Text?.Trim().ToLower() ?? "";

        var filtered = _allPlaces
            .Where(p =>
                (_selectedCategory == "Tất cả" || p.Category == _selectedCategory) &&
                (string.IsNullOrWhiteSpace(keyword) || p.Name.ToLower().Contains(keyword)))
            .ToList();

        Places.Clear();
        foreach (var item in filtered)
            Places.Add(item);

        PlacesCollectionView.ItemsSource = null;
        PlacesCollectionView.ItemsSource = Places;

        EmptyStateLayout.IsVisible = Places.Count == 0;
        PlacesCollectionView.IsVisible = Places.Count > 0;

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
            FilterLabel.TextColor = Color.FromArgb("#243B5A");
            return;
        }

        FilterLabel.Text = $"{LocalizationService.Instance.T("Places.Filter")}: {_selectedCategory}";
        FilterLabel.TextColor = Color.FromArgb("#18A94B");
    }

    private void OnToggleFilterPopup(object sender, TappedEventArgs e)
    {
        FilterPopup.IsVisible = !FilterPopup.IsVisible;
    }

    private void ApplyCategory(string category)
    {
        if (_selectedCategory == category)
            _selectedCategory = "Tất cả";
        else
            _selectedCategory = category;

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
        label.TextColor = Color.FromArgb("#243B5A");
        label.FontAttributes = FontAttributes.None;
        indicator.IsVisible = false;
    }

    private void SelectFilterItem(Grid item, Label label, BoxView indicator)
    {
        item.BackgroundColor = Color.FromArgb("#E8F7EE");
        label.TextColor = Color.FromArgb("#18A94B");
        label.FontAttributes = FontAttributes.Bold;
        indicator.IsVisible = true;
    }

    private void OnFilterAllTapped(object sender, TappedEventArgs e) => ApplyCategory("Tất cả");
    private void OnFilterSignatureDishTapped(object sender, TappedEventArgs e) => ApplyCategory("Món ăn đặc trưng");
    private void OnFilterFamousRestaurantTapped(object sender, TappedEventArgs e) => ApplyCategory("Quán nổi tiếng");
    private void OnFilterDrinksTapped(object sender, TappedEventArgs e) => ApplyCategory("Đồ uống");
    private void OnFilterFoodCultureTapped(object sender, TappedEventArgs e) => ApplyCategory("Văn hóa ẩm thực");
    private void OnFilterUtilityTapped(object sender, TappedEventArgs e) => ApplyCategory("Tiện ích");

    public class PlaceItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Rating { get; set; } = "";
        public string Image { get; set; } = "";
        public Color CategoryColor { get; set; } = Colors.LightGray;
        public Color CategoryTextColor { get; set; } = Colors.Black;
    }
}