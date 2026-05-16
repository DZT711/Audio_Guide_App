using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Models;

public sealed class BufferedUploadFile
{
    public string Name { get; set; } = "";

    public string ContentType { get; set; } = "";

    public byte[] Content { get; set; } = [];
}

public sealed record DownloadedAdminFile(
    string FileName,
    string ContentType,
    byte[] Content,
    IReadOnlyDictionary<string, string> Headers)
{
    public string? GetHeader(string name) =>
        Headers.TryGetValue(name, out var value) ? value : null;
}

public sealed class AdminCredentialsModel
{
    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = "";
}

public sealed class CategoryFormModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(255)]
    public string Description { get; set; } = "";

    [StringLength(32)]
    public string ThemeName { get; set; } = "";

    [StringLength(16)]
    public string IconEmoji { get; set; } = "";

    [RegularExpression("^$|^#(?:[0-9a-fA-F]{6})$", ErrorMessage = "Use #RRGGBB format.")]
    public string PrimaryColor { get; set; } = "";

    [RegularExpression("^$|^#(?:[0-9a-fA-F]{6})$", ErrorMessage = "Use #RRGGBB format.")]
    public string SecondaryColor { get; set; } = "";

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static CategoryFormModel FromDto(CategoryDto dto) => new()
    {
        Name = dto.Name,
        Description = dto.Description ?? "",
        ThemeName = dto.ThemeName ?? "",
        IconEmoji = dto.IconEmoji ?? "",
        PrimaryColor = dto.PrimaryColor ?? "",
        SecondaryColor = dto.SecondaryColor ?? "",
        Status = dto.Status
    };
}

public sealed class LanguageFormModel
{
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = "vi-VN";

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(100)]
    public string NativeName { get; set; } = "";

    public bool PreferNativeVoice { get; set; } = true;

    public bool IsDefault { get; set; }

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static LanguageFormModel FromDto(LanguageDto dto) => new()
    {
        Code = dto.Code,
        Name = dto.Name,
        NativeName = dto.NativeName ?? "",
        PreferNativeVoice = dto.PreferNativeVoice,
        IsDefault = dto.IsDefault,
        Status = dto.Status
    };
}

