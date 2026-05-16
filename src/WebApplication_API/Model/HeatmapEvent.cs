namespace WebApplication_API.Model;

public class HeatmapEvent
{
    public int HeatmapEventId { get; set; }
    public required string DeviceId { get; set; }
    public string? SessionId { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public int? TourId { get; set; }
    public string EventType { get; set; } = "EnterPoi";
    public int Weight { get; set; } = 1;
    public string TriggerSource { get; set; } = "Unknown";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public int? BatteryPercent { get; set; }
    public bool IsForeground { get; set; } = true;
    public string? Context { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
