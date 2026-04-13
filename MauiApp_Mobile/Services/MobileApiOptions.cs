using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Project_SharedClassLibrary.Storage;

namespace MauiApp_Mobile.Services;

public static class MobileApiOptions
{
    private const string ApiBaseUrlPreferenceKey = "mobile_api_base_url";
    private const string DefaultAndroidPhysicalDeviceBaseUrl = "http://192.168.1.3:5123/";
    private const string DefaultAndroidEmulatorBaseUrl = "http://10.0.2.2:5123/";
    private const string DefaultDesktopBaseUrl = "http://localhost:5123/";

    public static string BaseUrl => GetBaseUrl();

    public static Uri BaseUri => new(BaseUrl, UriKind.Absolute);

    public static void SetBaseUrl(string baseUrl) =>
        Preferences.Default.Set(ApiBaseUrlPreferenceKey, NormalizeBaseUrl(baseUrl));

    private static string GetBaseUrl()
    {
        var configuredBaseUrl = Preferences.Default.Get(ApiBaseUrlPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return NormalizeBaseUrl(configuredBaseUrl);
        }

        return GetDefaultBaseUrl();
    }

    private static string GetDefaultBaseUrl()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return DeviceInfo.DeviceType == DeviceType.Virtual
                ? DefaultAndroidEmulatorBaseUrl
                : DefaultAndroidPhysicalDeviceBaseUrl;
        }

        return DefaultDesktopBaseUrl;
    }

    public static string ResolveImageUrl(string? imageUrl) =>
        ResolveArchiveUrl(imageUrl, SharedStoragePaths.NormalizePublicImagePath);

    public static string ResolveAudioUrl(string? audioUrl) =>
        ResolveArchiveUrl(audioUrl, SharedStoragePaths.NormalizePublicAudioPath);

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var fallback = DeviceInfo.Platform == DevicePlatform.Android
            ? GetDefaultBaseUrl()
            : DefaultDesktopBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return fallback;
        }

        var trimmedBaseUrl = baseUrl.Trim();
        if (!Uri.TryCreate(trimmedBaseUrl, UriKind.Absolute, out var uri))
        {
            trimmedBaseUrl = trimmedBaseUrl.Trim('/');
            trimmedBaseUrl = $"http://{trimmedBaseUrl}/";
            return trimmedBaseUrl;
        }

        var builder = new UriBuilder(uri)
        {
            Path = string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/"
                ? "/"
                : $"{uri.AbsolutePath.TrimEnd('/')}/"
        };

        return builder.Uri.ToString();
    }

    private static string ResolveArchiveUrl(string? rawUrl, Func<string?, string?> normalizeManagedPath)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return string.Empty;
        }

        var normalizedValue = rawUrl.Trim().Replace("\\", "/");
        var normalizedManagedPath = normalizeManagedPath(normalizedValue);
        if (!string.IsNullOrWhiteSpace(normalizedManagedPath) &&
            normalizedManagedPath.StartsWith("/", StringComparison.Ordinal))
        {
            return new Uri(BaseUri, normalizedManagedPath.TrimStart('/')).AbsoluteUri;
        }

        if (Uri.TryCreate(normalizedValue, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
            {
                return absoluteUri.AbsoluteUri;
            }

            if (IsLoopbackDevelopmentHost(absoluteUri.Host))
            {
                var rewrittenBuilder = new UriBuilder(BaseUri)
                {
                    Path = absoluteUri.AbsolutePath,
                    Query = absoluteUri.Query.TrimStart('?')
                };

                return rewrittenBuilder.Uri.AbsoluteUri;
            }

            return absoluteUri.AbsoluteUri;
        }

        if (Path.IsPathRooted(normalizedValue) && !normalizedValue.StartsWith("/", StringComparison.Ordinal))
        {
            return normalizedValue;
        }

        if (!normalizedValue.Contains('/') && !normalizedValue.Contains('\\'))
        {
            return normalizedValue;
        }

        return new Uri(BaseUri, normalizedValue.TrimStart('/')).AbsoluteUri;
    }

    private static bool IsLoopbackDevelopmentHost(string? host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "10.0.2.2", StringComparison.OrdinalIgnoreCase);
}
