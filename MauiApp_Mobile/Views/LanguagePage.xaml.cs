namespace MauiApp_Mobile.Views;

public partial class LanguagePage : ContentPage
{
    public LanguagePage()
    {
        InitializeComponent();
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//places");
    }
}