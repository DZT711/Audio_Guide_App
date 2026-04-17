namespace WebApplication_API.Services;

public sealed class QrLinkOptions
{
    public const string SectionName = "QrLinks";

    public bool Enabled { get; set; } = true;

    public string? PublicBaseUrl { get; set; }

    public string? AndroidApkUrl { get; set; }

    public string? AndroidStoreUrl { get; set; }

    public string AppDeepLinkScheme { get; set; } = "smarttour";

    public string AppDeepLinkHost { get; set; } = "play";

    public int DefaultQrSize { get; set; } = 512;

    public string DefaultQrFormat { get; set; } = Project_SharedClassLibrary.Contracts.QrCodeFormats.Png;

    public int LandingOpenDelayMs { get; set; } = 120;

    public int LandingFallbackDelayMs { get; set; } = 1600;
}
