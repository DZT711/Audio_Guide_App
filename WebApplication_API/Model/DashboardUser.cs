using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class DashboardUser
{
    public int UserId { get; set; }

    [MaxLength(100)]
    public required string Username { get; set; }

    [MaxLength(512)]
    public required string PasswordHash { get; set; }

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(32)]
    public string Role { get; set; } = "User";

    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Location> OwnedLocations { get; set; } = [];

    public ICollection<Tour> OwnedTours { get; set; } = [];
}
