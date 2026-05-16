using System.ComponentModel;

namespace MauiApp_Mobile.Services;

public sealed class MiniPlayerPresentationService : INotifyPropertyChanged
{
    public static MiniPlayerPresentationService Instance { get; } = new();

    private bool _isCollapsed;

    private MiniPlayerPresentationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        private set
        {
            if (_isCollapsed == value)
            {
                return;
            }

            _isCollapsed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCollapsed)));
        }
    }

    public void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    public void Expand() => IsCollapsed = false;
}
