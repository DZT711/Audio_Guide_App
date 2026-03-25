using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class MapPage : ContentPage
{
    public MapPage()
    {
        InitializeComponent();

        ApplyTexts();
        LocalizationService.Instance.PropertyChanged += (_, _) => ApplyTexts();
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Map.Title");
        SearchEntry.Placeholder = LocalizationService.Instance.T("Map.Search");
    }
}