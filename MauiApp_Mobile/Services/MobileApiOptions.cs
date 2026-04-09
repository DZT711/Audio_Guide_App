using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace MauiApp_Mobile.Services;

public static class MobileApiOptions
{
    private const string ApiBaseUrlPreferenceKey = "mobile_api_base_url";
    private const string DefaultAndroidDebugBaseUrl = "http://127.0.0.1:5123/";
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
            return DefaultAndroidDebugBaseUrl;
        }

        return DefaultDesktopBaseUrl;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var fallback = DeviceInfo.Platform == DevicePlatform.Android
            ? DefaultAndroidDebugBaseUrl
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
}
