using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Maui.Storage;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Storage;

namespace MauiApp_Mobile.Services;

public static class MobileApiOptions
{
    private const string ConfigFileName = "mobile-api.json";
    private const string PlaceholderBaseUrl = "http://smarttour.invalid/";
    private const string BaseUrlPreferenceKey = "smarttour.mobile.api.base-url";
    private const string LastKnownWorkingBaseUrlPreferenceKey = "smarttour.mobile.api.last-known-working-base-url";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim ConfigurationSemaphore = new(1, 1);
    private static readonly HttpClient ProbeHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };
    private static readonly object StateLock = new();
    private static MobileApiRuntimeConfiguration _runtimeConfiguration = new();
    private static string _currentBaseUrl = GetDefaultBaseUrl();
    private static bool _runtimeConfigurationLoaded;

    public static string BaseUrl
    {
        get
        {
            lock (StateLock)
            {
                return _currentBaseUrl;
            }
        }
    }

    public static Uri BaseUri => new(BaseUrl, UriKind.Absolute);
    public static Uri PlaceholderBaseUri => new(PlaceholderBaseUrl, UriKind.Absolute);

    public static void SetBaseUrl(string baseUrl)
    {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
        if (normalizedBaseUrl is null)
        {
            return;
        }

        Preferences.Default.Set(BaseUrlPreferenceKey, normalizedBaseUrl);
        UpdateCurrentBaseUrl(normalizedBaseUrl);
    }

    public static void SetLastKnownWorkingBaseUrl(string baseUrl)
    {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
        if (normalizedBaseUrl is null)
        {
            return;
        }

        Preferences.Default.Set(LastKnownWorkingBaseUrlPreferenceKey, normalizedBaseUrl);
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

    public static bool IsNgrokHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return host.Contains("ngrok-free.dev", StringComparison.OrdinalIgnoreCase)
               || host.Contains("ngrok.io", StringComparison.OrdinalIgnoreCase)
               || host.Contains("ngrok.app", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<string> EnsureResolvedBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRuntimeConfigurationLoadedAsync(cancellationToken);
        var resolvedBaseUrl = await ResolvePreferredBaseUrlAsync(cancellationToken);
        UpdateCurrentBaseUrl(resolvedBaseUrl);
        return resolvedBaseUrl;
    }

    public static async Task<string?> TryDiscoverAndRememberBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRuntimeConfigurationLoadedAsync(cancellationToken);
        var resolvedBaseUrl = await ResolvePreferredBaseUrlAsync(cancellationToken);
        UpdateCurrentBaseUrl(resolvedBaseUrl);
        return resolvedBaseUrl;
    }

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

            var managedPathFromAbsoluteUri = normalizeManagedPath(absoluteUri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(managedPathFromAbsoluteUri) &&
                managedPathFromAbsoluteUri.StartsWith("/", StringComparison.Ordinal))
            {
                return new Uri(BaseUri, managedPathFromAbsoluteUri.TrimStart('/')).AbsoluteUri;
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

    private static async Task EnsureRuntimeConfigurationLoadedAsync(CancellationToken cancellationToken)
    {
        if (_runtimeConfigurationLoaded)
        {
            return;
        }

        await ConfigurationSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_runtimeConfigurationLoaded)
            {
                return;
            }

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(ConfigFileName);
                var configuration = await JsonSerializer.DeserializeAsync<MobileApiRuntimeConfiguration>(
                    stream,
                    JsonOptions,
                    cancellationToken);

                _runtimeConfiguration = configuration ?? new MobileApiRuntimeConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MobileApi] Failed to load {ConfigFileName}: {ex.Message}");
                _runtimeConfiguration = new MobileApiRuntimeConfiguration();
            }

            _runtimeConfigurationLoaded = true;
        }
        finally
        {
            ConfigurationSemaphore.Release();
        }
    }

    private static async Task<string> ResolvePreferredBaseUrlAsync(CancellationToken cancellationToken)
    {
        var candidates = BuildCandidateBaseUrls().ToList();
        if (candidates.Count == 0)
        {
            return GetDefaultBaseUrl();
        }

        foreach (var candidate in candidates)
        {
            if (await CanReachBaseUrlAsync(candidate, cancellationToken))
            {
                Preferences.Default.Set(LastKnownWorkingBaseUrlPreferenceKey, candidate);
                return candidate;
            }
        }

        return candidates[0];
    }

    private static IEnumerable<string> BuildCandidateBaseUrls()
    {
        var candidates = new List<string>();

        AddCandidate(candidates, Preferences.Default.Get(BaseUrlPreferenceKey, string.Empty));
        AddCandidate(candidates, Environment.GetEnvironmentVariable("SMARTTOUR_API_BASE_URL"));
        AddCandidate(candidates, Environment.GetEnvironmentVariable("SMARTTOUR_PUBLIC_BASE_URL"));
        AddCandidate(candidates, _runtimeConfiguration.BaseUrl);
        AddCandidate(candidates, _runtimeConfiguration.PublicBaseUrl);
        AddCandidate(candidates, Preferences.Default.Get(LastKnownWorkingBaseUrlPreferenceKey, string.Empty));

        foreach (var fallbackBaseUrl in _runtimeConfiguration.FallbackBaseUrls)
        {
            AddCandidate(candidates, fallbackBaseUrl);
        }

        if (_runtimeConfiguration.AllowLocalhostFallback)
        {
            foreach (var fallbackBaseUrl in GetDefaultFallbackBaseUrls())
            {
                AddCandidate(candidates, fallbackBaseUrl);
            }
        }

        return candidates;
    }

    private static async Task<bool> CanReachBaseUrlAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(new Uri(baseUrl, UriKind.Absolute), ApiRoutes.PublicServerInfo));

            if (IsNgrokHost(request.RequestUri.Host))
            {
                request.Headers.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
            }

            using var response = await ProbeHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine($"[MobileApi] API probe failed for {baseUrl}: {ex.Message}");
            return false;
        }
    }

    private static void AddCandidate(List<string> candidates, string? baseUrl)
    {
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
        if (normalizedBaseUrl is null)
        {
            return;
        }

        if (!candidates.Contains(normalizedBaseUrl, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(normalizedBaseUrl);
        }
    }

    private static string? NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri.AbsoluteUri
            : $"{uri.AbsoluteUri}/";
    }

    private static void UpdateCurrentBaseUrl(string baseUrl)
    {
        lock (StateLock)
        {
            _currentBaseUrl = baseUrl;
        }
    }

    private static string GetDefaultBaseUrl() =>
        GetDefaultFallbackBaseUrls().First();

    private static IReadOnlyList<string> GetDefaultFallbackBaseUrls()
    {
        return
        [
            "https://flirt-zeppelin-dimness.ngrok-free.dev/"
        ];
    }

    private static bool IsLoopbackDevelopmentHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "10.0.2.2", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        return IPAddress.IsLoopback(address) || IsPrivateIpv4(address);
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
