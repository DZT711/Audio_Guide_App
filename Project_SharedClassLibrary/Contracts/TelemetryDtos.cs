namespace Project_SharedClassLibrary.Contracts;

public sealed class RouteHistorySampleIngestDto
{
    public string DeviceHash { get; init; } = "";
    public string? SessionHash { get; init; }
    public DateTime CapturedAtUtc { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public double? SpeedMetersPerSecond { get; init; }
    public int? BatteryPercent { get; init; }
    public bool IsForeground { get; init; }
    public int? TourId { get; init; }
    public int? PoiId { get; init; }
    public string? Context { get; init; }
}

public sealed class RouteHistoryBatchIngestRequest
{
    public IReadOnlyList<RouteHistorySampleIngestDto> Samples { get; init; } = [];
}

public sealed class AudioPlayEventIngestDto
{
    public string DeviceHash { get; init; } = "";
    public string? SessionHash { get; init; }
    public DateTime PlayedAtUtc { get; init; }
    public int? AudioId { get; init; }
    public int? PoiId { get; init; }
    public int? TourId { get; init; }
    public string EventType { get; init; } = "Started";
    public string TriggerSource { get; init; } = "Unknown";
    public int? ListeningSeconds { get; init; }
    public double? PositionSeconds { get; init; }
    public int? BatteryPercent { get; init; }
    public string? NetworkType { get; init; }
    public string? Context { get; init; }
}

public sealed class AudioPlayEventBatchIngestRequest
{
    public IReadOnlyList<AudioPlayEventIngestDto> Events { get; init; } = [];
}

public sealed class AudioListeningSessionIngestDto
{
    public string DeviceHash { get; init; } = "";
    public string? SessionHash { get; init; }
    public int? AudioId { get; init; }
    public int? PoiId { get; init; }
    public int? TourId { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime EndedAtUtc { get; init; }
    public int ListeningSeconds { get; init; }
    public bool IsCompleted { get; init; }
    public string? InterruptedReason { get; init; }
    public string? Context { get; init; }
}

public sealed class AudioListeningSessionBatchIngestRequest
{
    public IReadOnlyList<AudioListeningSessionIngestDto> Sessions { get; init; } = [];
}

public sealed class TelemetryIngestResultDto
{
    public int AcceptedCount { get; init; }
    public int RejectedCount { get; init; }
}
