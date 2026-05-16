using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Project_SharedClassLibrary.Constants;

namespace MauiApp_Mobile.Services;

public sealed class QrDeepLinkService
{
    public const string SmartTourScheme = "smarttour";
    public const string SmartTourHost = "play";

    private static readonly TimeSpan DuplicateScanWindow = TimeSpan.FromMilliseconds(1200);
    private readonly object _syncRoot = new();
    private DeepLinkResult? _pendingDeepLink;
    private string? _lastProcessedRawContent;
    private DateTimeOffset _lastProcessedAt = DateTimeOffset.MinValue;
    private int _isHandlingPending;

    public static QrDeepLinkService Instance { get; } = new();

    private QrDeepLinkService()
    {
    }

    public DeepLinkResult? ParseDeepLink(string? rawContent)
    {
        var normalizedContent = rawContent?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return null;
        }

        lock (_syncRoot)
        {
            if (string.Equals(_lastProcessedRawContent, normalizedContent, StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.UtcNow - _lastProcessedAt < DuplicateScanWindow)
            {
                System.Diagnostics.Debug.WriteLine("[QrDeepLink] Duplicate scan ignored.");
                return null;
            }

            _lastProcessedRawContent = normalizedContent;
            _lastProcessedAt = DateTimeOffset.UtcNow;
        }

        if (!Uri.TryCreate(normalizedContent, UriKind.Absolute, out var uri))
        {
            return DeepLinkResult.Invalid(normalizedContent, "Deep link is not a valid absolute URI.");
        }

        if (TryParseLocationLink(uri, out var locationResult))
        {
            return locationResult;
        }

        if (TryParsePublicLandingLink(uri, out var landingResult))
        {
            return landingResult;
        }

        if (IsGooglePlayLink(uri))
        {
            return DeepLinkResult.Valid(DeepLinkType.GooglePlayStore, normalizedContent);
        }

        if (IsAppleStoreLink(uri))
        {
            return DeepLinkResult.Valid(DeepLinkType.AppStore, normalizedContent);
        }

        return DeepLinkResult.Invalid(normalizedContent, "QR content is not a supported SmartTour link.");
    }

    public void QueuePendingDeepLink(string? rawContent)
    {
        var parsedDeepLink = ParseDeepLink(rawContent);
        if (parsedDeepLink is null)
        {
            return;
        }

        lock (_syncRoot)
        {
            _pendingDeepLink = parsedDeepLink;
        }
    }

    public async Task<bool> TryHandlePendingAsync()
    {
        if (Interlocked.CompareExchange(ref _isHandlingPending, 1, 0) == 1)
        {
            return false;
        }

        try
        {
            DeepLinkResult? pendingDeepLink;
            lock (_syncRoot)
            {
                pendingDeepLink = _pendingDeepLink;
            }

            if (pendingDeepLink is null)
            {
                return false;
            }

            var handleStatus = await HandleDeepLinkAsync(pendingDeepLink);
            if (handleStatus != DeepLinkHandleStatus.Deferred)
            {
                ClearPendingDeepLink(pendingDeepLink.RawContent);
            }

            return handleStatus == DeepLinkHandleStatus.Handled;
        }
        finally
        {
            Interlocked.Exchange(ref _isHandlingPending, 0);
        }
    }

    public async Task<DeepLinkHandleStatus> HandleDeepLinkAsync(DeepLinkResult? deepLink)
    {
        if (deepLink is null)
        {
            return DeepLinkHandleStatus.Rejected;
        }

        if (!deepLink.IsValid)
        {
            System.Diagnostics.Debug.WriteLine($"[QrDeepLink] Rejecting invalid link: {deepLink.ErrorMessage}");
            return DeepLinkHandleStatus.Rejected;
        }

        try
        {
            return deepLink.Type switch
            {
                DeepLinkType.LocationPlay => await NavigateToLocationAsync(deepLink),
                DeepLinkType.GooglePlayStore or DeepLinkType.AppStore => await OpenExternalUrlAsync(deepLink.RawContent),
                _ => DeepLinkHandleStatus.Rejected
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrDeepLink] Handling failed: {ex}");
            return DeepLinkHandleStatus.Rejected;
        }
    }

