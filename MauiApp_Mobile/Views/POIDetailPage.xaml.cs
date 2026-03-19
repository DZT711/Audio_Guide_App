using MauiApp_Mobile.ViewModels;

namespace MauiApp_Mobile.Views;

public partial class POIDetailPage : ContentPage
{
    public POIDetailPage(POIDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
