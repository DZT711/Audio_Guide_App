using MauiApp_Mobile.Services;
using MauiApp_Mobile.Models;

namespace MauiApp_Mobile.Views;

public partial class HistoryPage : ContentPage
{
    private const double HistoryDetailClosedOffset = 520;
    private bool _isHistoryDetailVisible;
    private PlaceItem? _selectedHistoryItem;
    private double _historyDetailStartY;

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
        }
    }

    public string HistoryDetailPriorityText => SelectedHistoryItem == null
        ? string.Empty
        : $"Độ ưu tiên {SelectedHistoryItem.Rating}";

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
    }

    private void UpdateCount()
    {
        var count = HistoryService.Instance.HistoryItems.Count;
        CountLabel.Text = $"⏱ {count} địa điểm";
        EmptyLabel.IsVisible = count == 0;
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

    private async void OnHistoryItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaceItem item)
            return;

        SelectedHistoryItem = item;
        IsHistoryDetailVisible = true;

        await Task.Yield();
        HistoryDetailSheet.TranslationY = HistoryDetailClosedOffset;
        await HistoryDetailSheet.TranslateToAsync(0, 0, 280, Easing.CubicOut);
    }

    private async void OnCloseHistoryDetail(object sender, EventArgs e)
    {
        await HideHistoryDetail();
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
                var nextY = Math.Max(0, _historyDetailStartY + e.TotalY);
                HistoryDetailSheet.TranslationY = nextY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (HistoryDetailSheet.TranslationY > 140 || e.TotalY > 120)
                {
                    await HideHistoryDetail();
                }
                else
                {
                    await HistoryDetailSheet.TranslateToAsync(0, 0, 140, Easing.CubicOut);
                }
                break;
        }
    }

    private async Task HideHistoryDetail()
    {
        if (!IsHistoryDetailVisible)
            return;

        await HistoryDetailSheet.TranslateToAsync(0, HistoryDetailClosedOffset, 220, Easing.CubicIn);
        IsHistoryDetailVisible = false;
        SelectedHistoryItem = null;
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("History.Title");
        SubtitleLabel.Text = LocalizationService.Instance.T("History.Subtitle");
        // CountLabel will be updated by UpdateCount()
    }
}
