using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MauiApp_Mobile.Views;

public partial class OfflinePage : ContentPage, INotifyPropertyChanged
{
    private readonly ObservableCollection<OfflinePackItem> _allItems = new();
    private ObservableCollection<OfflinePackItem> _filteredItems = new();
    private string _selectedFilter = "All";
    private bool _isDeleteConfirmVisible;
    private OfflinePackItem? _pendingDeleteItem;
    private bool _isBulkDeleteConfirm;
    private int _pendingBulkDeleteCount;

    public new event PropertyChangedEventHandler? PropertyChanged;

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
    public ICommand ToggleExpandCommand { get; }

    public string DownloadedCountText => $"{_allItems.Count(x => x.IsDownloaded)}/{_allItems.Count} pack";
    public string DownloadedSizeText => $"{_allItems.Where(x => x.IsDownloaded).Sum(x => x.SizeValue):0.#} MB";

    public Color AllTabBg => _selectedFilter == "All" ? Color.FromArgb("#18A94B") : Color.FromArgb("#F2F4F7");
    public Color DownloadedTabBg => _selectedFilter == "Downloaded" ? Color.FromArgb("#18A94B") : Color.FromArgb("#F2F4F7");
    public Color NotDownloadedTabBg => _selectedFilter == "NotDownloaded" ? Color.FromArgb("#18A94B") : Color.FromArgb("#F2F4F7");

    public Color AllTabTextColor => _selectedFilter == "All" ? Colors.White : Color.FromArgb("#475467");
    public Color DownloadedTabTextColor => _selectedFilter == "Downloaded" ? Colors.White : Color.FromArgb("#475467");
    public Color NotDownloadedTabTextColor => _selectedFilter == "NotDownloaded" ? Colors.White : Color.FromArgb("#475467");

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
        ToggleExpandCommand = new Command<OfflinePackItem>(OnToggleExpand);

