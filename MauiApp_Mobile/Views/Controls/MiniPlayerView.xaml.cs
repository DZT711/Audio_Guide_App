using System.ComponentModel;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Views;

namespace MauiApp_Mobile.Views.Controls;

public partial class MiniPlayerView : ContentView
{
    public MiniPlayerView()
    {
        InitializeComponent();
        PlaybackCoordinatorService.Instance.PropertyChanged += OnPlaybackStateChanged;
        AppSettingsService.Instance.PropertyChanged += OnSettingsChanged;
        UpdateBindings();
    }

    public bool IsMiniPlayerVisible =>
        AppSettingsService.Instance.MiniPlayerEnabled &&
        PlaybackCoordinatorService.Instance.HasActivePlayback;

    public string TitleText => PlaybackCoordinatorService.Instance.CurrentTitle;
    public string SubtitleText => string.IsNullOrWhiteSpace(PlaybackCoordinatorService.Instance.CurrentSubtitle)
        ? PlaybackCoordinatorService.Instance.QueueTitle
        : PlaybackCoordinatorService.Instance.CurrentSubtitle;
    public string PlayPauseGlyph => PlaybackCoordinatorService.Instance.PlayPauseGlyph;
    public double ProgressRatio => PlaybackCoordinatorService.Instance.ProgressRatio;
    public string PositionText => FormatTime(PlaybackCoordinatorService.Instance.Position);
    public string DurationText => FormatTime(PlaybackCoordinatorService.Instance.Duration);
    public double PreviousOpacity => PlaybackCoordinatorService.Instance.CanGoPrevious ? 1d : 0.45d;
    public double NextOpacity => PlaybackCoordinatorService.Instance.CanGoNext ? 1d : 0.45d;
    public double SeekOpacity => PlaybackCoordinatorService.Instance.CanSeek ? 1d : 0.45d;

    private async void OnPlayPauseTapped(object? sender, TappedEventArgs e) =>
        await PlaybackCoordinatorService.Instance.TogglePauseResumeAsync();

    private async void OnSeekBackwardTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanSeek)
        {
            await PlaybackCoordinatorService.Instance.SeekByAsync(TimeSpan.FromSeconds(-5));
        }
    }

    private async void OnSeekForwardTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanSeek)
        {
            await PlaybackCoordinatorService.Instance.SeekByAsync(TimeSpan.FromSeconds(5));
        }
    }

    private async void OnPreviousTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanGoPrevious)
        {
            await PlaybackCoordinatorService.Instance.PlayPreviousAsync();
        }
    }

    private async void OnNextTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanGoNext)
        {
            await PlaybackCoordinatorService.Instance.PlayNextAsync();
        }
    }

    private async void OnStopTapped(object? sender, TappedEventArgs e) =>
        await PlaybackCoordinatorService.Instance.StopAsync();

    private async void OnQueueTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is null)
        {
            return;
        }

        if (Shell.Current.CurrentPage is PlaybackQueuePage)
        {
            return;
        }

        await Shell.Current.GoToAsync("playback-queue");
    }

    private void OnPlaybackStateChanged(object? sender, PropertyChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateBindings);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(AppSettingsService.MiniPlayerEnabled), StringComparison.Ordinal))
        {
            MainThread.BeginInvokeOnMainThread(UpdateBindings);
        }
    }

    private void UpdateBindings()
    {
        OnPropertyChanged(nameof(IsMiniPlayerVisible));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(ProgressRatio));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(PreviousOpacity));
        OnPropertyChanged(nameof(NextOpacity));
        OnPropertyChanged(nameof(SeekOpacity));
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");
}
