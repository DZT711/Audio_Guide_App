namespace Project_SharedClassLibrary.Contracts;

public static class HeatmapEventTypes
{
    public const string EnterPoi = "EnterPoi";
    public const string DwellTime = "DwellTime";
    public const string AudioPlay = "AudioPlay";

    public static string Normalize(string? value) =>
        value?.Trim() switch
        {
            var eventType when string.Equals(eventType, DwellTime, StringComparison.OrdinalIgnoreCase) => DwellTime,
            var eventType when string.Equals(eventType, AudioPlay, StringComparison.OrdinalIgnoreCase) => AudioPlay,
            _ => EnterPoi
        };

    public static int ResolveWeight(string? eventType, int? requestedWeight = null)
    {
        if (requestedWeight.HasValue && requestedWeight.Value > 0)
        {
            return requestedWeight.Value;
        }

        return Normalize(eventType) switch
        {
            DwellTime => 2,
            AudioPlay => 3,
            _ => 1
        };
    }
}

public sealed class HeatmapEventIngestDto
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
    public int? PoiId { get; init; }
    public int? TourId { get; init; }
    public string EventType { get; init; } = HeatmapEventTypes.EnterPoi;
    public int Weight { get; init; } = 1;
    public string TriggerSource { get; init; } = "Unknown";
    public string? Context { get; init; }
}

public sealed class HeatmapEventBatchIngestRequest
{
    public IReadOnlyList<HeatmapEventIngestDto> Events { get; init; } = [];
}
