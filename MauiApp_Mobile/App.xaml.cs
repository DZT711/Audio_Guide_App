using MauiApp_Mobile.Services;

namespace MauiApp_Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeService.Instance.Initialize();

        MainPage = new AppShell();
    }
}
