using System;

namespace WebApplication_API.Model;

public class Audio
{
    public int Id { get; set; }
    public required int LocationId { get; set; }
    public required string Title { get; set; }
    public required string FilePath { get; set; }
    public string? Language { get; set; }
    public int? Duration { get; set; }
    public string? Description { get; set; }
    public string? Script { get; set; }
    // public int NumOfPeoplePlayed { get; set; }
    public string? VoiceGender { get; set; }
    public required int Status { get; set; }
}
