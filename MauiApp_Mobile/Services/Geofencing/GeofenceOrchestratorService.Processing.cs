using System.ComponentModel;
using System.Diagnostics;
using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofenceOrchestratorService
{
    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        if (_locationChannel is null)
        {
            return;
        }

        try
        {
            while (await _locationChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                if (!_locationChannel.Reader.TryRead(out var latestSample))
                {
                    continue;
                }

                var collapsedCount = 0;
                while (_locationChannel.Reader.TryRead(out var newerSample))
                {
                    latestSample = newerSample;
                    collapsedCount++;
                }

                if (collapsedCount > 0)
                {
                    Interlocked.Add(ref _droppedLocationEvents, collapsedCount);
                }

                AdjustQueueDepth(-(collapsedCount + 1));
                _lastProcessorHeartbeatUtc = DateTimeOffset.UtcNow;

                try
                {
                    await ProcessLocationSampleAsync(latestSample, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log("processing-failed", ("error", ex.Message));
                    SetRunState(GeofenceRunState.Faulted, "Automatic audio is recovering after a background error.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task WatchdogLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var stalled =
                    _lastEnqueuedAtUtc.HasValue &&
                    nowUtc - _lastProcessorHeartbeatUtc > _engineOptions.WatchdogThreshold &&
                    Volatile.Read(ref _queueDepth) > 0;

                if (stalled)
                {
                    Log("watchdog-restart", ("queueDepth", Volatile.Read(ref _queueDepth)));
                    await RestartBackgroundLoopsAsync(cancellationToken);
                    continue;
                }

                if (_nativeCircuitBrokenUntilUtc.HasValue && nowUtc >= _nativeCircuitBrokenUntilUtc.Value)
                {
                    _nativeCircuitBrokenUntilUtc = null;
                    _ = RefreshCatalogAndRegistrationsAsync("native-circuit-retry", cancellationToken);
                }

                if (_processingLoopTask is { IsCompleted: true })
                {
                    Log("processor-restart", ("status", _processingLoopTask.Status.ToString()));
                    await RestartBackgroundLoopsAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log("watchdog-failed", ("error", ex.Message));
            }
        }
    }

    private async Task RefreshCatalogAndRegistrationsAsync(string reason, CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return;
        }

        await _catalogRefreshSemaphore.WaitAsync(cancellationToken);
        try
        {
            RefreshPerformanceTier();
            await PlaceCatalogService.Instance.EnsureLoadedAsync(false, cancellationToken);

            var definitions = PlaceCatalogService.Instance.GetGeofenceDefinitions();
            var cooldowns = !string.IsNullOrWhiteSpace(LocationTrackingService.Instance.DeviceId)
                ? await MobileDatabaseService.Instance.GetPlaybackCooldownsAsync(LocationTrackingService.Instance.DeviceId, cancellationToken)
                : new Dictionary<int, DateTimeOffset>();

            lock (_stateGate)
            {
                _definitions = definitions.ToList();
                _engineOptions = CreateEngineOptions(PerformanceTier);
                _evaluationEngine = new GeofenceEvaluationEngine(_engineOptions);
                _spatialIndex = GeofenceSpatialIndex.Build(_definitions, _engineOptions);

                var validIds = new HashSet<string>(_definitions.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);
                foreach (var stalePoiId in _runtimeStates.Keys.Where(item => !validIds.Contains(item)).ToList())
                {
                    _runtimeStates.Remove(stalePoiId);
                }

                foreach (var definition in _definitions)
                {
                    var runtimeState = GetOrCreateRuntimeState(definition.Id);
                    if (int.TryParse(definition.Id, out var locationId) && cooldowns.TryGetValue(locationId, out var cooldownUntilUtc))
                    {
                        runtimeState.CooldownUntilUtc = cooldownUntilUtc > DateTimeOffset.UtcNow ? cooldownUntilUtc : null;
                    }
                }
            }

            await RefreshNativeRegistrationsAsync(reason, cancellationToken);

            if (_definitions.Count == 0)
            {
                SetRunState(GeofenceRunState.FallbackOnly, "No active GPS POIs are available for automatic playback.");
            }
            else if (RunState is GeofenceRunState.Starting or GeofenceRunState.Stopped)
            {
                SetRunState(GeofenceRunState.Running, "Automatic audio guidance is watching nearby POIs.");
            }

            Log("catalog-refreshed", ("reason", reason), ("geofenceCount", _definitions.Count), ("tier", PerformanceTier.ToString()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log("catalog-refresh-failed", ("reason", reason), ("error", ex.Message));
            SetRunState(GeofenceRunState.Faulted, "Automatic audio is running in safe fallback mode.");
        }
        finally
        {
            _catalogRefreshSemaphore.Release();
        }
    }

    private async Task RefreshNativeRegistrationsAsync(string reason, CancellationToken cancellationToken)
    {
        try
        {
            if (_definitions.Count == 0)
            {
                await SafeUnregisterNativeAsync(cancellationToken);
                return;
            }

            var foregroundPermission = await LocationTrackingService.Instance.GetForegroundPermissionStatusAsync();
            if (foregroundPermission != PermissionStatus.Granted)
            {
                await SafeUnregisterNativeAsync(cancellationToken);
                SetRunState(GeofenceRunState.PermissionDenied, "Location permission is needed for automatic POI playback.");
                return;
            }

            if (!AppSettingsService.Instance.BackgroundTrackingEnabled)
            {
                await SafeUnregisterNativeAsync(cancellationToken);
                SetRunState(GeofenceRunState.FallbackOnly, "Distance fallback is active. Enable background tracking for native regions.");
                return;
            }

            var backgroundPermission = await LocationTrackingService.Instance.GetBackgroundPermissionStatusAsync();
            if (backgroundPermission != PermissionStatus.Granted)
            {
                await SafeUnregisterNativeAsync(cancellationToken);
                SetRunState(GeofenceRunState.FallbackOnly, "Distance fallback is active until background location is granted.");
                return;
            }

            if (!GeofencePlatformMonitor.Instance.IsSupported)
            {
                SetRunState(GeofenceRunState.FallbackOnly, "Native region monitoring is unavailable. Using distance fallback.");
                return;
            }

            if (_nativeCircuitBrokenUntilUtc.HasValue && _nativeCircuitBrokenUntilUtc.Value > DateTimeOffset.UtcNow)
            {
                SetRunState(GeofenceRunState.FallbackOnly, "Native region monitoring is cooling down. Using distance fallback.");
                return;
            }

            var registrationResult = await GeofencePlatformMonitor.Instance.RegisterAsync(SelectNativeDefinitions(_definitions), cancellationToken);
            if (registrationResult.IsSuccessful)
            {
                _nativeFailureCount = 0;
                _nativeCircuitBrokenUntilUtc = null;
                SetRunState(GeofenceRunState.Running, "Automatic audio guidance is watching nearby POIs.");
                Log("native-register-success", ("reason", reason), ("count", registrationResult.RegisteredCount), ("mode", registrationResult.Mode));
                return;
            }

            _nativeFailureCount++;
            SetRunState(GeofenceRunState.FallbackOnly, "Native region monitoring is unavailable right now. Using distance fallback.");
            Log("native-register-failed", ("reason", reason), ("failure", registrationResult.FailureReason ?? string.Empty), ("count", _nativeFailureCount));

            if (_nativeFailureCount >= _engineOptions.NativeFailureThreshold)
            {
                _nativeCircuitBrokenUntilUtc = DateTimeOffset.UtcNow.Add(_engineOptions.NativeCircuitBreakerDuration);
                SetRunState(GeofenceRunState.FallbackOnly, "Native region monitoring is unstable. Using distance fallback.");
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _nativeFailureCount++;
            SetRunState(GeofenceRunState.FallbackOnly, "Native region monitoring is unavailable right now. Using distance fallback.");
            Log("native-register-exception", ("reason", reason), ("error", ex.Message));

            if (_nativeFailureCount >= _engineOptions.NativeFailureThreshold)
            {
                _nativeCircuitBrokenUntilUtc = DateTimeOffset.UtcNow.Add(_engineOptions.NativeCircuitBreakerDuration);
                SetRunState(GeofenceRunState.FallbackOnly, "Native region monitoring is unstable. Using distance fallback.");
            }
        }
    }

    private async Task ProcessLocationSampleAsync(GeofenceLocationSample sample, CancellationToken cancellationToken)
    {
        if (!IsFinite(sample.Latitude) || !IsFinite(sample.Longitude) || !ShouldEvaluateSample(sample))
        {
            return;
        }

        GeofenceEvaluationResult evaluationResult;
        var stopwatch = Stopwatch.StartNew();

        lock (_stateGate)
        {
            evaluationResult = _evaluationEngine.Evaluate(
                sample,
                _spatialIndex,
                _runtimeStates,
                AppSettingsService.Instance.AutoPlayEnabled ? _globalCooldownUntilUtc : DateTimeOffset.MaxValue);

            _lastProcessedSample = sample;
            _lastProcessedAtUtc = sample.CapturedAtUtc;
        }

        stopwatch.Stop();

        foreach (var skipped in evaluationResult.SkippedTriggers.Take(4))
        {
            LogSkip(
                DescribeSkipReason(skipped.Reason),
                ("poiId", skipped.Definition.Id),
                ("eventType", skipped.EventType.ToString()),
                ("distanceMeters", Math.Round(skipped.DistanceMeters, 2)));
        }

        UpdateDebugSnapshot(
            evaluationResult.NearestPoiId,
            evaluationResult.NearestDistanceMeters,
            evaluationResult.CandidateCount,
            stopwatch.Elapsed,
            sample.CapturedAtUtc,
            string.Empty);

        var allowedTriggers = new List<GeofenceTriggeredEvent>(evaluationResult.AcceptedTriggers.Count);
        foreach (var acceptedTrigger in evaluationResult.AcceptedTriggers)
        {
            if (ShouldAllowTrigger(acceptedTrigger))
            {
                allowedTriggers.Add(acceptedTrigger);
                continue;
            }

            LogSkip(
                "Near notification disabled",
                ("poiId", acceptedTrigger.Definition.Id),
                ("eventType", acceptedTrigger.EventType.ToString()),
                ("distanceMeters", Math.Round(acceptedTrigger.DistanceMeters, 2)));
        }

        var selectedTrigger = GeofenceTriggerSelector.SelectBest(allowedTriggers);

        if (selectedTrigger is null)
        {
            if (RunState == GeofenceRunState.Starting)
            {
                SetRunState(GeofenceRunState.Running, "Automatic audio guidance is watching nearby POIs.");
            }

            return;
        }

        if (allowedTriggers.Count > 1)
        {
            foreach (var skippedTrigger in allowedTriggers.Where(item => !Equals(item, selectedTrigger)))
            {
                LogSkip(
                    "Lower Priority",
                    ("poiId", skippedTrigger.Definition.Id),
                    ("eventType", skippedTrigger.EventType.ToString()),
                    ("selectedPoiId", selectedTrigger.Definition.Id),
                    ("selectedPriority", selectedTrigger.Definition.Priority),
                    ("skippedPriority", skippedTrigger.Definition.Priority));
            }

            Log("overlap-resolved", ("selectedPoiId", selectedTrigger.Definition.Id), ("acceptedCount", allowedTriggers.Count));
        }

        await HandleTriggerAsync(selectedTrigger, cancellationToken);
    }

    private void OnTrackedLocationUpdated(object? sender, LocationSample sample)
    {
        try
        {
            if (sample.Location is null)
            {
                LogSkip("Missing tracked location", ("source", nameof(LocationTrackingService)));
                return;
            }

            EnqueueLocation(new GeofenceLocationSample(
                sample.Location.Latitude,
                sample.Location.Longitude,
                sample.Location.Accuracy,
                sample.Location.Speed,
                sample.CapturedAtUtc,
                sample.IsForeground));
        }
        catch (Exception ex)
        {
            Log("tracked-location-update-failed", ("error", ex.Message));
        }
    }

    private void OnUserLocationUpdated(object? sender, Location? location)
    {
        try
        {
            if (location is null)
            {
                LogSkip("Missing user location", ("source", nameof(UserLocationService)));
                return;
            }

            EnqueueLocation(new GeofenceLocationSample(
                location.Latitude,
                location.Longitude,
                location.Accuracy,
                location.Speed,
                DateTimeOffset.UtcNow,
                true));
        }
        catch (Exception ex)
        {
            Log("user-location-update-failed", ("error", ex.Message));
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e) =>
        _ = RefreshCatalogAndRegistrationsAsync("catalog-changed", _engineCts?.Token ?? CancellationToken.None);

    private void OnAppSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ShouldRefreshForSetting(e.PropertyName))
        {
            return;
        }

        _ = RefreshCatalogAndRegistrationsAsync("settings-changed", _engineCts?.Token ?? CancellationToken.None);
    }

    private void EnqueueLocation(GeofenceLocationSample sample)
    {
        if (_isDisposed || _locationChannel is null)
        {
            return;
        }

        _lastEnqueuedAtUtc = DateTimeOffset.UtcNow;
        if (_locationChannel.Writer.TryWrite(sample))
        {
            AdjustQueueDepth(1);
            return;
        }

        var dropped = 0;
        while (_locationChannel.Reader.TryRead(out _))
        {
            dropped++;
        }

        if (dropped > 0)
        {
            Interlocked.Add(ref _droppedLocationEvents, dropped);
            AdjustQueueDepth(-dropped);
        }

        if (_locationChannel.Writer.TryWrite(sample))
        {
            AdjustQueueDepth(1);
            return;
        }

        Interlocked.Increment(ref _droppedLocationEvents);
    }

    public GeofenceProcessingHealthCheck GetProcessingHealthCheck()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var queueDepth = Math.Max(0, Volatile.Read(ref _queueDepth));
        var hasPendingWork = queueDepth > 0;
        var processorRunning = _processingLoopTask is { IsCompleted: false };
        var watchdogRunning = _watchdogLoopTask is { IsCompleted: false };
        var heartbeatFresh = nowUtc - _lastProcessorHeartbeatUtc <= _engineOptions.WatchdogThreshold;

        var isHealthy =
            !_isDisposed &&
            _engineCts is not null &&
            _locationChannel is not null &&
            processorRunning &&
            watchdogRunning &&
            (!hasPendingWork || heartbeatFresh) &&
            RunState != GeofenceRunState.Faulted;

        var status = isHealthy
            ? "healthy"
            : !processorRunning
                ? "processing-loop-stopped"
                : !watchdogRunning
                    ? "watchdog-stopped"
                    : hasPendingWork && !heartbeatFresh
                        ? "processing-loop-stalled"
                        : RunState == GeofenceRunState.Faulted
                            ? "faulted"
                            : _engineCts is null || _locationChannel is null
                                ? "not-started"
                                : "degraded";

        return new GeofenceProcessingHealthCheck(
            isHealthy,
            status,
            RunState,
            _lastProcessorHeartbeatUtc,
            _lastProcessedAtUtc,
            _lastEnqueuedAtUtc,
            queueDepth);
    }

    private bool ShouldEvaluateSample(GeofenceLocationSample sample)
    {
        if (!_lastProcessedAtUtc.HasValue || !_lastProcessedSample.HasValue)
        {
            return true;
        }

        var elapsed = sample.CapturedAtUtc - _lastProcessedAtUtc.Value;
        if (elapsed >= _engineOptions.MinimumEvaluationInterval)
        {
            return true;
        }

        var movedDistanceMeters = HaversineDistanceCalculator.CalculateMeters(
            _lastProcessedSample.Value.Latitude,
            _lastProcessedSample.Value.Longitude,
            sample.Latitude,
            sample.Longitude);

        return movedDistanceMeters >= _engineOptions.MinimumMovementMeters;
    }

    private void AdjustQueueDepth(int delta)
    {
        var updated = Interlocked.Add(ref _queueDepth, delta);
        if (updated < 0)
        {
            Interlocked.Exchange(ref _queueDepth, 0);
        }
    }

    private static GeofenceEngineOptions CreateEngineOptions(GeofencePerformanceTier tier)
    {
        var defaults = GeofenceEngineOptions.Create(tier);
        var configuredCooldown = TimeSpan.FromSeconds(Math.Max(0d, AppSettingsService.Instance.WaitTimeSeconds));

        return configuredCooldown > TimeSpan.Zero
            ? defaults with
            {
                DefaultPoiCooldown = configuredCooldown,
                GlobalCooldown = configuredCooldown
            }
            : defaults;
    }

    private static string DescribeSkipReason(string reason) =>
        reason switch
        {
            "global-cooldown" => "In Cooldown",
            "poi-cooldown" => "In Cooldown",
            "duplicate-event" => "Duplicate Trigger",
            _ => string.IsNullOrWhiteSpace(reason) ? "Skipped" : reason
        };

    private static bool ShouldRefreshForSetting(string? propertyName) =>
        string.Equals(propertyName, nameof(AppSettingsService.BatterySaverEnabled), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.BackgroundTrackingEnabled), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.AutoPlayEnabled), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.NotifyNearEnabled), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.TriggerRadiusMeters), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.AlertRadiusMeters), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.WaitTimeSeconds), StringComparison.Ordinal) ||
        string.Equals(propertyName, nameof(AppSettingsService.GpsAccuracy), StringComparison.Ordinal);
}
