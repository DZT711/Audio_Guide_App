using System.ComponentModel.DataAnnotations;
using Project_SharedClassLibrary.Constants;

namespace WebApplication_API.Model;

public class Tour
{
    public int TourId { get; set; }

    public int? OwnerId { get; set; }

    public DashboardUser? Owner { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    public double TotalDistanceKm { get; set; }

    public int EstimatedDurationMinutes { get; set; }

    public double WalkingSpeedKph { get; set; } = TourDefaults.DefaultWalkingSpeedKph;

    [MaxLength(5)]
    public string? StartTime { get; set; }

    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<TourLocation> Stops { get; set; } = [];
}