    private async Task<DeepLinkHandleStatus> NavigateToLocationAsync(DeepLinkResult deepLink)
    {
        if (!deepLink.LocationId.HasValue || deepLink.LocationId.Value <= 0)
        {
            return DeepLinkHandleStatus.Rejected;
        }

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var appShell = ResolveAppShell();
            if (appShell is null)
            {
                System.Diagnostics.Debug.WriteLine("[QrDeepLink] Shell is not ready yet. Deferring navigation.");
                return DeepLinkHandleStatus.Deferred;
            }

            ApplyLanguageOverride(deepLink.LanguageCode);
            PlaceNavigationService.Instance.RequestPlaceDetail(
                deepLink.LocationId.Value.ToString(CultureInfo.InvariantCulture),
                deepLink.Autoplay,
                deepLink.AudioTrackId);

            await appShell.NavigateToPlacesTabAsync();
            System.Diagnostics.Debug.WriteLine(
                $"[QrDeepLink] Routed to location {deepLink.LocationId.Value} (autoplay={deepLink.Autoplay}, audioTrackId={deepLink.AudioTrackId?.ToString() ?? "default"}).");
            return DeepLinkHandleStatus.Handled;
        });
    }

    private static async Task<DeepLinkHandleStatus> OpenExternalUrlAsync(string rawContent)
    {
        if (!Uri.TryCreate(rawContent, UriKind.Absolute, out var uri))
        {
            return DeepLinkHandleStatus.Rejected;
        }

        await Launcher.Default.OpenAsync(uri);
        System.Diagnostics.Debug.WriteLine($"[QrDeepLink] Opened external URI: {uri}");
        return DeepLinkHandleStatus.Handled;
    }

    private static bool TryParseLocationLink(Uri uri, out DeepLinkResult result)
    {
        result = DeepLinkResult.Invalid(uri.AbsoluteUri, "QR content is not a supported SmartTour link.");

        if (!string.Equals(uri.Scheme, SmartTourScheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, SmartTourHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var trimmedPath = uri.AbsolutePath.Trim('/');
        var queryParameters = ParseQuery(uri.Query);

        if (trimmedPath.StartsWith("location/", StringComparison.OrdinalIgnoreCase)
            || trimmedPath.StartsWith("poi/", StringComparison.OrdinalIgnoreCase))
        {
            var prefixLength = trimmedPath.StartsWith("location/", StringComparison.OrdinalIgnoreCase)
                ? "location/".Length
                : "poi/".Length;
            var idSegment = trimmedPath[prefixLength..].Trim('/');
            if (!int.TryParse(idSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId)
                || locationId <= 0)
            {
                result = DeepLinkResult.Invalid(uri.AbsoluteUri, "Location deep link is missing a valid identifier.");
                return true;
            }

            queryParameters.TryGetValue("language", out var languageCode);
            result = DeepLinkResult.Valid(
                DeepLinkType.LocationPlay,
                uri.AbsoluteUri,
                locationId,
                NormalizeLanguageCode(languageCode),
                ParseAutoplay(queryParameters),
                ParseAudioTrackId(queryParameters));
            return true;
        }

        if (string.Equals(trimmedPath, "place", StringComparison.OrdinalIgnoreCase))
        {
            if (!queryParameters.TryGetValue("id", out var idValue)
                || !int.TryParse(idValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId)
                || locationId <= 0)
            {
                result = DeepLinkResult.Invalid(uri.AbsoluteUri, "Place deep link is missing a valid identifier.");
                return true;
            }

            queryParameters.TryGetValue("language", out var languageCode);
            result = DeepLinkResult.Valid(
                DeepLinkType.LocationPlay,
                uri.AbsoluteUri,
                locationId,
                NormalizeLanguageCode(languageCode),
                ParseAutoplay(queryParameters),
                ParseAudioTrackId(queryParameters));
            return true;
        }

        result = DeepLinkResult.Invalid(uri.AbsoluteUri, "SmartTour deep link path is not supported.");
        return true;
    }

    private static bool TryParsePublicLandingLink(Uri uri, out DeepLinkResult result)
    {
        result = DeepLinkResult.Invalid(uri.AbsoluteUri, "QR content is not a supported SmartTour landing link.");
        if (!ApiRoutes.TryParsePublicLocationQrLandingPath(uri.AbsolutePath, out var locationId))
        {
            return false;
        }

        var queryParameters = ParseQuery(uri.Query);
        queryParameters.TryGetValue("language", out var languageCode);
        result = DeepLinkResult.Valid(
            DeepLinkType.LocationPlay,
            uri.AbsoluteUri,
            locationId,
            NormalizeLanguageCode(languageCode),
            ParseAutoplay(queryParameters),
            ParseAudioTrackId(queryParameters));
        return true;
    }

    private static bool IsGooglePlayLink(Uri uri) =>
        string.Equals(uri.Host, "play.google.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith("/store/apps/details", StringComparison.OrdinalIgnoreCase);

    private static bool IsAppleStoreLink(Uri uri) =>
        uri.Host.EndsWith("apps.apple.com", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return values;
        }

        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = parts.Length > 1
                ? Uri.UnescapeDataString(parts[1]).Trim()
                : string.Empty;

            values[key] = value;
        }

        return values;
    }

    private static bool ParseAutoplay(IReadOnlyDictionary<string, string> queryParameters)
    {
        if (!queryParameters.TryGetValue("autoplay", out var rawValue))
        {
            return false;
        }

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "y" => true,
            _ => false
        };
    }

    private static int? ParseAudioTrackId(IReadOnlyDictionary<string, string> queryParameters)
    {
        if (TryReadPositiveInt(queryParameters, "audioTrackId", out var audioTrackId))
        {
            return audioTrackId;
        }

        if (TryReadPositiveInt(queryParameters, "audioTrack", out audioTrackId))
        {
            return audioTrackId;
        }

        return null;
    }

    private static bool TryReadPositiveInt(
        IReadOnlyDictionary<string, string> queryParameters,
        string key,
        out int value)
    {
        value = 0;
        return queryParameters.TryGetValue(key, out var rawValue)
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    private static string? NormalizeLanguageCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            var language when language.StartsWith("vi", StringComparison.Ordinal) => "vi",
            var language when language.StartsWith("en", StringComparison.Ordinal) => "en",
            var language when language.StartsWith("fr", StringComparison.Ordinal) => "fr",
            var language when language.StartsWith("ja", StringComparison.Ordinal) || language.StartsWith("jp", StringComparison.Ordinal) => "jp",
            var language when language.StartsWith("ko", StringComparison.Ordinal) || language.StartsWith("kr", StringComparison.Ordinal) => "kr",
            var language when language.StartsWith("zh", StringComparison.Ordinal) || language.StartsWith("cn", StringComparison.Ordinal) => "cn",
            _ => null
        };
    }

    private static void ApplyLanguageOverride(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return;
        }

        LocalizationService.Instance.Language = languageCode;
    }

    private static AppShell? ResolveAppShell()
    {
        if (Shell.Current is AppShell currentShell)
        {
            return currentShell;
        }

        return Application.Current?.Windows.FirstOrDefault()?.Page as AppShell;
    }

    private void ClearPendingDeepLink(string rawContent)
    {
        lock (_syncRoot)
        {
            if (_pendingDeepLink is null)
            {
                return;
            }

            if (string.Equals(_pendingDeepLink.RawContent, rawContent, StringComparison.OrdinalIgnoreCase))
            {
                _pendingDeepLink = null;
            }
        }
    }
}

public sealed class DeepLinkResult
{
    public DeepLinkType Type { get; init; }
    public string RawContent { get; init; } = string.Empty;
    public int? LocationId { get; init; }
    public string? LanguageCode { get; init; }
    public bool Autoplay { get; init; }
    public int? AudioTrackId { get; init; }
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static DeepLinkResult Valid(
        DeepLinkType type,
        string rawContent,
        int? locationId = null,
        string? languageCode = null,
        bool autoplay = false,
        int? audioTrackId = null) =>
        new()
        {
            Type = type,
            RawContent = rawContent,
            LocationId = locationId,
            LanguageCode = languageCode,
            Autoplay = autoplay,
            AudioTrackId = audioTrackId,
            IsValid = true
        };

    public static DeepLinkResult Invalid(string rawContent, string errorMessage) =>
        new()
        {
            Type = DeepLinkType.Unknown,
            RawContent = rawContent,
            ErrorMessage = errorMessage,
            IsValid = false
        };
}

public enum DeepLinkType
{
    Unknown = 0,
    LocationPlay = 1,
    GooglePlayStore = 2,
    AppStore = 3
}

public enum DeepLinkHandleStatus
{
    Handled = 0,
    Deferred = 1,
    Rejected = 2
}
