using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class Audio
{
    public int AudioId { get; set; }

    public int LocationId { get; set; }

    public Location? Location { get; set; }

    [MaxLength(20)]
    public string LanguageCode { get; set; } = "vi-VN";

    [MaxLength(200)]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [MaxLength(20)]
    public string SourceType { get; set; } = "TTS";

    public string? Script { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }

    public int? FileSizeBytes { get; set; }

    public int? DurationSeconds { get; set; }

    [MaxLength(100)]
    public string? VoiceName { get; set; }

    [MaxLength(20)]
    public string? VoiceGender { get; set; }

    public int Priority { get; set; }

    [MaxLength(32)]
    public string PlaybackMode { get; set; } = "Auto";

    [MaxLength(32)]
    public string InterruptPolicy { get; set; } = "NotificationFirst";

    public bool IsDownloadable { get; set; } = true;

    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<PlaybackEvent> PlaybackEvents { get; set; } = [];
}
