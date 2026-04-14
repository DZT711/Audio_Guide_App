using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Storage;

namespace MauiApp_Mobile.Services;

public static class MobileApiOptions
{
    private const string ApiBaseUrlPreferenceKey = "mobile_api_base_url";
    private const string LastKnownWorkingBaseUrlPreferenceKey = "mobile_api_last_working_base_url";
    private const int DefaultApiPort = 5123;
    private const string DefaultAndroidEmulatorBaseUrl = "http://10.0.2.2:5123/";
    private const string DefaultDesktopBaseUrl = "http://localhost:5123/";
    private const string PlaceholderBaseUrl = "http://smarttour.invalid/";
    private static readonly SemaphoreSlim DiscoverySemaphore = new(1, 1);

    public static string BaseUrl => GetRequestBaseUrl();
    public static Uri BaseUri => new(GetRequestBaseUrl(), UriKind.Absolute);
    public static Uri PlaceholderBaseUri => new(PlaceholderBaseUrl, UriKind.Absolute);

    public static void SetBaseUrl(string baseUrl) =>
        Preferences.Default.Set(ApiBaseUrlPreferenceKey, NormalizeBaseUrl(baseUrl));

    public static void SetLastKnownWorkingBaseUrl(string baseUrl) =>
        Preferences.Default.Set(LastKnownWorkingBaseUrlPreferenceKey, NormalizeBaseUrl(baseUrl));

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

