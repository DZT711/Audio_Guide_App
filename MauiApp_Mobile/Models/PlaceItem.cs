using System.ComponentModel;
using System.Runtime.CompilerServices;

using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Models;

public class PlaceItem : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string AudioDescription { get; set; } = "";
    public string Category { get; set; } = "";
    public string Rating { get; set; } = "";
    public string Image { get; set; } = "";
    public IReadOnlyList<string> GalleryImages { get; set; } = Array.Empty<string>();
    public string Address { get; set; } = ""; // Added for History page detail
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string EstablishedYear { get; set; } = "";
    public string RadiusText { get; set; } = "";
    public string StandbyRadiusText { get; set; } = "";
    public string GpsText { get; set; } = "";
    public string PriorityText { get; set; } = "";
    public string DebounceText { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string GpsTriggerText { get; set; } = "";
    public string AudioCountText { get; set; } = "";
    public IReadOnlyList<PublicAudioTrackDto> AudioTracks { get; set; } = Array.Empty<PublicAudioTrackDto>();
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Color CategoryColor { get; set; } = Colors.LightGray;
    public Color CategoryTextColor { get; set; } = Colors.Black;

    private bool _isPlayed;
    public bool IsPlayed
    {
        get => _isPlayed;
        set
        {
            if (_isPlayed != value)
            {
                _isPlayed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayIcon));
            }
        }
    }

    public string PlayIcon => IsPlayed ? "🔊" : "▶";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
