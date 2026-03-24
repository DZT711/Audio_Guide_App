namespace Project_SharedClassLibrary.Contracts;

public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    int Status);

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

public sealed class ApiMessageResponse
{
    public string Message { get; set; } = "";
}
