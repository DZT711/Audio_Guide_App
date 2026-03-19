using MauiApp_Mobile.ViewModels;

namespace MauiApp_Mobile.Views;

public partial class TourListPage : ContentPage
{
    private readonly TourListViewModel _viewModel;

    public TourListPage(TourListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
