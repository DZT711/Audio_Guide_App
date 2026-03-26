using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class Language
{
    public int LanguageId { get; set; }

    [MaxLength(20)]
    public required string LangCode { get; set; }

    [MaxLength(100)]
    public required string LangName { get; set; }

    [MaxLength(100)]
    public string? NativeName { get; set; }

    public bool PreferNativeVoice { get; set; } = true;

    public bool IsDefault { get; set; }

    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
