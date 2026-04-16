using System.Collections.ObjectModel;

namespace Project_SharedClassLibrary.Geofencing;

public enum GeofenceState
{
    Outside,
    Inside,
    Near,
    Cooldown
}

public enum GeofenceTriggerEvent
{
    EnteredRadius,
    NearStay,
    ExitedRadius
}

public enum GeofencePerformanceTier
{
    Normal,
    BatterySaver
}

public sealed record PoiGeofenceDefinition(
    string Id,
    double Latitude,
    double Longitude,
    double ActivationRadiusMeters,
    double NearRadiusMeters,
    int Priority,
    int DebounceSeconds,
    bool IsGpsTriggerEnabled);

public readonly record struct GeofenceLocationSample(
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    double? SpeedMetersPerSecond,
    DateTimeOffset CapturedAtUtc,
    bool IsForeground);

public sealed record GeofenceEngineOptions(
    TimeSpan DefaultPoiCooldown,
    TimeSpan GlobalCooldown,
    TimeSpan NearDwellWindow,
    TimeSpan MinimumEvaluationInterval,
    double MinimumMovementMeters,
    double SpatialPaddingMeters,
    int CandidateLimit,
    TimeSpan NativeCircuitBreakerDuration,
    int NativeFailureThreshold,
    TimeSpan WatchdogThreshold)
{
    public static GeofenceEngineOptions Create(GeofencePerformanceTier tier) =>
        tier == GeofencePerformanceTier.BatterySaver
            ? new GeofenceEngineOptions(
                DefaultPoiCooldown: TimeSpan.FromSeconds(45),
                GlobalCooldown: TimeSpan.FromSeconds(10),
                NearDwellWindow: TimeSpan.FromSeconds(20),
                MinimumEvaluationInterval: TimeSpan.FromSeconds(12),
                MinimumMovementMeters: 22d,
                SpatialPaddingMeters: 60d,
                CandidateLimit: 18,
                NativeCircuitBreakerDuration: TimeSpan.FromMinutes(15),
                NativeFailureThreshold: 3,
                WatchdogThreshold: TimeSpan.FromMinutes(2))
            : new GeofenceEngineOptions(
                DefaultPoiCooldown: TimeSpan.FromSeconds(30),
                GlobalCooldown: TimeSpan.FromSeconds(6),
                NearDwellWindow: TimeSpan.FromSeconds(12),
                MinimumEvaluationInterval: TimeSpan.FromSeconds(5),
                MinimumMovementMeters: 8d,
                SpatialPaddingMeters: 40d,
                CandidateLimit: 36,
                NativeCircuitBreakerDuration: TimeSpan.FromMinutes(10),
                NativeFailureThreshold: 3,
                WatchdogThreshold: TimeSpan.FromSeconds(90));
}

public sealed class GeofencePoiRuntimeState
{
    private readonly Dictionary<GeofenceTriggerEvent, DateTimeOffset> _lastTriggerTimes = new();

    public GeofenceState State { get; set; } = GeofenceState.Outside;
    public DateTimeOffset? NearWindowStartedAtUtc { get; set; }
    public DateTimeOffset? LastEnteredAtUtc { get; set; }
    public DateTimeOffset? LastExitedAtUtc { get; set; }
    public DateTimeOffset? LastTriggeredAtUtc { get; set; }
    public DateTimeOffset? CooldownUntilUtc { get; set; }
    public DateTimeOffset? LastEvaluatedAtUtc { get; set; }
    public double? LastDistanceMeters { get; set; }
    public bool HasEstablishedState { get; set; }
    public bool NearStayHandled { get; set; }
    public IReadOnlyDictionary<GeofenceTriggerEvent, DateTimeOffset> LastTriggerTimes =>
        new ReadOnlyDictionary<GeofenceTriggerEvent, DateTimeOffset>(_lastTriggerTimes);

    public bool ShouldEvaluate(DateTimeOffset nowUtc) =>
        State != GeofenceState.Outside ||
        NearWindowStartedAtUtc.HasValue ||
        (CooldownUntilUtc.HasValue && CooldownUntilUtc.Value > nowUtc);

    public bool TryGetLastTriggerTime(GeofenceTriggerEvent eventType, out DateTimeOffset timestamp) =>
        _lastTriggerTimes.TryGetValue(eventType, out timestamp);

    public void SetTriggerTime(GeofenceTriggerEvent eventType, DateTimeOffset timestamp) =>
        _lastTriggerTimes[eventType] = timestamp;

    public void ResetNearWindow()
    {
        NearWindowStartedAtUtc = null;
        NearStayHandled = false;
    }
}

public sealed record GeofenceTriggeredEvent(
    PoiGeofenceDefinition Definition,
    GeofenceTriggerEvent EventType,
    double DistanceMeters,
    TimeSpan CooldownWindow,
    bool IsNativeTransition = false);

public sealed record GeofenceSkippedTrigger(
    PoiGeofenceDefinition Definition,
    GeofenceTriggerEvent EventType,
    double DistanceMeters,
    string Reason);

public sealed record GeofenceEvaluationResult(
    IReadOnlyList<GeofenceTriggeredEvent> AcceptedTriggers,
    IReadOnlyList<GeofenceSkippedTrigger> SkippedTriggers,
    string? NearestPoiId,
    double? NearestDistanceMeters,
    int CandidateCount);

public static class GeofenceTriggerSelector
{
    public static GeofenceTriggeredEvent? SelectBest(IReadOnlyList<GeofenceTriggeredEvent> triggers)
    {
        if (triggers.Count == 0)
        {
            return null;
        }

        return triggers
            .OrderByDescending(item => item.Definition.Priority)
            .ThenBy(item => item.DistanceMeters)
            .ThenBy(item => item.EventType == GeofenceTriggerEvent.EnteredRadius ? 0 : 1)
            .ThenBy(item => item.Definition.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
