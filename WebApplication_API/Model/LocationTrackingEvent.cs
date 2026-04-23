namespace WebApplication_API.Model;

public class LocationTrackingEvent
{
    public int TrackingEventId { get; set; }
    public required string DeviceId { get; set; }
    public string? SessionId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedMetersPerSecond { get; set; }
    public int? BatteryPercent { get; set; }
    public bool IsForeground { get; set; } = true;
    public int? TourId { get; set; }
    public int? PoiId { get; set; }
    public string? Context { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
