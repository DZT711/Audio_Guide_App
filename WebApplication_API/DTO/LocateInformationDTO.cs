namespace WebApplication_API.DTO;

public record  LocateInformationDTO
(
    int Id,
    int CategoryId,
    string Name,
    string Address,
    int EstablishedYear,
    double Latitude,
    double Longitude,
    string OwnerName,
    string WebURL,
    string Phone,
    string Email,
    // int NumOfAudio,
    // int NumOfImg,
    // int NumOfPeopleVisited,
    int Status
);
