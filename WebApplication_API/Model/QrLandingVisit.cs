using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class QrLandingVisit
{
    public int QrLandingVisitId { get; set; }

    public int LocationId { get; set; }

    public Location? Location { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? Source { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    [MaxLength(500)]
    public string? Referrer { get; set; }
}
