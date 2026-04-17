using System.ComponentModel;
using MauiApp_Mobile.Services;
using MauiApp_Mobile.Views;

namespace MauiApp_Mobile.Views.Controls;

public partial class MiniPlayerView : ContentView
{
    public static readonly BindableProperty UseTransparentChromeProperty =
        BindableProperty.Create(
            nameof(UseTransparentChrome),
            typeof(bool),
            typeof(MiniPlayerView),
            false);

    public MiniPlayerView()
    {
        InitializeComponent();
        PlaybackCoordinatorService.Instance.PropertyChanged += OnPlaybackStateChanged;
        AppSettingsService.Instance.PropertyChanged += OnSettingsChanged;
        MiniPlayerPresentationService.Instance.PropertyChanged += OnPresentationChanged;
        UpdateBindings();
    }

    public bool IsMiniPlayerVisible =>
        AppSettingsService.Instance.MiniPlayerEnabled &&
        PlaybackCoordinatorService.Instance.HasActivePlayback;
    public bool UseTransparentChrome
    {
        get => (bool)GetValue(UseTransparentChromeProperty);
        set => SetValue(UseTransparentChromeProperty, value);
    }

    public string TitleText => PlaybackCoordinatorService.Instance.CurrentTitle;
    public string SubtitleText => string.IsNullOrWhiteSpace(PlaybackCoordinatorService.Instance.CurrentSubtitle)
        ? PlaybackCoordinatorService.Instance.QueueTitle
        : PlaybackCoordinatorService.Instance.CurrentSubtitle;
    public IReadOnlyList<PlaybackQueueItem> Queue => PlaybackCoordinatorService.Instance.Queue;
    public bool HasQueueItems => Queue.Count > 0;
    public string QueueCountText => Queue.Count == 1 ? "1 item" : $"{Queue.Count} items";
    public string PlayPauseGlyph => PlaybackCoordinatorService.Instance.PlayPauseGlyph;
    public double ProgressRatio => PlaybackCoordinatorService.Instance.ProgressRatio;
    public string PositionText => FormatTime(PlaybackCoordinatorService.Instance.Position);
    public string DurationText => FormatTime(PlaybackCoordinatorService.Instance.Duration);
    public double PreviousOpacity => PlaybackCoordinatorService.Instance.CanGoPrevious ? 1d : 0.45d;
    public double NextOpacity => PlaybackCoordinatorService.Instance.CanGoNext ? 1d : 0.45d;
    public double SeekOpacity => PlaybackCoordinatorService.Instance.CanSeek ? 1d : 0.45d;
    public bool IsExpanded => !MiniPlayerPresentationService.Instance.IsCollapsed;
    public string CollapseGlyph => IsExpanded ? "⌄" : "⌃";
    public string CollapseIconSource => IsExpanded ? "triangle_up_filled.svg" : "triangle_down_filled.svg";

    private async void OnPlayPauseTapped(object? sender, TappedEventArgs e) =>
        await ExecutePlaybackActionAsync(() => PlaybackCoordinatorService.Instance.TogglePauseResumeAsync());

    private async void OnSeekBackwardTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanSeek)
        {
            await ExecutePlaybackActionAsync(() => PlaybackCoordinatorService.Instance.SeekByAsync(TimeSpan.FromSeconds(-5)));
        }
    }

    private async void OnSeekForwardTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanSeek)
        {
            await ExecutePlaybackActionAsync(() => PlaybackCoordinatorService.Instance.SeekByAsync(TimeSpan.FromSeconds(5)));
        }
    }

    private async void OnPreviousTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanGoPrevious)
        {
            await ExecutePlaybackActionAsync(() => PlaybackCoordinatorService.Instance.PlayPreviousAsync());
        }
    }

    private async void OnNextTapped(object? sender, TappedEventArgs e)
    {
        if (PlaybackCoordinatorService.Instance.CanGoNext)
        {
            await ExecutePlaybackActionAsync(() => PlaybackCoordinatorService.Instance.PlayNextAsync());
        }
    }

    private async void OnStopTapped(object? sender, TappedEventArgs e) =>
        await ExecutePlaybackActionAsync(() => PlaybackCoordinatorService.Instance.StopAsync());

    private void OnCollapseTapped(object? sender, TappedEventArgs e)
    {
        MiniPlayerPresentationService.Instance.ToggleCollapsed();
        UpdateBindings();
    }

    private async void OnQueueTapped(object? sender, TappedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mini player queue navigation error: {ex}");
        }
    }

    private void OnRemoveQueuedItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaybackQueueItem item)
        {
            return;
        }

        PlaybackCoordinatorService.Instance.RemoveQueueItem(item);
    }

    private void OnPlaybackStateChanged(object? sender, PropertyChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateBindings);

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs e) =>
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
        OnPropertyChanged(nameof(Queue));
        OnPropertyChanged(nameof(HasQueueItems));
        OnPropertyChanged(nameof(QueueCountText));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(ProgressRatio));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(PreviousOpacity));
        OnPropertyChanged(nameof(NextOpacity));
        OnPropertyChanged(nameof(SeekOpacity));
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(CollapseGlyph));
        OnPropertyChanged(nameof(CollapseIconSource));
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static async Task ExecutePlaybackActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mini player action error: {ex}");
        }
    }
}
