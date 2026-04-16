using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public interface IGeofencePlatformMonitor
{
    event EventHandler<NativeGeofenceTransition>? TransitionReceived;
    bool IsSupported { get; }
    Task<NativeGeofenceRegistrationResult> RegisterAsync(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        CancellationToken cancellationToken = default);
    Task UnregisterAllAsync(CancellationToken cancellationToken = default);
}

public sealed record NativeGeofenceTransition(
    string PoiId,
    GeofenceTriggerEvent EventType,
    DateTimeOffset OccurredAtUtc,
    bool IsNativeTransition = true);

public sealed record NativeGeofenceRegistrationResult(
    bool IsSuccessful,
    int RegisteredCount,
    string Mode,
    string? FailureReason = null);

public enum GeofenceRunState
{
    Stopped,
    Starting,
    Running,
    FallbackOnly,
    PermissionDenied,
    Faulted
}

public sealed record GeofenceDebugSnapshot(
    string? NearestPoiId,
    double? NearestDistanceMeters,
    int CandidateCount,
    long DroppedLocationEvents,
    int QueueDepth,
    TimeSpan LastEvaluationDuration,
    DateTimeOffset? LastProcessedAtUtc,
    string LastTriggerSummary)
{
    public static GeofenceDebugSnapshot Empty { get; } = new(
        null,
        null,
        0,
        0,
        0,
        TimeSpan.Zero,
        null,
        string.Empty);
}
