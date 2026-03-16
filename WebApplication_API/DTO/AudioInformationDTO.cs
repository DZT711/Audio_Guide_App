namespace WebApplication_API.DTO;

public record AudioInformationDTO
(
    int Id,
    int LocationId,
    string Title,
    string Description,
    string AudioURL,
    string Language,
    string VoiceGender,
    string Script,
    int Duration,
    // int NumOfPeoplePlayed,
    int Status
);