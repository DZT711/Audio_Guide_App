using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class ActivityLog
{
    public int ActivityLogId { get; set; }

    public int? UserId { get; set; }

    public DashboardUser? User { get; set; }

    [MaxLength(100)]
    public string UserName { get; set; } = "";

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(32)]
    public string Role { get; set; } = "";

    [MaxLength(50)]
    public string ActionType { get; set; } = "";

    [MaxLength(80)]
    public string EntityType { get; set; } = "";

    public int? EntityId { get; set; }

    [MaxLength(200)]
    public string? EntityName { get; set; }

    [MaxLength(500)]
    public string Summary { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
