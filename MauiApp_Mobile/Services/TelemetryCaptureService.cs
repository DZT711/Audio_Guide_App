using Microsoft.Maui.Networking;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using System.Globalization;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Geofencing;
using MauiApp_Mobile.Services.Geofencing;

namespace MauiApp_Mobile.Services;

public sealed class TelemetryCaptureService
{
    private const double RouteThrottleDistanceMeters = 18d;
    private const double DwellMovementToleranceMeters = 15d;
    private const double DwellMaxSpeedMetersPerSecond = 5d / 3.6d;
    private static readonly TimeSpan RouteThrottleInterval = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RouteBufferFlushInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DwellMinimumStayWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StaleLocationWindow = TimeSpan.FromMinutes(2);
    private readonly List<RouteTelemetryQueueRecord> _routeBuffer = [];
    private readonly object _backgroundTaskGate = new();
    private readonly object _routeGate = new();
    private readonly object _playbackGate = new();
    private readonly object _heatmapGate = new();
    private readonly List<Task> _backgroundTasks = [];
    private readonly Dictionary<int, PendingDwellCandidate> _pendingDwellCandidates = [];
    private CancellationTokenSource? _backgroundCts;
    private bool _started;
    private DateTimeOffset? _lastAcceptedRouteAtUtc;
    private Location? _lastAcceptedRouteLocation;
    private LocationSample? _lastLocationSample;
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
        GeofenceOrchestratorService.Instance.TriggerAccepted += OnGeofenceTriggerAccepted;
        AudioPlaybackService.Instance.PlaybackStateChanged += OnPlaybackStateChanged;
        AudioPlaybackService.Instance.PlaybackProgressChanged += OnPlaybackProgressChanged;
        TrackBackgroundTask(RunRouteBufferFlushLoopAsync(_backgroundCts.Token), "route-buffer-flush-loop");
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
        GeofenceOrchestratorService.Instance.TriggerAccepted -= OnGeofenceTriggerAccepted;
        AudioPlaybackService.Instance.PlaybackStateChanged -= OnPlaybackStateChanged;
        AudioPlaybackService.Instance.PlaybackProgressChanged -= OnPlaybackProgressChanged;

        lock (_heatmapGate)
        {
            _pendingDwellCandidates.Clear();
            _lastLocationSample = null;
        }

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

        await WaitForTrackedBackgroundTasksAsync(TimeSpan.FromSeconds(5), cancellationToken);

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

        if (DeveloperLocationSessionService.Instance.IsActive)
        {
            return;
        }

        List<HeatmapEventQueueRecord>? dueHeatmapEvents = null;
        var shouldFlushRouteBuffer = false;

        lock (_routeGate)
        {
            if (!ShouldThrottleRouteSample(sample))
            {
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

                shouldFlushRouteBuffer = _routeBuffer.Count >= 8;
            }
        }

        if (shouldFlushRouteBuffer)
        {
            TrackBackgroundTask(FlushRouteBufferAsync(CancellationToken.None), "route-buffer-threshold-flush");
        }

        lock (_heatmapGate)
        {
            _lastLocationSample = sample;
            dueHeatmapEvents = CollectDueDwellEventsLocked(sample);
        }

