using System.ComponentModel.DataAnnotations;
using Project_SharedClassLibrary.Validation;

namespace Project_SharedClassLibrary.Contracts;

public record CategoryUpsertRequest(
    [Required][StringLength(100)] string Name,
    [StringLength(255)] string Description,
    [Required][Range(0, 1)] int Status);

public record LocationUpsertRequest(
    [Required][StringLength(100)] string Name,
    [StringLength(500)] string Address,
    [Required][Range(0, int.MaxValue)] int CategoryId,
    [YearRange] int EstablishedYear,
    [StringLength(255)] string Description,
    [Required][Range(-90, 90)] double Latitude,
    [Required][Range(-180, 180)] double Longitude,
    [StringLength(100)] string OwnerName,
    [StringLength(255)] string WebURL,
    [StringLength(20)] string Phone,
    [EmailAddress] string Email,
    [Required][Range(0, 1)] int Status);

public record AudioUpsertRequest(
    [Required][StringLength(100)] string Title,
    [Required][StringLength(100)] string LocationName,
    [StringLength(255)] string? Description,
    [StringLength(255)] string? AudioURL,
    [StringLength(50)] string? Language,
    [StringLength(50)] string? VoiceGender,
    [StringLength(4000)] string? Script,
    [Range(0, int.MaxValue)] int Duration,
    [Required][Range(0, 1)] int Status);
