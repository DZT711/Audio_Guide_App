using System.Diagnostics;
using System.Globalization;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofenceOrchestratorService
{
    private const string EnterPlaybackSource = "geofence-enter";
    private const string NearPlaybackSource = "geofence-near";
    private static readonly TimeSpan TrackLookupTimeout = TimeSpan.FromSeconds(12);

    private async Task HandleTriggerAsync(GeofenceTriggeredEvent trigger, CancellationToken cancellationToken)
    {
        await _playbackSingleFlight.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trigger.Definition is null || string.IsNullOrWhiteSpace(trigger.Definition.Id))
            {
                LogSkip("Missing POI definition", ("eventType", trigger.EventType));
                return;
            }

            if (!AppSettingsService.Instance.AutoPlayEnabled)
            {
                LogSkip("Autoplay disabled", ("poiId", trigger.Definition.Id), ("eventType", trigger.EventType));
                return;
            }

            var currentTrack = PlaybackCoordinatorService.Instance.CurrentTrack
                ?? PlaybackCoordinatorService.Instance.CurrentQueueItem?.Track;
            var currentPlaceId = currentTrack?.LocationId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(currentPlaceId) &&
                string.Equals(currentPlaceId, trigger.Definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                LogSkip("Already playing", ("poiId", trigger.Definition.Id), ("eventType", trigger.EventType));
                return;
            }

            if (HasBlockingRuntimeCooldown(trigger, out var runtimeCooldownUntilUtc))
            {
                LogSkip("In Cooldown", ("poiId", trigger.Definition.Id), ("untilUtc", runtimeCooldownUntilUtc), ("source", "runtime"));
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var persistedCooldownUntilUtc = await GetPersistedCooldownUntilAsync(trigger.Definition.Id, cancellationToken);
            if (persistedCooldownUntilUtc.HasValue && persistedCooldownUntilUtc.Value > DateTimeOffset.UtcNow)
            {
                lock (_stateGate)
                {
                    var runtimeState = GetOrCreateRuntimeState(trigger.Definition.Id);
                    runtimeState.CooldownUntilUtc = persistedCooldownUntilUtc;
                }

                LogSkip("In Cooldown", ("poiId", trigger.Definition.Id), ("untilUtc", persistedCooldownUntilUtc.Value), ("source", "sqlite"));
                return;
            }

            var currentPriority = ResolveCurrentPlaybackPriority(currentTrack);
            if (currentTrack is not null && currentPriority > trigger.Definition.Priority)
            {
                LogSkip("Lower Priority", ("poiId", trigger.Definition.Id), ("currentPriority", currentPriority), ("requestedPriority", trigger.Definition.Priority));
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var place = await EnsurePlaceAsync(trigger.Definition.Id, cancellationToken);
            if (place is null)
            {
                LogSkip("Missing POI definition", ("poiId", trigger.Definition.Id), ("eventType", trigger.EventType));
                return;
            }

            using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lookupCts.CancelAfter(TrackLookupTimeout);

            lookupCts.Token.ThrowIfCancellationRequested();
            var audioTracks = await LoadAudioTracksAsync(place.Id, lookupCts.Token);
            lookupCts.Token.ThrowIfCancellationRequested();
            if (audioTracks is null || audioTracks.Count == 0)
            {
                LogSkip("No audio track", ("poiId", place.Id), ("eventType", trigger.EventType));
                return;
            }

            var sourceTrack = SelectDefaultTrack(audioTracks);
            if (sourceTrack is null)
            {
                LogSkip("No audio track", ("poiId", place.Id), ("eventType", trigger.EventType));
                return;
            }

            var queueItems = await BuildPlaybackQueueItemsAsync(place, audioTracks, sourceTrack.Id, lookupCts.Token);
            if (queueItems.Count == 0)
            {
                LogSkip("Audio queue empty", ("poiId", place.Id), ("eventType", trigger.EventType));
                return;
            }

            var historyItem = CreateHistoryItem(place, trigger);
            var playbackSource = trigger.EventType == GeofenceTriggerEvent.EnteredRadius ? EnterPlaybackSource : NearPlaybackSource;
            var startedAtUtc = DateTimeOffset.UtcNow;
            var queueStartIndex = FindQueueIndex(queueItems, sourceTrack.Id);
            var cooldownUntilUtc = startedAtUtc.Add(trigger.CooldownWindow);

            cancellationToken.ThrowIfCancellationRequested();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HistoryService.Instance.AddToHistory(historyItem, cancellationToken);
                _ = FireAndForgetPlaybackAsync(queueItems, queueStartIndex, cancellationToken);
                return Task.CompletedTask;
            });

            lock (_stateGate)
            {
                var runtimeState = GetOrCreateRuntimeState(place.Id);
                runtimeState.LastTriggeredAtUtc = startedAtUtc;
                runtimeState.CooldownUntilUtc = cooldownUntilUtc;
                runtimeState.SetTriggerTime(trigger.EventType, trigger.OccurredAtUtc);
                _globalCooldownUntilUtc = startedAtUtc.Add(_engineOptions.GlobalCooldown);
            }

            if (int.TryParse(place.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId) &&
                !string.IsNullOrWhiteSpace(LocationTrackingService.Instance.DeviceId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await MobileDatabaseService.Instance.SetPlaybackCooldownAsync(
                    LocationTrackingService.Instance.DeviceId,
                    locationId,
                    startedAtUtc,
                    cooldownUntilUtc,
                    cancellationToken);
            }

            UpdateDebugSnapshot(
                DebugSnapshot.NearestPoiId,
                DebugSnapshot.NearestDistanceMeters,
                DebugSnapshot.CandidateCount,
                DebugSnapshot.LastEvaluationDuration,
                DebugSnapshot.LastProcessedAtUtc,
                $"{playbackSource}:{place.Id}");

            Log("playback-triggered", ("poiId", place.Id), ("eventType", trigger.EventType.ToString()), ("priority", trigger.Definition.Priority));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log("playback-trigger-failed", ("poiId", trigger.Definition?.Id ?? string.Empty), ("error", ex.Message));
        }
        finally
        {
            _playbackSingleFlight.Release();
        }
    }

    private async Task HandleNativeTransitionAsync(NativeGeofenceTransition transition, CancellationToken cancellationToken)
    {
        try
        {
            GeofenceTriggeredEvent? acceptedTrigger = null;

            lock (_stateGate)
            {
                if (!_spatialIndex.TryGetDefinition(transition.PoiId, out var definition))
                {
                    return;
                }

                var runtimeState = GetOrCreateRuntimeState(definition.Id);
                runtimeState.HasEstablishedState = true;

                if (transition.EventType == GeofenceTriggerEvent.ExitedRadius)
                {
                    runtimeState.LastExitedAtUtc = transition.OccurredAtUtc;
                    runtimeState.ResetNearWindow();
                    runtimeState.State = GeofenceState.Outside;
                    Log("native-exit", ("poiId", definition.Id));
                    return;
                }

                var cooldownWindow = definition.DebounceSeconds > 0
                    ? TimeSpan.FromSeconds(definition.DebounceSeconds)
                    : _engineOptions.DefaultPoiCooldown > TimeSpan.Zero
                        ? _engineOptions.DefaultPoiCooldown
                        : TimeSpan.FromSeconds(1);

                if (runtimeState.CooldownUntilUtc.HasValue && runtimeState.CooldownUntilUtc.Value > transition.OccurredAtUtc)
                {
                    LogSkip("In Cooldown", ("poiId", definition.Id), ("source", "native-transition"));
                    return;
                }

                runtimeState.LastEnteredAtUtc = transition.OccurredAtUtc;
                runtimeState.State = GeofenceState.Inside;

                acceptedTrigger = new GeofenceTriggeredEvent(
                    definition,
                    GeofenceTriggerEvent.EnteredRadius,
                    runtimeState.LastDistanceMeters ?? double.MaxValue,
                    transition.OccurredAtUtc,
                    cooldownWindow,
                    IsNativeTransition: true);
            }

            if (acceptedTrigger is not null && ShouldAllowTrigger(acceptedTrigger))
            {
                await HandleTriggerAsync(acceptedTrigger, cancellationToken);
            }
            else if (acceptedTrigger is not null)
            {
                LogSkip("Near notification disabled", ("poiId", acceptedTrigger.Definition.Id), ("eventType", acceptedTrigger.EventType));
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log("native-transition-failed", ("poiId", transition.PoiId), ("error", ex.Message));
        }
    }

    private void OnNativeTransitionReceived(object? sender, NativeGeofenceTransition transition) =>
        _ = HandleNativeTransitionAsync(transition, _engineCts?.Token ?? CancellationToken.None);

    private static bool ShouldAllowTrigger(GeofenceTriggeredEvent trigger) =>
        trigger.EventType != GeofenceTriggerEvent.NearStay || AppSettingsService.Instance.NotifyNearEnabled;

    private static IReadOnlyList<PoiGeofenceDefinition> SelectNativeDefinitions(IReadOnlyList<PoiGeofenceDefinition> definitions)
    {
        var maxRegions = DeviceInfo.Platform == DevicePlatform.iOS ? 20 : 100;

        return definitions
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.ActivationRadiusMeters)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Take(maxRegions)
            .ToList();
    }

    private static int ResolveCurrentPlaybackPriority(PublicAudioTrackDto? currentTrack)
    {
        if (currentTrack is null)
        {
            return int.MinValue;
        }

        var placeId = currentTrack.LocationId.ToString(CultureInfo.InvariantCulture);
        return PlaceCatalogService.Instance.FindById(placeId)?.Priority ?? 0;
    }

    private async Task<PlaceItem?> EnsurePlaceAsync(string placeId, CancellationToken cancellationToken)
    {
        var place = PlaceCatalogService.Instance.FindById(placeId);
        if (place is not null)
        {
            return place;
        }

        await PlaceCatalogService.Instance.EnsureLoadedAsync(false, cancellationToken);
        return PlaceCatalogService.Instance.FindById(placeId);
    }

    private static async Task<IReadOnlyList<PublicAudioTrackDto>> LoadAudioTracksAsync(
        string placeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var audioTracks = await PlaceCatalogService.Instance.GetAudioTracksAsync(
            placeId,
            cancellationToken: cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        if (audioTracks.Count == 0 && AppDataModeService.Instance.IsApiEnabled)
        {
            audioTracks = await PlaceCatalogService.Instance.GetAudioTracksAsync(
                placeId,
                forceRefresh: true,
                cancellationToken: cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return audioTracks;
    }

    private static PublicAudioTrackDto? SelectDefaultTrack(IReadOnlyList<PublicAudioTrackDto> audioTracks) =>
        audioTracks.FirstOrDefault(item => item.IsDefault)
        ?? audioTracks
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.Id)
            .FirstOrDefault();

    private static async Task<IReadOnlyList<PlaybackQueueItem>> BuildPlaybackQueueItemsAsync(
        PlaceItem place,
        IReadOnlyList<PublicAudioTrackDto> audioTracks,
        int preferredTrackId,
        CancellationToken cancellationToken)
    {
        var orderedTracks = audioTracks
            .OrderByDescending(item => item.Id == preferredTrackId)
            .ThenByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.Id)
            .ToList();

        var queueItems = new List<PlaybackQueueItem>(orderedTracks.Count);
        foreach (var sourceTrack in orderedTracks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sourceTrack is null)
            {
                continue;
            }

            var playableTrack = await AudioDownloadService.Instance.ResolvePlayableTrackAsync(sourceTrack, cancellationToken);
            queueItems.Add(new PlaybackQueueItem(
                playableTrack,
                place.Name,
                $"{sourceTrack.LanguageName ?? sourceTrack.Language} • {sourceTrack.SourceType}"));
        }

        return queueItems;
    }

    private static int FindQueueIndex(IReadOnlyList<PlaybackQueueItem> queueItems, int trackId)
    {
        for (var index = 0; index < queueItems.Count; index++)
        {
            if (queueItems[index].Track.Id == trackId)
            {
                return index;
            }
        }

        return 0;
    }

    private static PlaceItem CreateHistoryItem(PlaceItem place, GeofenceTriggeredEvent trigger)
    {
        var playbackSource = trigger.EventType == GeofenceTriggerEvent.EnteredRadius ? EnterPlaybackSource : NearPlaybackSource;

        return new PlaceItem
        {
            Id = place.Id,
            Name = place.Name,
            Description = place.Description,
            AudioDescription = place.AudioDescription,
            Category = place.Category,
            Rating = place.Rating,
            Image = place.Image,
            PreferenceImage = place.PreferenceImage,
            GalleryImages = place.GalleryImages.ToList(),
            Address = place.Address,
            Phone = place.Phone,
            Email = place.Email,
            Website = place.Website,
            EstablishedYear = place.EstablishedYear,
            RadiusText = place.RadiusText,
            StandbyRadiusText = place.StandbyRadiusText,
            GpsText = place.GpsText,
            PriorityText = place.PriorityText,
            DebounceText = place.DebounceText,
            OwnerName = place.OwnerName,
            StatusText = place.StatusText,
            GpsTriggerText = place.GpsTriggerText,
            AudioCountText = place.AudioCountText,
            AudioTracks = place.AudioTracks.ToList(),
            AvailableVoiceGenders = place.AvailableVoiceGenders.ToList(),
            LanguageBadgeSummaryText = place.LanguageBadgeSummaryText,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            ActivationRadiusMeters = place.ActivationRadiusMeters,
            NearRadiusMeters = place.NearRadiusMeters,
            Priority = place.Priority,
            DebounceSeconds = place.DebounceSeconds,
            Status = place.Status,
            IsGpsTriggerEnabled = place.IsGpsTriggerEnabled,
            LastPlaybackSource = playbackSource,
            LastPlaybackTrigger = trigger.EventType.ToString(),
            LastPlaybackTriggeredAt = DateTimeOffset.UtcNow,
            LastTriggerDistanceMeters = trigger.DistanceMeters,
            CategoryColor = place.CategoryColor,
            CategoryTextColor = place.CategoryTextColor,
            HistoryAddedAt = DateTimeOffset.Now
        };
    }

    private bool HasBlockingRuntimeCooldown(GeofenceTriggeredEvent trigger, out DateTimeOffset? cooldownUntilUtc)
    {
        lock (_stateGate)
        {
            var runtimeState = GetOrCreateRuntimeState(trigger.Definition.Id);
            cooldownUntilUtc = runtimeState.CooldownUntilUtc;
            if (!cooldownUntilUtc.HasValue || cooldownUntilUtc.Value <= DateTimeOffset.UtcNow)
            {
                return false;
            }

            return !runtimeState.TryGetLastTriggerTime(trigger.EventType, out var lastTriggerAtUtc)
                || lastTriggerAtUtc != trigger.OccurredAtUtc;
        }
    }

    private static async Task FireAndForgetPlaybackAsync(
        IReadOnlyList<PlaybackQueueItem> queueItems,
        int queueStartIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            await PlaybackCoordinatorService.Instance.PlayQueueAsync(queueItems, queueStartIndex, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log("playback-start-failed", ("error", ex.Message));
        }
    }

    private static async Task<DateTimeOffset?> GetPersistedCooldownUntilAsync(string poiId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(poiId) ||
            string.IsNullOrWhiteSpace(LocationTrackingService.Instance.DeviceId) ||
            !int.TryParse(poiId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await MobileDatabaseService.Instance.GetPlaybackCooldownUntilAsync(
            LocationTrackingService.Instance.DeviceId,
            locationId,
            cancellationToken);
    }

    private GeofencePoiRuntimeState GetOrCreateRuntimeState(string poiId)
    {
        if (_runtimeStates.TryGetValue(poiId, out var runtimeState))
        {
            return runtimeState;
        }

        runtimeState = new GeofencePoiRuntimeState();
        _runtimeStates[poiId] = runtimeState;
        return runtimeState;
    }

    private static async Task AwaitSilentlyAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static int ResolveSourceTypeOrder(string? sourceType) =>
        sourceType?.Trim().ToUpperInvariant() switch
        {
            "RECORDED" => 0,
            "HYBRID" => 1,
            _ => 2
        };

    private static void LogSkip(string reason, params (string Key, object? Value)[] properties) =>
        Log($"Skipped: {reason}", properties);

    private static void Log(string eventName, params (string Key, object? Value)[] properties)
    {
        var payload = string.Join(", ", properties.Select(item => $"{item.Key}={item.Value}"));
        Debug.WriteLine($"[Geofence] {eventName} | {payload}");
    }

    private async Task SafeUnregisterNativeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GeofencePlatformMonitor.Instance.UnregisterAllAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log("native-unregister-failed", ("error", ex.Message));
        }
    }
}