    public static async Task<string> EnsureResolvedBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!NeedsSubnetDiscovery())
        {
            return GetRequestBaseUrl();
        }

        await TryDiscoverAndRememberBaseUrlAsync(cancellationToken);
        return GetRequestBaseUrl();
    }

    public static async Task<string?> TryDiscoverAndRememberBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!NeedsSubnetDiscovery())
        {
            return GetStoredBaseUrlOrNull();
        }

        await DiscoverySemaphore.WaitAsync(cancellationToken);
        try
        {
            var existingBaseUrl = GetStoredBaseUrlOrNull();
            if (!string.IsNullOrWhiteSpace(existingBaseUrl))
            {
                return existingBaseUrl;
            }

            var subnetPrefix = GetCurrentPrivateSubnetPrefix();
            if (string.IsNullOrWhiteSpace(subnetPrefix))
            {
                return null;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(6));
            using var probeClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(550)
            };

            var hostCandidates = BuildHostScanOrder(subnetPrefix, GetCurrentDeviceIpv4()).ToList();
            var maxParallelism = 18;
            string? discoveredBaseUrl = null;

            await Parallel.ForEachAsync(
                hostCandidates,
                new ParallelOptions
                {
                    CancellationToken = timeoutCts.Token,
                    MaxDegreeOfParallelism = maxParallelism
                },
                async (host, probeCancellationToken) =>
                {
                    if (!string.IsNullOrWhiteSpace(discoveredBaseUrl))
                    {
                        return;
                    }

                    var candidateBaseUrl = $"http://{host}:{DefaultApiPort}/";
                    var requestUri = new Uri(new Uri(candidateBaseUrl, UriKind.Absolute), ApiRoutes.PublicServerInfo);

                    try
                    {
                        using var response = await probeClient.GetAsync(requestUri, probeCancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            return;
                        }

                        await using var responseStream = await response.Content.ReadAsStreamAsync(probeCancellationToken);
                        var payload = await JsonSerializer.DeserializeAsync<PublicServerInfoDto>(responseStream, cancellationToken: probeCancellationToken);
                        if (!string.Equals(payload?.Status, "Online", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        if (Interlocked.CompareExchange(ref discoveredBaseUrl, candidateBaseUrl, null) is null)
                        {
                            SetLastKnownWorkingBaseUrl(candidateBaseUrl);
                            timeoutCts.Cancel();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch
                    {
                    }
                });

            return discoveredBaseUrl;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            DiscoverySemaphore.Release();
        }
    }

    public static string ResolveImageUrl(string? imageUrl) =>
        ResolveArchiveUrl(imageUrl, SharedStoragePaths.NormalizePublicImagePath);

    public static string ResolveAudioUrl(string? audioUrl) =>
        ResolveArchiveUrl(audioUrl, SharedStoragePaths.NormalizePublicAudioPath);

    private static string GetRequestBaseUrl()
    {
        var storedBaseUrl = GetStoredBaseUrlOrNull();
        if (!string.IsNullOrWhiteSpace(storedBaseUrl))
        {
            return storedBaseUrl;
        }

        return GetDefaultBaseUrl();
    }

    private static string? GetStoredBaseUrlOrNull()
    {
        var configuredBaseUrl = Preferences.Default.Get(ApiBaseUrlPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return NormalizeBaseUrl(configuredBaseUrl);
        }

        var lastKnownWorkingBaseUrl = Preferences.Default.Get(LastKnownWorkingBaseUrlPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(lastKnownWorkingBaseUrl))
        {
            return NormalizeBaseUrl(lastKnownWorkingBaseUrl);
        }

        return null;
    }

    private static string GetDefaultBaseUrl()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return DeviceInfo.DeviceType == DeviceType.Virtual
                ? DefaultAndroidEmulatorBaseUrl
                : DefaultDesktopBaseUrl;
        }

        return DefaultDesktopBaseUrl;
    }

    private static bool NeedsSubnetDiscovery() =>
        DeviceInfo.Platform == DevicePlatform.Android &&
        DeviceInfo.DeviceType != DeviceType.Virtual &&
        string.IsNullOrWhiteSpace(GetStoredBaseUrlOrNull());

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var fallback = GetDefaultBaseUrl();

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

    private static IEnumerable<string> BuildHostScanOrder(string subnetPrefix, string? deviceIpv4)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var priorityLastOctets = new[] { 1, 2, 3, 4, 5, 10, 11, 20, 50, 100, 101, 110, 120, 150, 200, 254 };

        foreach (var lastOctet in priorityLastOctets)
        {
            var candidate = $"{subnetPrefix}.{lastOctet}";
            if (string.Equals(candidate, deviceIpv4, StringComparison.OrdinalIgnoreCase) || !seen.Add(candidate))
            {
                continue;
            }

            yield return candidate;
        }

        for (var lastOctet = 1; lastOctet <= 254; lastOctet++)
        {
            var candidate = $"{subnetPrefix}.{lastOctet}";
            if (string.Equals(candidate, deviceIpv4, StringComparison.OrdinalIgnoreCase) || !seen.Add(candidate))
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static string? GetCurrentPrivateSubnetPrefix()
    {
        var deviceIpv4 = GetCurrentDeviceIpv4();
        if (string.IsNullOrWhiteSpace(deviceIpv4))
        {
            return null;
        }

        var parts = deviceIpv4.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 4 ? string.Join(".", parts.Take(3)) : null;
    }

    private static string? GetCurrentDeviceIpv4()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPInterfaceProperties? properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (var addressInformation in properties.UnicastAddresses)
            {
                var address = addressInformation.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork ||
                    IPAddress.IsLoopback(address))
                {
                    continue;
                }

                var value = address.ToString();
                if (IsPrivateIpv4(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static bool IsPrivateIpv4(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        return address.StartsWith("10.", StringComparison.Ordinal) ||
               address.StartsWith("192.168.", StringComparison.Ordinal) ||
               address.StartsWith("172.16.", StringComparison.Ordinal) ||
               address.StartsWith("172.17.", StringComparison.Ordinal) ||
               address.StartsWith("172.18.", StringComparison.Ordinal) ||
               address.StartsWith("172.19.", StringComparison.Ordinal) ||
               address.StartsWith("172.20.", StringComparison.Ordinal) ||
               address.StartsWith("172.21.", StringComparison.Ordinal) ||
               address.StartsWith("172.22.", StringComparison.Ordinal) ||
               address.StartsWith("172.23.", StringComparison.Ordinal) ||
               address.StartsWith("172.24.", StringComparison.Ordinal) ||
               address.StartsWith("172.25.", StringComparison.Ordinal) ||
               address.StartsWith("172.26.", StringComparison.Ordinal) ||
               address.StartsWith("172.27.", StringComparison.Ordinal) ||
               address.StartsWith("172.28.", StringComparison.Ordinal) ||
               address.StartsWith("172.29.", StringComparison.Ordinal) ||
               address.StartsWith("172.30.", StringComparison.Ordinal) ||
               address.StartsWith("172.31.", StringComparison.Ordinal);
    }

    private static bool IsLoopbackDevelopmentHost(string? host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "10.0.2.2", StringComparison.OrdinalIgnoreCase);
}
