using System.Net;
using Microsoft.Maui.Storage;
using Project_SharedClassLibrary.Storage;

namespace MauiApp_Mobile.Services;

public static class MobileApiOptions
{
    private const string HardcodedBaseUrl = "http://127.0.0.1:5123/";
    private const string PlaceholderBaseUrl = "http://smarttour.invalid/";

    public static string BaseUrl => HardcodedBaseUrl;
    public static Uri BaseUri => new(HardcodedBaseUrl, UriKind.Absolute);
    public static Uri PlaceholderBaseUri => new(PlaceholderBaseUrl, UriKind.Absolute);

    public static void SetBaseUrl(string baseUrl)
    {
    }

    public static void SetLastKnownWorkingBaseUrl(string baseUrl)
    {
    }

    public static Uri RewriteToCurrentBaseUri(Uri requestUri)
    {
        if (!requestUri.IsAbsoluteUri)
        {
            return new Uri(BaseUri, requestUri.OriginalString);
        }

        var builder = new UriBuilder(BaseUri)
        {
            Path = requestUri.AbsolutePath,
            Query = requestUri.Query.TrimStart('?')
        };

        return builder.Uri;
    }

    public static bool IsPlaceholderHost(string? host) =>
        string.Equals(host, PlaceholderBaseUri.Host, StringComparison.OrdinalIgnoreCase);

    public static Task<string> EnsureResolvedBaseUrlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(HardcodedBaseUrl);

    public static Task<string?> TryDiscoverAndRememberBaseUrlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(HardcodedBaseUrl);

    public static string ResolveImageUrl(string? imageUrl) =>
        ResolveArchiveUrl(imageUrl, SharedStoragePaths.NormalizePublicImagePath);

    public static string ResolveAudioUrl(string? audioUrl) =>
        ResolveArchiveUrl(audioUrl, SharedStoragePaths.NormalizePublicAudioPath);

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

            if (IsLoopbackDevelopmentHost(absoluteUri.Host) || IsPlaceholderHost(absoluteUri.Host))
            {
                return RewriteToCurrentBaseUri(absoluteUri).AbsoluteUri;
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
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase);
}