public sealed class LocationFormModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(1000)]
    public string Address { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }

    public int? OwnerId { get; set; }

    [Range(1800, 3000)]
    public int EstablishedYear { get; set; } = DateTime.UtcNow.Year;

    [StringLength(5000)]
    public string Description { get; set; } = "";

    [Range(-90, 90)]
    public double Latitude { get; set; }

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

    [StringLength(500)]
    public string WebURL { get; set; } = "";

    [StringLength(30)]
    public string Phone { get; set; } = "";

    [EmailAddress]
    public string Email { get; set; } = "";

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    [Range(128, 2048)]
    public int QrSize { get; set; } = 512;

    [Required]
    [RegularExpression("^(png|jpg|jpeg|svg)$", ErrorMessage = "Format must be png, jpg, jpeg, or svg.")]
    public string QrFormat { get; set; } = QrCodeFormats.Png;

    public bool QrAutoplay { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int? QrAudioTrackId { get; set; }

    public string ExistingPreferenceImageUrl { get; set; } = "";

    public BufferedUploadFile? PreferenceImageFile { get; set; }

    public IReadOnlyList<string> ExistingImageUrls { get; set; } = [];

    public IReadOnlyList<BufferedUploadFile> ImageFiles { get; set; } = [];

    public static LocationFormModel FromDto(LocationDto dto, IEnumerable<CategoryDto> categories)
    {
        var preferenceImageUrl = dto.PreferenceImageUrl ?? dto.CoverImageUrl ?? "";

        return new LocationFormModel
        {
            Name = dto.Name,
            Address = dto.Address ?? "",
            CategoryId = dto.CategoryId > 0
                ? dto.CategoryId
                : categories.FirstOrDefault(category => string.Equals(category.Name, dto.Category, StringComparison.OrdinalIgnoreCase))?.Id ?? 0,
            OwnerId = dto.OwnerId,
            EstablishedYear = dto.EstablishedYear,
            Description = dto.Description ?? "",
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Radius = dto.Radius,
            StandbyRadius = dto.StandbyRadius,
            Priority = dto.Priority,
            DebounceSeconds = dto.DebounceSeconds,
            IsGpsTriggerEnabled = dto.IsGpsTriggerEnabled,
            WebURL = dto.WebURL ?? "",
            Phone = dto.Phone ?? "",
            Email = dto.Email ?? "",
            Status = dto.Status,
            QrSize = dto.QrSize,
            QrFormat = dto.QrFormat,
            QrAutoplay = dto.QrAutoplay,
            QrAudioTrackId = dto.QrAudioTrackId,
            ExistingPreferenceImageUrl = preferenceImageUrl,
            ExistingImageUrls = dto.ImageUrls
                .Where(item => !string.Equals(item, preferenceImageUrl, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PreferenceImageFile is null && string.IsNullOrWhiteSpace(ExistingPreferenceImageUrl))
        {
            yield return new ValidationResult(
                "Upload a preference image for this POI.",
                [nameof(PreferenceImageFile)]);
        }
    }
}

public sealed class AudioFormModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Choose a location.")]
    public int LocationId { get; set; }

    [StringLength(2000)]
    public string Description { get; set; } = "";

    [StringLength(500)]
    public string AudioPath { get; set; } = "";

    public IBrowserFile? AudioFile { get; set; }

    [Required]
    [StringLength(20)]
    public string Language { get; set; } = "vi-VN";

    [Required]
    [StringLength(20)]
    public string SourceType { get; set; } = "TTS";

    [StringLength(100)]
    public string VoiceName { get; set; } = "";

    [StringLength(20)]
    public string VoiceGender { get; set; } = "Female";

    [StringLength(8000)]
    public string Script { get; set; } = "";

    [Range(0, int.MaxValue)]
    public int Duration { get; set; }

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

    public static AudioFormModel FromDto(AudioDto dto) => new()
    {
        Title = dto.Title,
        LocationId = dto.LocationId,
        Description = dto.Description ?? "",
        AudioPath = dto.AudioURL ?? "",
        Language = dto.Language,
        SourceType = dto.SourceType,
        VoiceName = dto.VoiceName ?? "",
        VoiceGender = string.IsNullOrWhiteSpace(dto.VoiceGender) ? "Female" : dto.VoiceGender,
        Script = dto.Script ?? "",
        Duration = dto.Duration,
        Priority = dto.Priority,
        PlaybackMode = dto.PlaybackMode,
        InterruptPolicy = dto.InterruptPolicy,
        IsDownloadable = dto.IsDownloadable,
        Status = dto.Status
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasAudioFile = AudioFile is not null || !string.IsNullOrWhiteSpace(AudioPath);
        var hasScript = !string.IsNullOrWhiteSpace(Script);

        if ((SourceType == "Recorded" || SourceType == "Hybrid") && !hasAudioFile)
        {
            yield return new ValidationResult("Choose an audio file to upload.", [nameof(AudioPath)]);
        }

        if ((SourceType == "TTS" || SourceType == "Hybrid") && !hasScript)
        {
            yield return new ValidationResult("Enter a narration script for TTS-based content.", [nameof(Script)]);
        }
    }
}

public sealed class TourFormModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(5000)]
    public string Description { get; set; } = "";

    [Range(0.5d, 12d)]
    public double WalkingSpeedKph { get; set; } = TourDefaults.DefaultWalkingSpeedKph;

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Start time must use HH:mm.")]
    public string StartTime { get; set; } = TourDefaults.DefaultStartTime;

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public List<int> StopLocationIds { get; set; } = [];

    public static TourFormModel FromDto(TourDto dto) => new()
    {
        Name = dto.Name,
        Description = dto.Description ?? "",
        WalkingSpeedKph = dto.WalkingSpeedKph <= 0 ? TourDefaults.DefaultWalkingSpeedKph : dto.WalkingSpeedKph,
        StartTime = string.IsNullOrWhiteSpace(dto.StartTime) ? TourDefaults.DefaultStartTime : dto.StartTime!,
        Status = dto.Status,
        StopLocationIds = dto.Stops
            .OrderBy(item => item.SequenceOrder)
            .Select(item => item.LocationId)
            .ToList()
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StopLocationIds.Count == 0)
        {
            yield return new ValidationResult("Choose at least one POI for the tour.", [nameof(StopLocationIds)]);
        }

        if (StopLocationIds.Count != StopLocationIds.Distinct().Count())
        {
            yield return new ValidationResult("Each POI can only appear once in a tour.", [nameof(StopLocationIds)]);
        }
    }
}

public sealed class UserFormModel
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = "";

    [StringLength(128)]
    public string Password { get; set; } = "";

    [StringLength(150)]
    public string FullName { get; set; } = "";

    [Required]
    public string Role { get; set; } = AdminRoles.User;

    [EmailAddress]
    public string Email { get; set; } = "";

    [StringLength(30)]
    public string Phone { get; set; } = "";

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static UserFormModel FromDto(DashboardUserDto dto) => new()
    {
        Username = dto.Username,
        FullName = dto.FullName ?? "",
        Role = dto.Role,
        Email = dto.Email ?? "",
        Phone = dto.Phone ?? "",
        Status = dto.Status
    };
}
