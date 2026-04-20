using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiApp_Mobile.Services;
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
    public string PreferenceImage { get; set; } = "";
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
    public IReadOnlyList<string> AvailableVoiceGenders { get; set; } = Array.Empty<string>();
    public string LanguageBadgeSummaryText { get; set; } = "";
    public DateTimeOffset? HistoryAddedAt { get; set; }
    public DateTimeOffset? LastPlaybackTriggeredAt { get; set; }
    public double? LastTriggerDistanceMeters { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ActivationRadiusMeters { get; set; }
    public double NearRadiusMeters { get; set; }
    public int Priority { get; set; }
    public int DebounceSeconds { get; set; }
    public int Status { get; set; }
    public bool IsGpsTriggerEnabled { get; set; }
    public string LastPlaybackSource { get; set; } = "";
    public string LastPlaybackTrigger { get; set; } = "";
    public Color CategoryColor { get; set; } = Colors.LightGray;
    public Color CategoryTextColor { get; set; } = Colors.Black;

    private bool _isPlayed;
    private bool _isPlaying;
    private bool _isAudioLoading;

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

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlayIcon));
            }
        }
    }

    public bool IsAudioLoading
    {
        get => _isAudioLoading;
        set
        {
            if (_isAudioLoading != value)
            {
                _isAudioLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public string PlayIcon => IsPlaying ? "❚❚" : "▶";
    public IReadOnlyList<LanguageBadgeChip> LanguageBadges => LanguageBadgeService.BuildItems(AudioTracks, LanguageBadgeSummaryText);
    public bool HasLanguageBadges => LanguageBadges.Count > 0;
    public bool HasCategory => !string.IsNullOrWhiteSpace(Category);
    public Color DisplayCategoryColor => ResolveCategoryPalette(Category).Background;
    public Color DisplayCategoryTextColor => ResolveCategoryPalette(Category).Foreground;

    public string HistoryRelativeTimeText
    {
        get
        {
            if (HistoryAddedAt is not DateTimeOffset timestamp)
            {
                return "Vừa nghe";
            }

            var elapsed = DateTimeOffset.Now - timestamp;
            if (elapsed.TotalMinutes < 1)
            {
                return "Vừa xong";
            }

            if (elapsed.TotalHours < 1)
            {
                return $"{Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes))} phút trước";
            }

            if (elapsed.TotalDays < 1)
            {
                return $"{Math.Max(1, (int)Math.Floor(elapsed.TotalHours))} giờ trước";
            }

            return $"{Math.Max(1, (int)Math.Floor(elapsed.TotalDays))} ngày trước";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static (Color Background, Color Foreground) ResolveCategoryPalette(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return (Color.FromArgb("#E5E7EB"), Color.FromArgb("#374151"));
        }

        var normalized = category.Trim().ToLowerInvariant();
        return normalized switch
        {
            var value when value.Contains("ăn") || value.Contains("food") =>
                (Color.FromArgb("#FFE3E3"), Color.FromArgb("#E53935")),
            var value when value.Contains("quán") || value.Contains("restaurant") =>
                (Color.FromArgb("#FFF7D6"), Color.FromArgb("#CA8A04")),
            var value when value.Contains("uống") || value.Contains("drink") || value.Contains("cafe") =>
                (Color.FromArgb("#E6F4FF"), Color.FromArgb("#2563EB")),
            var value when value.Contains("history") || value.Contains("heritage") || value.Contains("historical") || value.Contains("lịch sử") =>
                (Color.FromArgb("#FDECEC"), Color.FromArgb("#DC2626")),
            var value when value.Contains("landmark") || value.Contains("mốc") =>
                (Color.FromArgb("#E5F4F1"), Color.FromArgb("#0F766E")),
            var value when value.Contains("văn hóa") || value.Contains("culture") =>
                (Color.FromArgb("#F3E8FF"), Color.FromArgb("#7C3AED")),
            var value when value.Contains("tiện ích") || value.Contains("utility") =>
                (Color.FromArgb("#DCFCE7"), Color.FromArgb("#15803D")),
            _ => (Color.FromArgb("#E5F4F1"), Color.FromArgb("#0F766E"))
        };
    }
}
