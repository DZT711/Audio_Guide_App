using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.DTO;

public record CreateAudioDTO
(
    [Required][StringLength(100)] string Title,
    [Required][StringLength(100)] string LocationName,
    [StringLength(255)] string Description,
    [Required][StringLength(255)] string AudioURL,
    [StringLength(50)] string Language,
    [StringLength(50)] string VoiceGender,
    [StringLength(255)] string Script,
    [Range(0, int.MaxValue)] int Duration,
    // [Range(0, int.MaxValue)] int NumOfPeoplePlayed,
    [Required][Range(0, 1)] int Status
);
