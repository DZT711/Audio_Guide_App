using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class HistoryPage : ContentPage
{
    public HistoryPage()
    {
        InitializeComponent();
        ApplyTexts();
        LocalizationService.Instance.PropertyChanged += (_, _) => ApplyTexts();
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("History.Title");
        SubtitleLabel.Text = LocalizationService.Instance.T("History.Subtitle");
        CountLabel.Text = LocalizationService.Instance.T("History.Count");
        TotalLabel.Text = LocalizationService.Instance.T("History.Total");
        TodayLabel.Text = LocalizationService.Instance.T("History.Today");
        YesterdayLabel.Text = LocalizationService.Instance.T("History.Yesterday");
    }
}