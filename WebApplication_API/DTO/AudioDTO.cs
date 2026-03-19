namespace WebApplication_API.DTO;

public record AudioDTO
(
    int Id,
    string Title,
    string LocationName,
    string Description,
    string AudioURL,
    string Language,
    string VoiceGender,
    string Script,
    int Duration,
    // int NumOfPeoplePlayed,
    int Status
);
