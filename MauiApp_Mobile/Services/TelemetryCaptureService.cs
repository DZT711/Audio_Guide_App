using Microsoft.Maui.Networking;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using System.Globalization;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public sealed class TelemetryCaptureService
{
    private const double RouteThrottleDistanceMeters = 18d;
    private static readonly TimeSpan RouteThrottleInterval = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RouteBufferFlushInterval = TimeSpan.FromSeconds(15);
    private readonly List<RouteTelemetryQueueRecord> _routeBuffer = [];
    private readonly object _routeGate = new();
    private readonly object _playbackGate = new();
    private CancellationTokenSource? _backgroundCts;
    private bool _started;
    private DateTimeOffset? _lastAcceptedRouteAtUtc;
    private Location? _lastAcceptedRouteLocation;
    private ActivePlaybackTelemetryState? _activePlayback;

    public static TelemetryCaptureService Instance { get; } = new();

    private TelemetryCaptureService()
    {
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _backgroundCts = new CancellationTokenSource();
        _started = true;

        LocationTrackingService.Instance.LocationUpdated += OnLocationUpdated;
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
        AudioPlaybackService.Instance.PlaybackProgressChanged += OnPlaybackProgressChanged;
        _ = RunRouteBufferFlushLoopAsync(_backgroundCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

        ActivePlaybackTelemetryState? activePlaybackAtStop = null;
        var stoppedAtUtc = DateTimeOffset.UtcNow;

        lock (_playbackGate)
        {
            activePlaybackAtStop = _activePlayback;
            _activePlayback = null;
        }

        _started = false;
        LocationTrackingService.Instance.LocationUpdated -= OnLocationUpdated;
        AudioPlaybackService.Instance.PlaybackStateChanged -= OnPlaybackStateChanged;
        AudioPlaybackService.Instance.PlaybackProgressChanged -= OnPlaybackProgressChanged;

        if (_backgroundCts is not null)
        {
            try
            {
                _backgroundCts.Cancel();
            }
            catch
            {
            }
            _backgroundCts.Dispose();
            _backgroundCts = null;
        }

        if (activePlaybackAtStop is not null)
        {
            await PersistPlaybackEndedAsync(activePlaybackAtStop, stoppedAtUtc);
        }

        await FlushRouteBufferAsync(cancellationToken);
    }

    private void OnLocationUpdated(object? sender, LocationSample sample)
    {
        if (!IsFinite(sample.Location.Latitude) || !IsFinite(sample.Location.Longitude))
        {
            return;
        }

        lock (_routeGate)
        {
            if (ShouldThrottleRouteSample(sample))
            {
                return;
            }

            var identity = TelemetryAnonymizerService.Instance.CreateIdentitySnapshot();
            _lastAcceptedRouteAtUtc = sample.CapturedAtUtc;
            _lastAcceptedRouteLocation = sample.Location;

            _routeBuffer.Add(new RouteTelemetryQueueRecord(
                identity.DeviceHash,
                identity.SessionHash,
                sample.CapturedAtUtc,
                sample.Location.Latitude,
                sample.Location.Longitude,
                sample.Location.Accuracy,
                sample.Location.Speed,
                TryGetBatteryPercent(),
                sample.IsForeground,
                TourId: null,
                PoiId: ResolveCurrentPoiId(),
                Context: ResolvePlaybackSource()));

            if (_routeBuffer.Count < 8)
            {
                return;
            }
        }

        _ = FlushRouteBufferAsync(CancellationToken.None);
    }

    private void OnPlaybackProgressChanged(object? sender, AudioPlaybackProgressSnapshot snapshot)
    {
        lock (_playbackGate)
        {
            if (_activePlayback is null || snapshot.Track is null)
            {
                return;
            }

            if (!string.Equals(_activePlayback.TrackIdentity, GetTrackIdentity(snapshot.Track), StringComparison.Ordinal))
            {
                return;
            }

            _activePlayback = _activePlayback with
            {
                LastKnownPositionSeconds = Math.Max(0d, snapshot.Position.TotalSeconds),
                LastKnownDurationSeconds = Math.Max(0d, snapshot.Duration.TotalSeconds)
            };
        }
    }

    private void OnPlaybackStateChanged(object? sender, PublicAudioTrackDto? currentTrack)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        ActivePlaybackTelemetryState? endedState = null;
        ActivePlaybackTelemetryState? startedState = null;

        lock (_playbackGate)
        {
            var existing = _activePlayback;
            if (currentTrack is null)
            {
                endedState = existing;
                _activePlayback = null;
            }
            else
            {
                var identity = GetTrackIdentity(currentTrack);
                if (existing is not null && !string.Equals(existing.TrackIdentity, identity, StringComparison.Ordinal))
                {
                    endedState = existing;
                }

                if (existing is null || !string.Equals(existing.TrackIdentity, identity, StringComparison.Ordinal))
                {
                    var sessionIdentity = TelemetryAnonymizerService.Instance.CreateIdentitySnapshot();
                    startedState = new ActivePlaybackTelemetryState(
                        TrackIdentity: identity,
                        DeviceHash: sessionIdentity.DeviceHash,
                        SessionHash: sessionIdentity.SessionHash,
                        AudioId: currentTrack.Id > 0 ? currentTrack.Id : null,
                        PoiId: currentTrack.LocationId > 0 ? currentTrack.LocationId : null,
                        TourId: null,
                        StartedAtUtc: nowUtc,
                        TriggerSource: ResolvePlaybackSource(),
                        LastKnownPositionSeconds: 0d,
                        LastKnownDurationSeconds: Math.Max(0d, currentTrack.Duration),
                        Context: currentTrack.SourceType);

                    _activePlayback = startedState;
                }
            }
        }

        if (endedState is not null)
        {
            _ = PersistPlaybackEndedAsync(endedState, nowUtc);
        }

        if (startedState is not null)
        {
            _ = PersistPlaybackStartedAsync(startedState, nowUtc);
        }
    }

    private async Task PersistPlaybackStartedAsync(ActivePlaybackTelemetryState state, DateTimeOffset playedAtUtc)
    {
        try
        {
            await MobileDatabaseService.Instance.EnqueueAudioPlayEventsAsync(
            [
                new AudioPlayEventQueueRecord(
                    state.DeviceHash,
                    state.SessionHash,
                    playedAtUtc,
                    state.AudioId,
                    state.PoiId,
                    state.TourId,
                    "Started",
                    state.TriggerSource,
                    ListeningSeconds: null,
                    PositionSeconds: 0d,
                    BatteryPercent: TryGetBatteryPercent(),
                    NetworkType: ResolveNetworkType(),
                    Context: state.Context)
            ]);

            await TelemetrySyncService.Instance.TriggerSyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryCapture] Failed to enqueue playback started event: {ex.Message}");
        }
    }

    private async Task PersistPlaybackEndedAsync(ActivePlaybackTelemetryState state, DateTimeOffset endedAtUtc)
    {
        try
        {
            var listenedSeconds = ResolveListeningSeconds(state, endedAtUtc);
            var completed = IsCompleted(state, listenedSeconds);
            var eventType = completed ? "Completed" : "Interrupted";

            await MobileDatabaseService.Instance.EnqueueAudioPlayEventsAsync(
            [
                new AudioPlayEventQueueRecord(
                    state.DeviceHash,
                    state.SessionHash,
                    endedAtUtc,
                    state.AudioId,
                    state.PoiId,
                    state.TourId,
                    eventType,
                    state.TriggerSource,
                    ListeningSeconds: listenedSeconds,
                    PositionSeconds: state.LastKnownPositionSeconds,
                    BatteryPercent: TryGetBatteryPercent(),
                    NetworkType: ResolveNetworkType(),
                    Context: state.Context)
            ]);

            await MobileDatabaseService.Instance.EnqueueAudioListeningSessionsAsync(
            [
                new AudioListeningSessionQueueRecord(
                    state.DeviceHash,
                    state.SessionHash,
                    state.AudioId,
                    state.PoiId,
                    state.TourId,
                    state.StartedAtUtc,
                    endedAtUtc,
                    listenedSeconds,
                    completed,
                    completed ? null : "interrupted",
                    state.Context)
            ]);

            await AnalyticsService.Instance.TrackEventAsync(
                UsageEventType.PlayAudio,
                referenceId: state.PoiId?.ToString(CultureInfo.InvariantCulture),
                durationSeconds: listenedSeconds,
                details: completed
                    ? "{\"state\":\"completed\",\"source\":\"playback\"}"
                    : "{\"state\":\"interrupted\",\"source\":\"playback\"}");

            await TelemetrySyncService.Instance.TriggerSyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryCapture] Failed to enqueue playback ended event: {ex.Message}");
        }
    }

    private async Task RunRouteBufferFlushLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(RouteBufferFlushInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await FlushRouteBufferAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task FlushRouteBufferAsync(CancellationToken cancellationToken)
    {
        List<RouteTelemetryQueueRecord> pending;
        lock (_routeGate)
        {
            if (_routeBuffer.Count == 0)
            {
                return;
            }

            pending = _routeBuffer.ToList();
            _routeBuffer.Clear();
        }

        try
        {
            await MobileDatabaseService.Instance.EnqueueRouteTelemetryBatchAsync(pending, cancellationToken);
            await TelemetrySyncService.Instance.TriggerSyncAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_routeGate)
            {
                _routeBuffer.InsertRange(0, pending);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryCapture] Failed to flush route telemetry: {ex.Message}");
            lock (_routeGate)
            {
                _routeBuffer.InsertRange(0, pending);
            }
        }
    }

    private bool ShouldThrottleRouteSample(LocationSample sample)
    {
        if (!_lastAcceptedRouteAtUtc.HasValue || _lastAcceptedRouteLocation is null)
        {
            return false;
        }

        var elapsed = sample.CapturedAtUtc - _lastAcceptedRouteAtUtc.Value;
        if (elapsed >= RouteThrottleInterval)
        {
            return false;
        }

        var distanceMeters = CalculateDistanceMeters(
            _lastAcceptedRouteLocation.Latitude,
            _lastAcceptedRouteLocation.Longitude,
            sample.Location.Latitude,
            sample.Location.Longitude);

        return distanceMeters < RouteThrottleDistanceMeters;
    }

    private static int ResolveListeningSeconds(ActivePlaybackTelemetryState state, DateTimeOffset endedAtUtc)
    {
        var elapsedSeconds = Math.Max(0d, (endedAtUtc - state.StartedAtUtc).TotalSeconds);
        var positionSeconds = Math.Max(0d, state.LastKnownPositionSeconds);
        var bestEffort = Math.Max(positionSeconds, elapsedSeconds);
        return Math.Max(1, (int)Math.Round(bestEffort, MidpointRounding.AwayFromZero));
    }

    private static bool IsCompleted(ActivePlaybackTelemetryState state, int listenedSeconds)
    {
        var durationSeconds = state.LastKnownDurationSeconds;
        if (durationSeconds <= 0d)
        {
            return listenedSeconds >= 30;
        }

        var ratio = listenedSeconds / durationSeconds;
        return ratio >= 0.9d;
    }

    private static string GetTrackIdentity(PublicAudioTrackDto track)
    {
        if (track.Id > 0)
        {
            return $"track:{track.Id}";
        }

        return $"location:{track.LocationId}|title:{track.Title}|language:{track.Language}";
    }

    private static int? TryGetBatteryPercent()
    {
        try
        {
            var battery = Battery.Default.ChargeLevel;
            if (battery <= 0d)
            {
                return null;
            }

            return (int)Math.Round(Math.Clamp(battery, 0d, 1d) * 100d, MidpointRounding.AwayFromZero);
        }
        catch
        {
            return null;
        }
    }

    private static int? ResolveCurrentPoiId()
    {
        var locationId = PlaybackCoordinatorService.Instance.CurrentTrack?.LocationId ?? 0;
        return locationId > 0 ? locationId : null;
    }

    private static string ResolvePlaybackSource()
    {
        var source = PlaybackCoordinatorService.Instance.ActivePlaybackSource;
        return string.IsNullOrWhiteSpace(source) ? "unknown" : source;
    }

    private static string ResolveNetworkType()
    {
        var profile = Connectivity.Current.ConnectionProfiles.FirstOrDefault();
        if (profile != ConnectionProfile.Unknown)
        {
            return profile.ToString();
        }

        return Connectivity.Current.NetworkAccess.ToString();
    }

    private static double CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusMeters = 6371000d;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var startLatitude = DegreesToRadians(latitude1);
        var endLatitude = DegreesToRadians(latitude2);

        var haversine = Math.Sin(latitudeDelta / 2d) * Math.Sin(latitudeDelta / 2d)
            + Math.Cos(startLatitude) * Math.Cos(endLatitude)
            * Math.Sin(longitudeDelta / 2d) * Math.Sin(longitudeDelta / 2d);
        var arc = 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine));
        return earthRadiusMeters * arc;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180d);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private sealed record ActivePlaybackTelemetryState(
        string TrackIdentity,
        string DeviceHash,
        string SessionHash,
        int? AudioId,
        int? PoiId,
        int? TourId,
        DateTimeOffset StartedAtUtc,
        string TriggerSource,
        double LastKnownPositionSeconds,
        double LastKnownDurationSeconds,
        string? Context);
}
