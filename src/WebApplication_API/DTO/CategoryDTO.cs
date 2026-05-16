namespace WebApplication_API.DTO;

public record CategoryDTO
(
    int Id,
    string Name,
    string? Description,
    // int NumOfLocations,
    int Status
);
