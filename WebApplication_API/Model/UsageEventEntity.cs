using Project_SharedClassLibrary.Contracts;

namespace WebApplication_API.Model;

public class UsageEventEntity
{
    public Guid Id { get; set; }

    public required string DeviceId { get; set; }

    public UsageEventType EventType { get; set; }

    public string? ReferenceId { get; set; }

    public string? Details { get; set; }

    public int DurationSeconds { get; set; }

    public DateTime Timestamp { get; set; }
}
