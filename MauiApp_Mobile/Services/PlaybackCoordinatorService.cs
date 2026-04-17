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
    private string? _activePlaybackSource;

    private PlaybackCoordinatorService()
    {
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
        AudioPlaybackService.Instance.PlaybackProgressChanged += OnPlaybackProgressChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<PlaybackQueueItem> Queue => _queue;
    public PlaybackQueueItem? CurrentQueueItem => _currentIndex >= 0 && _currentIndex < _queue.Count ? _queue[_currentIndex] : null;
    public PublicAudioTrackDto? CurrentTrack => AudioPlaybackService.Instance.CurrentTrack ?? CurrentQueueItem?.Track;
    public string CurrentTitle => CurrentQueueItem?.Track.Title ?? AudioPlaybackService.Instance.CurrentTrack?.Title ?? string.Empty;
    public string CurrentSubtitle => CurrentQueueItem?.Subtitle ?? string.Empty;
    public string QueueTitle => CurrentQueueItem?.QueueTitle ?? string.Empty;
    public int QueueCount => _queue.Count;
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
    public string? ActivePlaybackSource => _activePlaybackSource;

    public async Task PlayQueueAsync(
        IReadOnlyList<PlaybackQueueItem> items,
        int startIndex,
        CancellationToken cancellationToken = default) =>
        await PlayQueueAsync(items, startIndex, null, cancellationToken);

    public async Task PlayQueueAsync(
        IReadOnlyList<PlaybackQueueItem> items,
        int startIndex,
        string? playbackSource,
        CancellationToken cancellationToken = default)
    {
        var normalizedItems = NormalizeQueueItems(items);
        var selectedIdentity = items.Count > 0 && startIndex >= 0 && startIndex < items.Count
            ? GetTrackIdentity(items[startIndex].Track)
            : string.Empty;

        _queue.Clear();
        foreach (var item in normalizedItems)
        {
            _queue.Add(item);
        }

        _activePlaybackSource = NormalizePlaybackSource(playbackSource);
        _currentIndex = ResolveStartIndex(normalizedItems, selectedIdentity);
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

    public void Enqueue(PublicAudioTrackDto track, string queueTitle, string subtitle)
    {
        if (ContainsTrack(track))
        {
            NotifyStateChanged();
            return;
        }

        _queue.Add(new PlaybackQueueItem(track, queueTitle, subtitle));
        if (_currentIndex < 0)
        {
            _currentIndex = 0;
        }

        NotifyStateChanged();
    }

    public void EnqueueRange(IReadOnlyList<PlaybackQueueItem> items)
    {
        foreach (var item in items)
        {
            Enqueue(item.Track, item.QueueTitle, item.Subtitle);
        }
    }

    public bool IsPlaybackSourceActive(string sourcePrefix) =>
        !string.IsNullOrWhiteSpace(sourcePrefix) &&
        !string.IsNullOrWhiteSpace(_activePlaybackSource) &&
        _activePlaybackSource.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) &&
        HasActivePlayback;

    public Task TogglePauseResumeAsync(CancellationToken cancellationToken = default) =>
        AudioPlaybackService.Instance.TogglePauseResumeAsync(cancellationToken);

    public Task SeekByAsync(TimeSpan offset) => AudioPlaybackService.Instance.SeekByAsync(offset);

    public Task SeekToAsync(TimeSpan targetPosition) => AudioPlaybackService.Instance.SeekToAsync(targetPosition);

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
        _activePlaybackSource = null;
        NotifyStateChanged();
        await AudioPlaybackService.Instance.StopAsync();
    }

    public void RemoveQueueItem(PlaybackQueueItem item)
    {
        var index = _queue.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        _queue.RemoveAt(index);
        if (index < _currentIndex)
        {
            _currentIndex--;
        }
        else if (_currentIndex >= _queue.Count)
        {
            _currentIndex = _queue.Count - 1;
        }

        NotifyStateChanged();
    }

    public void ClearQueuedUpcoming()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        var keepCount = _currentIndex >= 0 ? _currentIndex + 1 : 0;
        while (_queue.Count > keepCount)
        {
            _queue.RemoveAt(_queue.Count - 1);
        }

        NotifyStateChanged();
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
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Playback transition canceled for track {item.Track.Id}.");
            return;
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
            if (_queue.Count == 0 || (_currentIndex >= _queue.Count - 1 && !CanGoNext))
            {
                _activePlaybackSource = null;
            }
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
        RaisePropertyChanged(nameof(QueueCount));
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
        RaisePropertyChanged(nameof(ActivePlaybackSource));
    }

    private bool ContainsTrack(PublicAudioTrackDto track)
    {
        var identity = GetTrackIdentity(track);
        if (!string.IsNullOrWhiteSpace(identity) &&
            !string.IsNullOrWhiteSpace(GetTrackIdentity(CurrentTrack)) &&
            string.Equals(identity, GetTrackIdentity(CurrentTrack), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _queue.Any(item => string.Equals(GetTrackIdentity(item.Track), identity, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<PlaybackQueueItem> NormalizeQueueItems(IReadOnlyList<PlaybackQueueItem> items)
    {
        var result = new List<PlaybackQueueItem>(items.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var identity = GetTrackIdentity(item.Track);
            if (!seen.Add(identity))
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static int ResolveStartIndex(IReadOnlyList<PlaybackQueueItem> items, string selectedIdentity)
    {
        if (items.Count == 0)
        {
            return -1;
        }

        if (!string.IsNullOrWhiteSpace(selectedIdentity))
        {
            var identityIndex = items
                .Select((item, index) => new { Identity = GetTrackIdentity(item.Track), Index = index })
                .FirstOrDefault(item => string.Equals(item.Identity, selectedIdentity, StringComparison.OrdinalIgnoreCase));

            if (identityIndex is not null)
            {
                return identityIndex.Index;
            }
        }

        return 0;
    }

    private static string GetTrackIdentity(PublicAudioTrackDto? track)
    {
        if (track is null)
        {
            return string.Empty;
        }

        if (track.Id > 0)
        {
            return $"track:{track.Id}";
        }

        return string.Join(
            "|",
            track.LocationId,
            track.Language ?? string.Empty,
            track.SourceType ?? string.Empty,
            track.Title ?? string.Empty).ToLowerInvariant();
    }

    private static string? NormalizePlaybackSource(string? playbackSource) =>
        string.IsNullOrWhiteSpace(playbackSource)
            ? null
            : playbackSource.Trim();

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record PlaybackQueueItem(
    PublicAudioTrackDto Track,
    string QueueTitle,
    string Subtitle);
