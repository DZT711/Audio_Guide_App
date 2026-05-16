using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class InboxMessage
{
    public int MessageId { get; set; }

    public int UserId { get; set; }

    public DashboardUser? User { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(4000)]
    public string Body { get; set; } = "";

    [MaxLength(32)]
    public string MessageType { get; set; } = "Info";

    public int? RelatedRequestId { get; set; }

    public ChangeRequest? RelatedRequest { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}
