namespace WebApplication_API.Model;

public class AudioListeningSession
{
    public int AudioListeningSessionId { get; set; }
    public required string DeviceId { get; set; }
    public string? SessionId { get; set; }
    public int? AudioId { get; set; }
    public Audio? Audio { get; set; }
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public int? TourId { get; set; }
    public int? PoiId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int ListeningSeconds { get; set; }
    public bool IsCompleted { get; set; }
    public string? InterruptedReason { get; set; }
    public string? Context { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