        if (dueHeatmapEvents is { Count: > 0 })
        {
            TrackBackgroundTask(PersistHeatmapEventsAsync(dueHeatmapEvents), "persist-heatmap-events");
        }
    }

    public void RecordDeveloperLocationSample(Location location, bool isForeground = true)
    {
        if (!_started || !IsFinite(location.Latitude) || !IsFinite(location.Longitude))
        {
            return;
        }

        List<HeatmapEventQueueRecord>? dueHeatmapEvents = null;
        var sample = new LocationSample(
            location,
            isForeground,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(5));

        lock (_heatmapGate)
        {
            _lastLocationSample = sample;
            dueHeatmapEvents = CollectDueDwellEventsLocked(sample);
        }

        if (dueHeatmapEvents is { Count: > 0 })
        {
            TrackBackgroundTask(PersistHeatmapEventsAsync(dueHeatmapEvents), "persist-developer-heatmap-events");
        }
    }

    private void OnGeofenceTriggerAccepted(object? sender, GeofenceTriggeredEvent trigger)
    {
        if (trigger.EventType != GeofenceTriggerEvent.EnteredRadius
            || !int.TryParse(trigger.Definition.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var poiId)
            || poiId <= 0)
        {
            return;
        }

        var snapshot = ResolveHeatmapLocationSnapshot(poiId, trigger.Definition, trigger.OccurredAtUtc);
        if (snapshot is null)
        {
            return;
        }

        var identity = TelemetryAnonymizerService.Instance.CreateIdentitySnapshot();

        var heatmapEvent = new HeatmapEventQueueRecord(
            DeviceHash: identity.DeviceHash,
            SessionHash: identity.SessionHash,
            CapturedAtUtc: trigger.OccurredAtUtc,
            Latitude: snapshot.Latitude,
            Longitude: snapshot.Longitude,
            AccuracyMeters: snapshot.AccuracyMeters,
            SpeedMetersPerSecond: snapshot.SpeedMetersPerSecond,
            BatteryPercent: TryGetBatteryPercent(),
            IsForeground: snapshot.IsForeground,
            PoiId: poiId,
            TourId: null,
            EventType: HeatmapEventTypes.EnterPoi,
            Weight: HeatmapEventTypes.ResolveWeight(HeatmapEventTypes.EnterPoi),
            TriggerSource: ResolveHeatmapTriggerSource(trigger),
            Context: trigger.IsNativeTransition ? "native-geofence-enter" : "distance-geofence-enter");

        lock (_heatmapGate)
        {
            _pendingDwellCandidates[poiId] = new PendingDwellCandidate(
                PoiId: poiId,
                TourId: null,
                PoiLatitude: trigger.Definition.Latitude,
                PoiLongitude: trigger.Definition.Longitude,
                ActivationRadiusMeters: Math.Max(1d, trigger.Definition.ActivationRadiusMeters),
                StableSinceUtc: trigger.OccurredAtUtc,
                AnchorLatitude: snapshot.Latitude,
                AnchorLongitude: snapshot.Longitude,
                TriggerSource: heatmapEvent.TriggerSource,
                IsForeground: snapshot.IsForeground);
        }

        TrackBackgroundTask(PersistHeatmapEventsAsync([heatmapEvent]), "persist-enter-poi-heatmap-event");
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
            TrackBackgroundTask(PersistPlaybackEndedAsync(endedState, nowUtc), "persist-playback-ended");
        }

        if (startedState is not null)
        {
            TrackBackgroundTask(PersistPlaybackStartedAsync(startedState, nowUtc), "persist-playback-started");
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

            var heatmapEvent = CreatePlaybackHeatmapEvent(state, playedAtUtc);
            if (heatmapEvent is not null)
            {
                await MobileDatabaseService.Instance.EnqueueHeatmapEventsAsync([heatmapEvent]);
            }

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

    private List<HeatmapEventQueueRecord>? CollectDueDwellEventsLocked(LocationSample sample)
    {
        if (_pendingDwellCandidates.Count == 0)
        {
            return null;
        }

        List<HeatmapEventQueueRecord>? dueEvents = null;
        foreach (var poiId in _pendingDwellCandidates.Keys.ToArray())
        {
            if (!_pendingDwellCandidates.TryGetValue(poiId, out var candidate))
            {
                continue;
            }

            var distanceToPoiMeters = CalculateDistanceMeters(
                sample.Location.Latitude,
                sample.Location.Longitude,
                candidate.PoiLatitude,
                candidate.PoiLongitude);

            if (distanceToPoiMeters > candidate.ActivationRadiusMeters)
            {
                _pendingDwellCandidates.Remove(poiId);
                continue;
            }

            var movedDistanceMeters = CalculateDistanceMeters(
                candidate.AnchorLatitude,
                candidate.AnchorLongitude,
                sample.Location.Latitude,
                sample.Location.Longitude);

            var speedMetersPerSecond = ResolveEffectiveSpeedMetersPerSecond(sample, candidate);
            if (speedMetersPerSecond > DwellMaxSpeedMetersPerSecond
                || movedDistanceMeters > DwellMovementToleranceMeters)
            {
                _pendingDwellCandidates[poiId] = candidate with
                {
                    StableSinceUtc = sample.CapturedAtUtc,
                    AnchorLatitude = sample.Location.Latitude,
                    AnchorLongitude = sample.Location.Longitude,
                    IsForeground = sample.IsForeground
                };
                continue;
            }

            if (sample.CapturedAtUtc - candidate.StableSinceUtc < DwellMinimumStayWindow)
            {
                continue;
            }

            dueEvents ??= [];
            var identity = TelemetryAnonymizerService.Instance.CreateIdentitySnapshot();
            dueEvents.Add(new HeatmapEventQueueRecord(
                DeviceHash: identity.DeviceHash,
                SessionHash: identity.SessionHash,
                CapturedAtUtc: sample.CapturedAtUtc,
                Latitude: sample.Location.Latitude,
                Longitude: sample.Location.Longitude,
                AccuracyMeters: sample.Location.Accuracy,
                SpeedMetersPerSecond: sample.Location.Speed,
                BatteryPercent: TryGetBatteryPercent(),
                IsForeground: sample.IsForeground,
                PoiId: candidate.PoiId,
                TourId: candidate.TourId,
                EventType: HeatmapEventTypes.DwellTime,
                Weight: HeatmapEventTypes.ResolveWeight(HeatmapEventTypes.DwellTime),
                TriggerSource: candidate.TriggerSource,
                Context: "poi-dwell-time"));

            _pendingDwellCandidates.Remove(poiId);
        }

        return dueEvents;
    }

    private void TrackBackgroundTask(Task task, string operationName)
    {
        lock (_backgroundTaskGate)
        {
            _backgroundTasks.Add(task);
        }

        _ = task.ContinueWith(
            completedTask =>
            {
                if (completedTask.IsFaulted)
                {
                    var error = completedTask.Exception?.GetBaseException().Message ?? "Unknown error";
                    System.Diagnostics.Debug.WriteLine($"[TelemetryCapture] Background task '{operationName}' failed: {error}");
                }

                lock (_backgroundTaskGate)
                {
                    _backgroundTasks.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task WaitForTrackedBackgroundTasksAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task[] pendingTasks;
        lock (_backgroundTaskGate)
        {
            pendingTasks = _backgroundTasks.Where(task => !task.IsCompleted).ToArray();
        }

        if (pendingTasks.Length == 0)
        {
            return;
        }

        try
        {
            var allTasks = Task.WhenAll(pendingTasks);
            var completedTask = await Task.WhenAny(allTasks, Task.Delay(timeout, cancellationToken));
            if (!ReferenceEquals(completedTask, allTasks))
            {
                System.Diagnostics.Debug.WriteLine("[TelemetryCapture] Timed out waiting for background telemetry tasks.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryCapture] Failed while waiting for background tasks: {ex.Message}");
        }
    }

    private async Task PersistHeatmapEventsAsync(IReadOnlyList<HeatmapEventQueueRecord> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            await MobileDatabaseService.Instance.EnqueueHeatmapEventsAsync(events);
            await TelemetrySyncService.Instance.TriggerSyncAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetryCapture] Failed to enqueue heatmap event(s): {ex.Message}");
        }
    }

    private HeatmapEventQueueRecord? CreatePlaybackHeatmapEvent(ActivePlaybackTelemetryState state, DateTimeOffset playedAtUtc)
    {
        if (!state.PoiId.HasValue || state.PoiId.Value <= 0)
        {
            return null;
        }

        var snapshot = ResolveHeatmapLocationSnapshot(state.PoiId, definition: null, playedAtUtc);
        if (snapshot is null)
        {
            return null;
        }

        return new HeatmapEventQueueRecord(
            DeviceHash: state.DeviceHash,
            SessionHash: state.SessionHash,
            CapturedAtUtc: playedAtUtc,
            Latitude: snapshot.Latitude,
            Longitude: snapshot.Longitude,
            AccuracyMeters: snapshot.AccuracyMeters,
            SpeedMetersPerSecond: snapshot.SpeedMetersPerSecond,
            BatteryPercent: TryGetBatteryPercent(),
            IsForeground: snapshot.IsForeground,
            PoiId: state.PoiId,
            TourId: state.TourId,
            EventType: HeatmapEventTypes.AudioPlay,
            Weight: HeatmapEventTypes.ResolveWeight(HeatmapEventTypes.AudioPlay),
            TriggerSource: string.IsNullOrWhiteSpace(state.TriggerSource) ? "audio-play" : state.TriggerSource,
            Context: state.Context);
    }

    private HeatmapLocationSnapshot? ResolveHeatmapLocationSnapshot(
        int? poiId,
        PoiGeofenceDefinition? definition,
        DateTimeOffset occurredAtUtc)
    {
        if (DeveloperLocationSessionService.Instance.TryGetActiveSession(out var developerSession)
            && developerSession is not null
            && IsFinite(developerSession.Location.Latitude)
            && IsFinite(developerSession.Location.Longitude))
        {
            return new HeatmapLocationSnapshot(
                developerSession.Location.Latitude,
                developerSession.Location.Longitude,
                developerSession.Location.Accuracy,
                developerSession.Location.Speed,
                true);
        }

        lock (_heatmapGate)
        {
            if (_lastLocationSample is { } lastSample
                && occurredAtUtc - lastSample.CapturedAtUtc <= StaleLocationWindow
                && IsFinite(lastSample.Location.Latitude)
                && IsFinite(lastSample.Location.Longitude))
            {
                return new HeatmapLocationSnapshot(
                    lastSample.Location.Latitude,
                    lastSample.Location.Longitude,
                    lastSample.Location.Accuracy,
                    lastSample.Location.Speed,
                    lastSample.IsForeground);
            }
        }

        if (UserLocationService.Instance.LastKnownLocation is { } lastKnownLocation
            && IsFinite(lastKnownLocation.Latitude)
            && IsFinite(lastKnownLocation.Longitude))
        {
            return new HeatmapLocationSnapshot(
                lastKnownLocation.Latitude,
                lastKnownLocation.Longitude,
                lastKnownLocation.Accuracy,
                lastKnownLocation.Speed,
                true);
        }

        if (definition is not null)
        {
            return new HeatmapLocationSnapshot(
                definition.Latitude,
                definition.Longitude,
                null,
                null,
                true);
        }

        if (poiId.HasValue && poiId.Value > 0)
        {
            var place = PlaceCatalogService.Instance.FindById(poiId.Value.ToString(CultureInfo.InvariantCulture));
            if (place is not null && IsFinite(place.Latitude) && IsFinite(place.Longitude))
            {
                return new HeatmapLocationSnapshot(
                    place.Latitude,
                    place.Longitude,
                    null,
                    null,
                    true);
            }
        }

        return null;
    }

    private static double ResolveEffectiveSpeedMetersPerSecond(LocationSample sample, PendingDwellCandidate candidate)
    {
        if (sample.Location.Speed is double speedMetersPerSecond
            && IsFinite(speedMetersPerSecond)
            && speedMetersPerSecond >= 0d)
        {
            return speedMetersPerSecond;
        }

        var elapsedSeconds = Math.Max(1d, (sample.CapturedAtUtc - candidate.StableSinceUtc).TotalSeconds);
        var movedDistanceMeters = CalculateDistanceMeters(
            candidate.AnchorLatitude,
            candidate.AnchorLongitude,
            sample.Location.Latitude,
            sample.Location.Longitude);

        return movedDistanceMeters / elapsedSeconds;
    }

    private static string ResolveHeatmapTriggerSource(GeofenceTriggeredEvent trigger) =>
        trigger.IsNativeTransition ? "native-geofence-enter" : "geofence-enter";

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

    private sealed record PendingDwellCandidate(
        int PoiId,
        int? TourId,
        double PoiLatitude,
        double PoiLongitude,
        double ActivationRadiusMeters,
        DateTimeOffset StableSinceUtc,
        double AnchorLatitude,
        double AnchorLongitude,
        string TriggerSource,
        bool IsForeground);

    private sealed record HeatmapLocationSnapshot(
        double Latitude,
        double Longitude,
        double? AccuracyMeters,
        double? SpeedMetersPerSecond,
        bool IsForeground);
}
