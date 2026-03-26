namespace WebApplication_API.Model;

public class PlaybackEvent
{
    public int PlaybackEventId { get; set; }
    public string? DeviceId { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public int? AudioId { get; set; }
    public Audio? Audio { get; set; }
    public string TriggerSource { get; set; } = "GeofenceEnter";
    public string EventType { get; set; } = "Queued";
    public DateTime EventAt { get; set; } = DateTime.UtcNow;
    public int? ListeningSeconds { get; set; }
    public int? QueuePosition { get; set; }
    public int? BatteryPercent { get; set; }
    public string? NetworkType { get; set; }
    public string? SessionId { get; set; }
}
