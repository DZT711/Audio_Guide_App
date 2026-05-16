using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class QrDeviceCheckLog
{
    public int QrDeviceCheckLogId { get; set; }

    public int LocationId { get; set; }

    public Location? Location { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    public string? DeviceName { get; set; }

    [MaxLength(80)]
    public string? Platform { get; set; }

    [MaxLength(120)]
    public string? OsVersion { get; set; }

    [MaxLength(32)]
    public string? QrCode { get; set; }

    public int WeakScore { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }
}
