using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public sealed class PlaybackCoordinatorService : INotifyPropertyChanged
{
    public static PlaybackCoordinatorService Instance { get; } = new();

    private readonly ObservableCollection<PlaybackQueueItem> _queue = new();
    private int _currentIndex = -1;
    private bool _isTransitioning;
    private bool _manualStopRequested;
    private PublicAudioTrackDto? _lastPlaybackTrack;

    private PlaybackCoordinatorService()
    {
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
        AudioPlaybackService.Instance.PlaybackProgressChanged += OnPlaybackProgressChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<PlaybackQueueItem> Queue => _queue;
    public PlaybackQueueItem? CurrentQueueItem => _currentIndex >= 0 && _currentIndex < _queue.Count ? _queue[_currentIndex] : null;
    public PublicAudioTrackDto? CurrentTrack => AudioPlaybackService.Instance.CurrentTrack;
    public string CurrentTitle => CurrentTrack?.Title ?? string.Empty;
    public string CurrentSubtitle => CurrentQueueItem?.Subtitle ?? string.Empty;
    public string QueueTitle => CurrentQueueItem?.QueueTitle ?? string.Empty;
    public bool HasActivePlayback => CurrentTrack is not null || AudioPlaybackService.Instance.IsPaused;
    public bool CanGoPrevious => _currentIndex > 0;
    public bool CanGoNext => _currentIndex >= 0 && _currentIndex < _queue.Count - 1;
    public bool IsPlaying => AudioPlaybackService.Instance.IsPlaying;
    public bool IsPaused => AudioPlaybackService.Instance.IsPaused;
    public bool IsLoading => AudioPlaybackService.Instance.IsLoading;
    public TimeSpan Position => AudioPlaybackService.Instance.CurrentPosition;
    public TimeSpan Duration => AudioPlaybackService.Instance.CurrentDuration;
    public bool CanSeek => AudioPlaybackService.Instance.CanSeek;
    public double ProgressRatio => Duration.TotalMilliseconds <= 0 ? 0d : Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0d, 1d);
    public string PlayPauseGlyph => IsPlaying ? "❚❚" : "▶";

    public async Task PlayQueueAsync(
        IReadOnlyList<PlaybackQueueItem> items,
        int startIndex,
        CancellationToken cancellationToken = default)
    {
        _queue.Clear();
        foreach (var item in items)
        {
            _queue.Add(item);
        }

        _currentIndex = Math.Clamp(startIndex, 0, Math.Max(_queue.Count - 1, 0));
        NotifyStateChanged();

        if (_queue.Count == 0)
        {
            await StopAsync();
            return;
        }

        await PlayCurrentAsync(cancellationToken);
    }

    public async Task PlaySingleAsync(PublicAudioTrackDto track, string queueTitle, string subtitle, CancellationToken cancellationToken = default)
    {
        await PlayQueueAsync(
        [
            new PlaybackQueueItem(track, queueTitle, subtitle)
        ], 0, cancellationToken);
    }

    public Task TogglePauseResumeAsync(CancellationToken cancellationToken = default) =>
        AudioPlaybackService.Instance.TogglePauseResumeAsync(cancellationToken);

    public Task SeekByAsync(TimeSpan offset) => AudioPlaybackService.Instance.SeekByAsync(offset);

    public async Task PlayPreviousAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoPrevious)
        {
            return;
        }

        _currentIndex--;
        NotifyStateChanged();
        await PlayCurrentAsync(cancellationToken);
    }

    public async Task PlayNextAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoNext)
        {
            return;
        }

        _currentIndex++;
        NotifyStateChanged();
        await PlayCurrentAsync(cancellationToken);
    }

    public async Task StopAsync()
    {
        _manualStopRequested = true;
        _queue.Clear();
        _currentIndex = -1;
        NotifyStateChanged();
        await AudioPlaybackService.Instance.StopAsync();
    }

    private async Task PlayCurrentAsync(CancellationToken cancellationToken = default)
    {
        var item = CurrentQueueItem;
        if (item is null)
        {
            return;
        }

        try
        {
            _isTransitioning = true;
            _manualStopRequested = false;
            NotifyStateChanged();
            await AppNotificationService.EnsurePermissionAsync();
            await AudioPlaybackService.Instance.PlayAsync(item.Track, cancellationToken);
        }
        finally
        {
            _isTransitioning = false;
            NotifyStateChanged();
        }

        if (!_manualStopRequested && AudioPlaybackService.Instance.CurrentTrack is null && CanGoNext)
        {
            _currentIndex++;
            NotifyStateChanged();
            await PlayCurrentAsync(cancellationToken);
        }
    }

    private void OnPlaybackStateChanged(object? sender, PublicAudioTrackDto? currentTrack)
    {
        var previousTrack = _lastPlaybackTrack;
        _lastPlaybackTrack = currentTrack;

        if (!_isTransitioning && !_manualStopRequested && previousTrack is not null && currentTrack is null && CanGoNext)
        {
            _ = MainThread.InvokeOnMainThreadAsync(async () => await PlayNextAsync());
            return;
        }

        if (currentTrack is null)
        {
            _manualStopRequested = false;
        }

        MainThread.BeginInvokeOnMainThread(NotifyStateChanged);
    }

    private void OnPlaybackProgressChanged(object? sender, AudioPlaybackProgressSnapshot e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RaisePropertyChanged(nameof(Position));
            RaisePropertyChanged(nameof(Duration));
            RaisePropertyChanged(nameof(ProgressRatio));
            RaisePropertyChanged(nameof(CanSeek));
            RaisePropertyChanged(nameof(IsPlaying));
            RaisePropertyChanged(nameof(IsPaused));
            RaisePropertyChanged(nameof(IsLoading));
            RaisePropertyChanged(nameof(PlayPauseGlyph));
            RaisePropertyChanged(nameof(HasActivePlayback));
        });

    private void NotifyStateChanged()
    {
        RaisePropertyChanged(nameof(CurrentQueueItem));
        RaisePropertyChanged(nameof(CurrentTrack));
        RaisePropertyChanged(nameof(CurrentTitle));
        RaisePropertyChanged(nameof(CurrentSubtitle));
        RaisePropertyChanged(nameof(QueueTitle));
        RaisePropertyChanged(nameof(CanGoPrevious));
        RaisePropertyChanged(nameof(CanGoNext));
        RaisePropertyChanged(nameof(IsPlaying));
        RaisePropertyChanged(nameof(IsPaused));
        RaisePropertyChanged(nameof(IsLoading));
        RaisePropertyChanged(nameof(PlayPauseGlyph));
        RaisePropertyChanged(nameof(HasActivePlayback));
        RaisePropertyChanged(nameof(Position));
        RaisePropertyChanged(nameof(Duration));
        RaisePropertyChanged(nameof(ProgressRatio));
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record PlaybackQueueItem(
    PublicAudioTrackDto Track,
    string QueueTitle,
    string Subtitle);
