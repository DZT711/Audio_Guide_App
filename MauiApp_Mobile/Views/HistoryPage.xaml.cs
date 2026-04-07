using System.Collections.ObjectModel;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Models;

namespace MauiApp_Mobile.Views;

public partial class HistoryPage : ContentPage
{
    private const double HistoryDetailOpenTopInset = 16;
    private const double HistoryDetailFallbackClosedOffset = 520;
    private const double HistoryDetailHalfVisibleRatio = 0.58;
    private bool _isHistoryDetailVisible;
    private bool _isHistoryAudioListExpanded;
    private PlaceItem? _selectedHistoryItem;
    private double _historyDetailStartY;
    private double _historyDetailExpandedY = HistoryDetailOpenTopInset;
    private double _historyDetailHalfY = 180;
    private double _historyDetailClosedY = HistoryDetailFallbackClosedOffset;
    public ObservableCollection<HistoryAudioTrack> SelectedHistoryAudioTracks { get; } = new();

    public bool IsHistoryDetailVisible
    {
        get => _isHistoryDetailVisible;
        set
        {
            _isHistoryDetailVisible = value;
            OnPropertyChanged();
        }
    }

    public PlaceItem? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set
        {
            _selectedHistoryItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HistoryDetailPriorityText));
            OnPropertyChanged(nameof(SelectedHistoryLanguagesText));
            RefreshSelectedHistoryAudioTracks();
        }
    }

    public string HistoryDetailPriorityText => SelectedHistoryItem == null
        ? string.Empty
        : $"Độ ưu tiên {SelectedHistoryItem.Rating}";

    public bool IsHistoryAudioListExpanded
    {
        get => _isHistoryAudioListExpanded;
        set
        {
            if (_isHistoryAudioListExpanded == value)
                return;

            _isHistoryAudioListExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HistoryAudioListExpandIcon));
        }
    }

    public string HistoryAudioListExpandIcon => IsHistoryAudioListExpanded ? "triangle_up_filled.svg" : "triangle_down_filled.svg";
    public string SelectedHistoryLanguagesText => SelectedHistoryItem == null
        ? string.Empty
        : string.Join(" • ", SelectedHistoryAudioTracks.Select(track => track.LanguageCode));

    public HistoryPage()
    {
        InitializeComponent();
        
        // Bind to the Service
        BindingContext = HistoryService.Instance;

        ApplyTexts();
        LocalizationService.Instance.PropertyChanged += (_, _) => ApplyTexts();
        HistoryService.Instance.HistoryItems.CollectionChanged += (_, _) => UpdateCount();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateCount();
        UpdateHistoryDetailSheetLayout();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateHistoryDetailSheetLayout();
    }

    private void UpdateCount()
    {
        var count = HistoryService.Instance.HistoryItems.Count;
        CountLabel.Text = $"{count} địa điểm";
        DurationLabel.Text = $"{ComputeHistoryDurationText()} tổng";
        EmptyLabel.IsVisible = count == 0;
    }

    private string ComputeHistoryDurationText()
    {
        var totalSeconds = HistoryService.Instance.HistoryItems
            .Select(item => ExtractAudioCount(item.AudioCountText) * 125)
            .Sum();

        if (totalSeconds <= 0)
        {
            return "00:00";
        }

        var duration = TimeSpan.FromSeconds(totalSeconds);
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\m\ ss\s");
    }

    private static int ExtractAudioCount(string? audioCountText)
    {
        if (string.IsNullOrWhiteSpace(audioCountText))
        {
            return 1;
        }

        var digits = new string(audioCountText.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) && value > 0 ? value : 1;
    }

    private void OnDeleteTapped(object sender, TappedEventArgs e)
    {
        if (sender is Element element && element.BindingContext is PlaceItem item)
        {
            HistoryService.Instance.RemoveFromHistory(item);

            if (SelectedHistoryItem == item)
            {
                IsHistoryDetailVisible = false;
                SelectedHistoryItem = null;
            }
        }
    }

    private void OnPlayTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceItem item)
            return;

        var nextState = !item.IsPlayed;
        foreach (var historyItem in HistoryService.Instance.HistoryItems)
        {
            historyItem.IsPlayed = ReferenceEquals(historyItem, item) && nextState;
        }
    }

    private void RefreshSelectedHistoryAudioTracks()
    {
        SelectedHistoryAudioTracks.Clear();

        if (SelectedHistoryItem is null)
        {
            IsHistoryAudioListExpanded = false;
            return;
        }

        var languageTemplates = new (string LanguageCode, string LanguageName)[]
        {
            ("VI", "Tiếng Việt"),
            ("EN", "English"),
            ("FR", "Francais"),
            ("KO", "Korean"),
            ("JA", "Japanese"),
            ("ZH", "Chinese")
        };

        foreach (var item in languageTemplates)
        {
            SelectedHistoryAudioTracks.Add(new HistoryAudioTrack
            {
                LanguageCode = item.LanguageCode,
                LanguageName = item.LanguageName,
                Title = SelectedHistoryItem.Name,
                Duration = "02:05"
            });
        }

        IsHistoryAudioListExpanded = false;
        OnPropertyChanged(nameof(SelectedHistoryLanguagesText));
    }

    private async void OnClearHistoryTapped(object? sender, TappedEventArgs e)
    {
        if (HistoryService.Instance.HistoryItems.Count == 0)
        {
            return;
        }

        var shouldClear = await DisplayAlertAsync(
            "Xóa lịch sử",
            "Bạn có muốn xóa toàn bộ lịch sử nghe gần đây không?",
            "Xóa",
            "Hủy");

        if (!shouldClear)
        {
            return;
        }

        HistoryService.Instance.ClearHistory();

        if (IsHistoryDetailVisible)
        {
            await HideHistoryDetail();
            return;
        }

        SelectedHistoryItem = null;
        IsHistoryAudioListExpanded = false;
    }

    private async void OnHistoryItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceItem item)
            return;

        SelectedHistoryItem = item;
        IsHistoryAudioListExpanded = false;
        IsHistoryDetailVisible = true;
        await ShowHistoryDetailAsync();
    }

    private async void OnViewHistoryPlaceOnMapTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedHistoryItem is null)
            return;

        try
        {
            PlaceNavigationService.Instance.RequestMapFocus(SelectedHistoryItem.Id);
            await HideHistoryDetail();

            if (Application.Current?.Windows.FirstOrDefault()?.Page is AppShell appShell)
            {
                await appShell.NavigateToMapTabAsync();
                return;
            }

            await Shell.Current.GoToAsync("//mainTabs/map");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigate history item to map error: {ex.Message}");
        }
    }

    private async void OnCloseHistoryDetail(object sender, EventArgs e)
    {
        await HideHistoryDetail();
    }

    private void OnToggleHistoryAudioListTapped(object sender, TappedEventArgs e)
    {
        if (SelectedHistoryItem is null)
            return;

        IsHistoryAudioListExpanded = !IsHistoryAudioListExpanded;
    }

    private void OnHistoryAudioTrackPlayTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not HistoryAudioTrack track)
            return;

        foreach (var item in SelectedHistoryAudioTracks)
        {
            item.IsPlaying = ReferenceEquals(item, track) && !track.IsPlaying;
        }
    }

    private async void OnHistoryDetailHandleTapped(object sender, TappedEventArgs e)
    {
        await HideHistoryDetail();
    }

    private async void OnHistoryDetailBackdropTapped(object sender, TappedEventArgs e)
    {
        await HideHistoryDetail();
    }

    private async void OnHistoryDetailPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsHistoryDetailVisible)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _historyDetailStartY = HistoryDetailSheet.TranslationY;
                break;

            case GestureStatus.Running:
                var nextY = Math.Clamp(_historyDetailStartY + e.TotalY, _historyDetailExpandedY, _historyDetailClosedY);
                HistoryDetailSheet.TranslationY = nextY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var targetY = ResolveHistoryDetailSnapTarget(HistoryDetailSheet.TranslationY, e.TotalY);
                if (targetY >= _historyDetailClosedY - 1)
                {
                    await HideHistoryDetail();
                }
                else
                {
                    await HistoryDetailSheet.TranslateToAsync(0, targetY, 170, Easing.CubicOut);
                }
                break;
        }
    }

    private async Task ShowHistoryDetailAsync()
    {
        await Task.Yield();
        UpdateHistoryDetailSheetLayout();
        HistoryDetailSheet.TranslationY = _historyDetailClosedY;
        await HistoryDetailSheet.TranslateToAsync(0, _historyDetailHalfY, 300, Easing.CubicOut);
    }

    private async Task HideHistoryDetail()
    {
        if (!IsHistoryDetailVisible)
            return;

        UpdateHistoryDetailSheetLayout();
        await HistoryDetailSheet.TranslateToAsync(0, _historyDetailClosedY, 230, Easing.CubicIn);
        IsHistoryDetailVisible = false;
        IsHistoryAudioListExpanded = false;
        SelectedHistoryItem = null;
    }

    private void UpdateHistoryDetailSheetLayout()
    {
        if (Height <= 0 || HistoryDetailSheet is null)
            return;

        var maxSheetHeight = Math.Max(360, Height * 0.88);
        HistoryDetailSheet.MaximumHeightRequest = maxSheetHeight;

        _historyDetailExpandedY = HistoryDetailOpenTopInset;
        var halfVisibleHeight = Math.Max(320, Height * HistoryDetailHalfVisibleRatio);
        _historyDetailHalfY = Math.Clamp(
            maxSheetHeight - halfVisibleHeight,
            _historyDetailExpandedY + 72,
            _historyDetailExpandedY + 300);
        _historyDetailClosedY = Math.Max(HistoryDetailFallbackClosedOffset, maxSheetHeight + 48);

        if (!IsHistoryDetailVisible)
        {
            HistoryDetailSheet.TranslationY = _historyDetailClosedY;
        }
    }

    private double ResolveHistoryDetailSnapTarget(double currentY, double totalDragY)
    {
        var expandedHalfMid = (_historyDetailExpandedY + _historyDetailHalfY) / 2;
        var halfClosedMid = (_historyDetailHalfY + _historyDetailClosedY) / 2;

        if (totalDragY < -80)
        {
            return _historyDetailExpandedY;
        }

        if (totalDragY > 160 && currentY > _historyDetailHalfY + 24)
        {
            return _historyDetailClosedY;
        }

        if (currentY <= expandedHalfMid)
        {
            return _historyDetailExpandedY;
        }

        if (currentY <= halfClosedMid)
        {
            return _historyDetailHalfY;
        }

        return _historyDetailClosedY;
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("History.Title");
        SubtitleLabel.Text = LocalizationService.Instance.T("History.Subtitle");
        // CountLabel will be updated by UpdateCount()
    }
}

public class HistoryAudioTrack : BindableObject
{
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string MetaText => $"{LanguageName} • {Duration}";

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

    public string PlayIcon => IsPlaying ? "❚❚" : "▶";
}
