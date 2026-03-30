using System.ComponentModel.DataAnnotations;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Validation;

namespace Project_SharedClassLibrary.Contracts;

public sealed class CategoryUpsertRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(0, 1)]
    public int Status { get; set; } = 1;
}

public sealed class LanguageUpsertRequest
{
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = "vi-VN";

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(100)]
    public string? NativeName { get; set; }

    public bool PreferNativeVoice { get; set; } = true;

    public bool IsDefault { get; set; }

    [Range(0, 1)]
    public int Status { get; set; } = 1;
}

public class LocationUpsertRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(5000)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Range(0, int.MaxValue)]
    public int? OwnerId { get; set; }

    [Required]
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; set; }

    [Range(1, 5000)]
    public double Radius { get; set; } = 30;

    [Range(1, 5000)]
    public double StandbyRadius { get; set; } = 12;

    [Range(0, int.MaxValue)]
    public int Priority { get; set; }

    [Range(0, 86400)]
    public int DebounceSeconds { get; set; } = 300;

    public bool IsGpsTriggerEnabled { get; set; } = true;

    [StringLength(1000)]
    public string? Address { get; set; }

    [StringLength(500)]
    public string? WebURL { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    public List<string> RetainedImageUrls { get; set; } = [];

    [YearRange]
    public int EstablishedYear { get; set; } = DateTime.UtcNow.Year;

    [Range(0, 1)]
    public int Status { get; set; } = 1;
}

public class AudioUpsertRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int LocationId { get; set; }

    [Required]
    [StringLength(20)]
    public string Language { get; set; } = "vi-VN";

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(20)]
    public string SourceType { get; set; } = "TTS";

    [StringLength(8000)]
    public string? Script { get; set; }

    [StringLength(500)]
    public string? AudioURL { get; set; }

    [Range(0, int.MaxValue)]
    public int? FileSizeBytes { get; set; }

    [Range(0, int.MaxValue)]
    public int Duration { get; set; }

    [StringLength(100)]
    public string? VoiceName { get; set; }

    [StringLength(20)]
    public string? VoiceGender { get; set; }

    [Range(0, int.MaxValue)]
    public int Priority { get; set; }

    [Required]
    [StringLength(32)]
    public string PlaybackMode { get; set; } = "Auto";

    [Required]
    [StringLength(32)]
    public string InterruptPolicy { get; set; } = "NotificationFirst";

    public bool IsDownloadable { get; set; } = true;

    [Range(0, 1)]
    public int Status { get; set; } = 1;
}

public sealed class TourStopUpsertRequest
{
    [Range(1, int.MaxValue)]
    public int LocationId { get; set; }

    [Range(1, int.MaxValue)]
    public int SequenceOrder { get; set; }
}

public sealed class TourUpsertRequest : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(5000)]
    public string? Description { get; set; }

    [Range(0.5d, 12d)]
    public double WalkingSpeedKph { get; set; } = TourDefaults.DefaultWalkingSpeedKph;

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Start time must use HH:mm.")]
    public string? StartTime { get; set; } = TourDefaults.DefaultStartTime;

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public TourRoutePreviewDto? RoutePreview { get; set; }

    public List<TourStopUpsertRequest> Stops { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Stops.Count == 0)
        {
            yield return new ValidationResult("Choose at least one POI for the tour.", [nameof(Stops)]);
        }

        var duplicateIds = Stops
            .GroupBy(item => item.LocationId)
            .Where(group => group.Key > 0 && group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            yield return new ValidationResult("Each POI can only appear once in the same tour.", [nameof(Stops)]);
        }
    }
}
