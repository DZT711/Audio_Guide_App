using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiApp_Mobile.Models;

public sealed class CategoryFilterOption : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _displayName = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value)
            {
                return;
            }

            _displayName = value;
            OnPropertyChanged();
        }
    }

    public string Icon { get; init; } = "🏷";

    public bool IsAllOption { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