        SeedData();
        ApplyFilter();
    }

    private void SeedData()
    {
        _allItems.Clear();

        _allItems.Add(new OfflinePackItem
        {
            Title = "Bảo tàng Chứng tích",
            AudioCount = 4,
            Duration = "12:46",
            Size = "11.7 MB",
            SizeValue = 11.7,
            Image = "dotnet_bot.png",
            Description = "Bảo tàng Chứng tích Chiến tranh là một trong những bảo tàng hàng đầu về lịch sử Việt Nam hiện đại.",
            Address = "28 Vo Van Tan, Phuong Vo Thi Sau, Quan 3, TP. Ho Chi Minh",
            Phone = "(028) 3930 6325",
            Email = "warremnants@hcm.gov.vn",
            Website = "baotangchungtich.vn",
            EstablishedYear = "1975",
            RadiusText = "70m",
            GpsText = "10.7797, 106.6924",
            RatingText = "9/10",
            IsDownloaded = false,
            LocalizedTitles = BuildLocalizedTitles(
                vi: "Bảo tàng Chứng tích Chiến tranh",
                en: "War Remnants Museum",
                fr: "Musee des vestiges de guerre",
                ko: "전쟁증적박물관",
                ja: "戦争証跡博物館",
                zh: "战争遗迹博物馆")
        });

        _allItems.Add(new OfflinePackItem
        {
            Title = "Bưu điện Sài Gòn",
            AudioCount = 3,
            Duration = "7:15",
            Size = "6.6 MB",
            SizeValue = 6.6,
            Image = "dotnet_bot.png",
            Description = "Bưu điện Trung tâm Sài Gòn là công trình kiến trúc Pháp cổ nổi bật tại trung tâm thành phố.",
            Address = "2 Cong xa Paris, Ben Nghe, Quan 1, TP. Ho Chi Minh",
            Phone = "(028) 3822 1677",
            Email = "saigonpost@vnpost.vn",
            Website = "vnpost.vn",
            EstablishedYear = "1886",
            RadiusText = "120m",
            GpsText = "10.7800, 106.6990",
            RatingText = "10/10",
            IsDownloaded = false,
            LocalizedTitles = BuildLocalizedTitles(
                vi: "Bưu điện Trung tâm Sài Gòn",
                en: "Saigon Central Post Office",
                fr: "Bureau de poste central de Saigon",
                ko: "사이공 중앙우체국",
                ja: "サイゴン中央郵便局",
                zh: "西贡中央邮局")
        });

        _allItems.Add(new OfflinePackItem
        {
            Title = "Chợ Bến Thành",
            AudioCount = 3,
            Duration = "9:10",
            Size = "8.4 MB",
            SizeValue = 8.4,
            Image = "dotnet_bot.png",
            Description = "Chợ Bến Thành là biểu tượng văn hóa lâu đời, nổi tiếng với ẩm thực và đặc sản địa phương.",
            Address = "Le Loi, Ben Thanh, Quan 1, TP. Ho Chi Minh",
            Phone = "(028) 3829 4421",
            Email = "benthanhmarket@hcm.gov.vn",
            Website = "chobenthanh.vn",
            EstablishedYear = "1914",
            RadiusText = "180m",
            GpsText = "10.7725, 106.6980",
            RatingText = "9/10",
            LocalizedTitles = BuildLocalizedTitles(
                vi: "Chợ Bến Thành",
                en: "Ben Thanh Market",
                fr: "Marche Ben Thanh",
                ko: "벤탄 시장",
                ja: "ベンタイン市場",
                zh: "滨城市场")
        });

        _allItems.Add(new OfflinePackItem
        {
            Title = "Chùa Thiên Mụ",
            AudioCount = 3,
            Duration = "11:05",
            Size = "10.1 MB",
            SizeValue = 10.1,
            Image = "dotnet_bot.png",
            Description = "Chùa Thiên Mụ là ngôi chùa cổ nổi tiếng của cố đô Huế, gắn liền với nhiều truyền thuyết.",
            Address = "Huong Long, TP. Hue, Thua Thien Hue",
            Phone = "(0234) 352 1234",
            Email = "thienmu@hue.gov.vn",
            Website = "thienmupagoda.vn",
            EstablishedYear = "1601",
            RadiusText = "220m",
            GpsText = "16.4548, 107.5458",
            RatingText = "9/10",
            LocalizedTitles = BuildLocalizedTitles(
                vi: "Chùa Thiên Mụ",
                en: "Thien Mu Pagoda",
                fr: "Pagode de la Dame Celeste",
                ko: "티엔무 사원",
                ja: "ティエンムー寺",
                zh: "天姥寺")
        });
    }

    private static ObservableCollection<LocalizedTitleItem> BuildLocalizedTitles(
        string vi,
        string en,
        string fr,
        string ko,
        string ja,
        string zh)
    {
        return new ObservableCollection<LocalizedTitleItem>
        {
            new() { LanguageCode = "VI", LanguageName = "Tiếng Việt", LocalizedName = vi },
            new() { LanguageCode = "EN", LanguageName = "English", LocalizedName = en },
            new() { LanguageCode = "FR", LanguageName = "Francais", LocalizedName = fr },
            new() { LanguageCode = "KO", LanguageName = "Korean", LocalizedName = ko },
            new() { LanguageCode = "JA", LanguageName = "Japanese", LocalizedName = ja },
            new() { LanguageCode = "ZH", LanguageName = "Chinese", LocalizedName = zh }
        };
    }

    private void ApplyFilter()
    {
        if (_selectedFilter == "Downloaded")
            FilteredItems = new ObservableCollection<OfflinePackItem>(_allItems.Where(x => x.IsDownloaded));
        else if (_selectedFilter == "NotDownloaded")
            FilteredItems = new ObservableCollection<OfflinePackItem>(_allItems.Where(x => !x.IsDownloaded));
        else
            FilteredItems = new ObservableCollection<OfflinePackItem>(_allItems);

        OnPropertyChanged(nameof(DownloadedCountText));
        OnPropertyChanged(nameof(DownloadedSizeText));
        OnPropertyChanged(nameof(AllTabBg));
        OnPropertyChanged(nameof(DownloadedTabBg));
        OnPropertyChanged(nameof(NotDownloadedTabBg));
        OnPropertyChanged(nameof(AllTabTextColor));
        OnPropertyChanged(nameof(DownloadedTabTextColor));
        OnPropertyChanged(nameof(NotDownloadedTabTextColor));
    }

    private void OnDownload(OfflinePackItem? item)
    {
        if (item == null) return;

        item.IsDownloaded = true;
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
            item.IsDownloaded = true;

        ApplyFilter();
    }

    private void OnToggleExpand(OfflinePackItem? item)
    {
        if (item == null) return;

        item.IsExpanded = !item.IsExpanded;
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
        _pendingDeleteItem.IsExpanded = false;

        _pendingDeleteItem = null;
        _isBulkDeleteConfirm = false;
        _pendingBulkDeleteCount = 0;
        IsDeleteConfirmVisible = false;
        ApplyFilter();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class OfflinePackItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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
    public ObservableCollection<LocalizedTitleItem> LocalizedTitles { get; set; } = new();

    private bool _isDownloaded;
    private bool _isExpanded;

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            _isDownloaded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotDownloaded));
        }
    }

    public bool IsNotDownloaded => !IsDownloaded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExpandIcon));
        }
    }

    public string ExpandIcon => IsExpanded ? "˄" : "˅";

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

public class OfflineAudioTrack
{
    public string LanguageCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Duration { get; set; } = "";
}