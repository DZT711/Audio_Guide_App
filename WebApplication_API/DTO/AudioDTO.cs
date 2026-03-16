namespace WebApplication_API.DTO;

public record AudioDTO
(
    int Id,
    string Title,
    string Description,
    string AudioURL,
    string Language,
    string VoiceGender,
    int Duration,
    int NumOfPeoplePlayed,
    int Status,
    string? LocationName

);
