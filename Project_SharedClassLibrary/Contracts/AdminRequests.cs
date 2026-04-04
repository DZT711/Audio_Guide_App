using System.ComponentModel.DataAnnotations;

namespace Project_SharedClassLibrary.Contracts;

public sealed class AdminLoginRequest
{
    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = "";
}

public sealed class DashboardUserUpsertRequest
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = "";

    [StringLength(1000)]
    public string? Password { get; set; }

    [StringLength(150)]
    public string? FullName { get; set; }

    [Required]
    [StringLength(32)]
    public string Role { get; set; } = "User";

    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [Range(0, 1)]
    public int Status { get; set; } = 1;
}

public sealed class DashboardUserInviteRequest
{
    [StringLength(150)]
    public string? FullName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required]
    [StringLength(32)]
    public string Role { get; set; } = "User";

    [StringLength(100)]
    public string? Username { get; set; }
}

public sealed class DashboardUserInviteResultDto
{
    public DashboardUserDto User { get; init; } = new();
    public string TemporaryPassword { get; init; } = "";
}

public sealed class AudioTtsPreviewRequest
{
    [Required]
    [StringLength(8000)]
    public string Text { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string Language { get; set; } = "vi-VN";

    [StringLength(20)]
    public string? VoiceGender { get; set; }

    public bool PreferNativeVoice { get; set; } = true;
}

public sealed class PublicAudioTranslateTtsRequest
{
    [Required]
    [StringLength(8000)]
    public string Text { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string SourceLanguage { get; set; } = "vi-VN";

    [Required]
    [StringLength(20)]
    public string TargetLanguage { get; set; } = "en-US";

    [StringLength(20)]
    public string? VoiceGender { get; set; }
}
