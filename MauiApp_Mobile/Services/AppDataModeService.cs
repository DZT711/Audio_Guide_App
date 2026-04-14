using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;

namespace MauiApp_Mobile.Services;

public sealed class AppDataModeService : INotifyPropertyChanged
{
    public static AppDataModeService Instance { get; } = new();

    private const string ApiModePreferenceKey = "app-data-mode-api-enabled";
    private bool _isApiEnabled;

    private AppDataModeService()
    {
        _isApiEnabled = Preferences.Default.Get(ApiModePreferenceKey, true);
    }

    public bool IsApiEnabled
    {
        get => _isApiEnabled;
        set
        {
            if (_isApiEnabled == value)
            {
                return;
            }

            _isApiEnabled = value;
            Preferences.Default.Set(ApiModePreferenceKey, value);
            OnPropertyChanged();
        }
    }

    public void Initialize(bool value)
    {
        if (_isApiEnabled == value)
        {
            return;
        }

        _isApiEnabled = value;
        Preferences.Default.Set(ApiModePreferenceKey, value);
        OnPropertyChanged(nameof(IsApiEnabled));
    }

    public void SwitchToOfflineFallback()
    {
        IsApiEnabled = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
