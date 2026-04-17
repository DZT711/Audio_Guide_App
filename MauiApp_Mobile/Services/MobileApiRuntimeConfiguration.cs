namespace MauiApp_Mobile.Services;

internal sealed class MobileApiRuntimeConfiguration
{
    public string? BaseUrl { get; set; }

    public string? PublicBaseUrl { get; set; }

    public bool AllowLocalhostFallback { get; set; } = true;

    public List<string> FallbackBaseUrls { get; set; } = [];
}
