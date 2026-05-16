using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class ChangeRequest
{
    public int RequestId { get; set; }

    [MaxLength(32)]
    public string TargetTable { get; set; } = "";

    public int? TargetId { get; set; }

    public int OwnerId { get; set; }

    public DashboardUser? Owner { get; set; }

    [MaxLength(16)]
    public string RequestType { get; set; } = "CREATE";

    public string NewDataJson { get; set; } = "";

    [MaxLength(2000)]
    public string? Reason { get; set; }

    [MaxLength(4000)]
    public string? AdminNote { get; set; }

    [MaxLength(16)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<InboxMessage> InboxMessages { get; set; } = [];
}
