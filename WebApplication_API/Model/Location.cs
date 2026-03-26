using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class Location
{
    public int LocationId { get; set; }

    public int? CategoryId { get; set; }

    public Category? Category { get; set; }

    public int? OwnerId { get; set; }

    public DashboardUser? Owner { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double Radius { get; set; } = 30;

    public double StandbyRadius { get; set; } = 12;

    public int Priority { get; set; }

    public int DebounceSeconds { get; set; } = 300;

    public bool IsGpsTriggerEnabled { get; set; } = true;

    public string? Address { get; set; }

    [MaxLength(500)]
    public string? WebURL { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? PhoneContact { get; set; }

    public int? EstablishedYear { get; set; }

    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Audio> AudioContents { get; set; } = [];

    public ICollection<LocationImage> Images { get; set; } = [];

    public ICollection<PlaybackEvent> PlaybackEvents { get; set; } = [];
}
