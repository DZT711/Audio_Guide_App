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
            if (!AppSettingsService.Instance.AutoPlayEnabled)
            {
                return;
            }

            var currentTrack = PlaybackCoordinatorService.Instance.CurrentTrack
                ?? PlaybackCoordinatorService.Instance.CurrentQueueItem?.Track;
            var currentPlaceId = currentTrack?.LocationId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(currentPlaceId) &&
                string.Equals(currentPlaceId, trigger.Definition.Id, StringComparison.OrdinalIgnoreCase))
            {
                Log("trigger-ignored", ("reason", "already-playing"), ("poiId", trigger.Definition.Id));
                return;
            }

            var currentPriority = ResolveCurrentPlaybackPriority(currentTrack);
            if (currentTrack is not null && currentPriority > trigger.Definition.Priority)
            {
                Log("trigger-ignored", ("reason", "higher-priority-playing"), ("poiId", trigger.Definition.Id));
                return;
            }

            var place = await EnsurePlaceAsync(trigger.Definition.Id, cancellationToken);
            if (place is null)
            {
                Log("trigger-ignored", ("reason", "place-not-found"), ("poiId", trigger.Definition.Id));
                return;
            }

            using var lookupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lookupCts.CancelAfter(TrackLookupTimeout);

            var sourceTrack = await PlaceCatalogService.Instance.GetDefaultAudioTrackAsync(
                place.Id,
                cancellationToken: lookupCts.Token);

            if (sourceTrack is null && AppDataModeService.Instance.IsApiEnabled)
            {
                sourceTrack = await PlaceCatalogService.Instance.GetDefaultAudioTrackAsync(
                    place.Id,
                    forceRefresh: true,
                    cancellationToken: lookupCts.Token);
            }

            if (sourceTrack is null)
            {
                Log("trigger-ignored", ("reason", "audio-track-not-found"), ("poiId", place.Id));
                return;
            }

            var playableTrack = await AudioDownloadService.Instance.ResolvePlayableTrackAsync(sourceTrack);
            var historyItem = CreateHistoryItem(place, trigger);
            var playbackSource = trigger.EventType == GeofenceTriggerEvent.EnteredRadius ? EnterPlaybackSource : NearPlaybackSource;
            var startedAtUtc = DateTimeOffset.UtcNow;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                HistoryService.Instance.AddToHistory(historyItem);
                await PlaybackCoordinatorService.Instance.PlaySingleAsync(
                    playableTrack,
                    place.Name,
                    $"{sourceTrack.LanguageName ?? sourceTrack.Language} • {playbackSource}");
            });

            if (int.TryParse(place.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId) &&
                !string.IsNullOrWhiteSpace(LocationTrackingService.Instance.DeviceId))
            {
                await MobileDatabaseService.Instance.SetPlaybackCooldownAsync(
                    LocationTrackingService.Instance.DeviceId,
                    locationId,
                    startedAtUtc,
                    startedAtUtc.Add(trigger.CooldownWindow),
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
            Log("playback-trigger-failed", ("poiId", trigger.Definition.Id), ("error", ex.Message));
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

                var cooldownWindow = TimeSpan.FromSeconds(Math.Max(
                    Math.Max(0, definition.DebounceSeconds),
                    Math.Max(1, (int)Math.Round(_engineOptions.DefaultPoiCooldown.TotalSeconds))));

                if (runtimeState.CooldownUntilUtc.HasValue && runtimeState.CooldownUntilUtc.Value > transition.OccurredAtUtc)
                {
                    Log("native-enter-skipped", ("poiId", definition.Id), ("reason", "poi-cooldown"));
                    return;
                }

                runtimeState.LastEnteredAtUtc = transition.OccurredAtUtc;
                runtimeState.LastTriggeredAtUtc = transition.OccurredAtUtc;
                runtimeState.CooldownUntilUtc = transition.OccurredAtUtc.Add(cooldownWindow);
                runtimeState.SetTriggerTime(GeofenceTriggerEvent.EnteredRadius, transition.OccurredAtUtc);
                runtimeState.State = GeofenceState.Inside;
                _globalCooldownUntilUtc = transition.OccurredAtUtc.Add(_engineOptions.GlobalCooldown);

                acceptedTrigger = new GeofenceTriggeredEvent(
                    definition,
                    GeofenceTriggerEvent.EnteredRadius,
                    runtimeState.LastDistanceMeters ?? double.MaxValue,
                    cooldownWindow,
                    IsNativeTransition: true);
            }

            if (acceptedTrigger is not null && ShouldAllowTrigger(acceptedTrigger))
            {
                await HandleTriggerAsync(acceptedTrigger, cancellationToken);
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
