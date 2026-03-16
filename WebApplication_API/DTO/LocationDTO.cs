namespace WebApplication_API.DTO;

public record LocationDTO
(
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
    // int NumOfAudio,
    // int NumOfImg,
    // int NumOfPeopleVisited,
    int Status
);


