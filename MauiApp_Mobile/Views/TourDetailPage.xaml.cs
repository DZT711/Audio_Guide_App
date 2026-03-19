using MauiApp_Mobile.ViewModels;

namespace MauiApp_Mobile.Views;

public partial class TourDetailPage : ContentPage
{
    public TourDetailPage(TourDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
