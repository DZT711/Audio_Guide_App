namespace WebApplication_API.Services;

public sealed class QrLinkOptions
{
    public const string SectionName = "QrLinks";

    public bool Enabled { get; set; } = true;

    public string? PublicBaseUrl { get; set; }

    public string? AndroidApkUrl { get; set; }

    public string? AndroidStoreUrl { get; set; }

    public bool EnableDynamicAndroidApkBuild { get; set; }

    public string AndroidProjectFilePath { get; set; } = "..\\MauiApp_Mobile\\MauiApp_Mobile.csproj";

    public string AndroidMobileConfigFilePath { get; set; } = "..\\MauiApp_Mobile\\Resources\\Raw\\mobile-api.json";

    public string AndroidPackageOutputRelativePath { get; set; } = "wwwroot\\downloads\\smarttour-latest.apk";

    public string AndroidBuildConfiguration { get; set; } = "Debug";

    public string AndroidTargetFramework { get; set; } = "net10.0-android";

    public string AndroidPackageFormat { get; set; } = "apk";

    public int AndroidBuildTimeoutSeconds { get; set; } = 900;

    public string AppDeepLinkScheme { get; set; } = "smarttour";

    public string AppDeepLinkHost { get; set; } = "play";

    public int DefaultQrSize { get; set; } = 512;

    public string DefaultQrFormat { get; set; } = Project_SharedClassLibrary.Contracts.QrCodeFormats.Png;

    public int LandingOpenDelayMs { get; set; } = 120;

    public int LandingFallbackDelayMs { get; set; } = 1600;
}
