using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.DTO;

public record UpdateCategoryDTO
(
    [Required][StringLength(100)] string Name,
    [StringLength(255)] string Description,
    // [Required][Range(0, int.MaxValue)] int NumOfLocations,
    [Required][Range(0, 1)] int Status
);