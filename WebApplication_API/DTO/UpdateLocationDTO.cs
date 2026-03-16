using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.DTO;

public record UpdateLocationDTO
(
    [Required][StringLength(100)] string Name,
    [StringLength(255)] string Address,
    [Required][Range(0, int.MaxValue)] int CategoryId,
    [YearRange] int EstablishedYear,
    [StringLength(255)] string Description,
    [Required][Range(-90, 90)] double Latitude,
    [Required][Range(-180, 180)] double Longitude,
    [StringLength(100)] string OwnerName,
    [StringLength(255)] string WebURL,
    [StringLength(20)] string Phone,
    [EmailAddress] string Email,
    [Range(0, int.MaxValue)] int NumOfAudio,
    [Range(0, int.MaxValue)] int NumOfImg,
    [Range(0, int.MaxValue)] int NumOfPeopleVisited,
    [Required][Range(0, 1)] int Status
);