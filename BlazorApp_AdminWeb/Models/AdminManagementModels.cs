using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Models;

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

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static CategoryFormModel FromDto(CategoryDto dto) => new()
    {
        Name = dto.Name,
        Description = dto.Description ?? "",
        Status = dto.Status
    };
}

public sealed class LocationFormModel
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

    [StringLength(100)]
    public string Ward { get; set; } = "";

    [StringLength(100)]
    public string City { get; set; } = "";

    [StringLength(500)]
    public string ImageUrl { get; set; } = "";

    [StringLength(500)]
    public string WebURL { get; set; } = "";

    [StringLength(30)]
    public string Phone { get; set; } = "";

    [EmailAddress]
    public string Email { get; set; } = "";

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static LocationFormModel FromDto(LocationDto dto, IEnumerable<CategoryDto> categories) => new()
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
        Ward = dto.Ward ?? "",
        City = dto.City ?? "",
        ImageUrl = dto.ImageUrl ?? "",
        WebURL = dto.WebURL ?? "",
        Phone = dto.Phone ?? "",
        Email = dto.Email ?? "",
        Status = dto.Status
    };
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
