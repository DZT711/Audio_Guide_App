using System.ComponentModel.DataAnnotations;

namespace BlazorApp_AdminWeb.Models;

public record CategoryDto(int Id, string Name, string? Description, int Status);

public record LocationDto(
    int Id,
    string Name,
    string? Address,
    string Category,
    int EstablishedYear,
    string? Description,
    double Latitude,
    double Longitude,
    string? OwnerName,
    string? WebURL,
    string? Phone,
    string? Email,
    int Status);

public record AudioDto(
    int Id,
    string Title,
    string LocationName,
    string Description,
    string AudioURL,
    string Language,
    string VoiceGender,
    string Script,
    int Duration,
    int Status);

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
    [StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(255)]
    public string Address { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }

    [Range(1800, 3000)]
    public int EstablishedYear { get; set; } = DateTime.UtcNow.Year;

    [StringLength(255)]
    public string Description { get; set; } = "";

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [StringLength(100)]
    public string OwnerName { get; set; } = "";

    [StringLength(255)]
    public string WebURL { get; set; } = "";

    [StringLength(20)]
    public string Phone { get; set; } = "";

    [EmailAddress]
    public string Email { get; set; } = "";

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static LocationFormModel FromDto(LocationDto dto, IEnumerable<CategoryDto> categories) => new()
    {
        Name = dto.Name,
        Address = dto.Address ?? "",
        CategoryId = categories.FirstOrDefault(category => string.Equals(category.Name, dto.Category, StringComparison.OrdinalIgnoreCase))?.Id ?? 0,
        EstablishedYear = dto.EstablishedYear,
        Description = dto.Description ?? "",
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        OwnerName = dto.OwnerName ?? "",
        WebURL = dto.WebURL ?? "",
        Phone = dto.Phone ?? "",
        Email = dto.Email ?? "",
        Status = dto.Status
    };
}

public sealed class AudioFormModel
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(100)]
    public string LocationName { get; set; } = "";

    [StringLength(255)]
    public string Description { get; set; } = "";

    [Required]
    [StringLength(255)]
    public string AudioURL { get; set; } = "";

    [StringLength(50)]
    public string Language { get; set; } = "vi-VN";

    [StringLength(50)]
    public string VoiceGender { get; set; } = "Female";

    [StringLength(255)]
    public string Script { get; set; } = "";

    [Range(0, int.MaxValue)]
    public int Duration { get; set; }

    [Range(0, 1)]
    public int Status { get; set; } = 1;

    public static AudioFormModel FromDto(AudioDto dto) => new()
    {
        Title = dto.Title,
        LocationName = dto.LocationName,
        Description = dto.Description,
        AudioURL = dto.AudioURL,
        Language = dto.Language,
        VoiceGender = string.IsNullOrWhiteSpace(dto.VoiceGender) ? "Female" : dto.VoiceGender,
        Script = dto.Script,
        Duration = dto.Duration,
        Status = dto.Status
    };
}

public sealed class ApiMessageResponse
{
    public string Message { get; set; } = "";
}
