namespace Project_SharedClassLibrary.Constants;

public static class ApiRoutes
{
    public const string Categories = "Category";
    public const string PublicCategories = "Category/public";
    public const string Languages = "Language";
    public const string Locations = "Location";
    public const string PublicLocations = "Location/public";
    public const string PublicCatalog = "Location/public/catalog";
    public const string Tours = "Tour";
    public const string PublicTours = "Tour/public";
    public const string ToursPreview = "Tour/preview";
    public const string Audio = "Audio";
    public const string PublicLocationAudioTemplate = "Audio/public/location/{0}";
    public const string PublicLocationDefaultAudioTemplate = "Audio/public/location/{0}/default";
    public const string PublicTranslateTts = "Audio/public/translate-tts";
    public const string AudioTtsPreview = "Audio/preview-tts";
    public const string UsageHistory = "Usage";
    public const string AuthLogin = "Auth/login";
    public const string AuthLogout = "Auth/logout";
    public const string AuthMe = "Auth/me";
    public const string Users = "DashboardUser";
    public const string UserInvite = "DashboardUser/invite";
    public const string DashboardOverview = "Dashboard/overview";
    public const string DashboardSnapshot = "Dashboard/snapshot";
    public const string ServerInfo = "System/info";
    public const string PublicServerInfo = "System/public/info";
    public const string Moderation = "Moderation";
    public const string ChangeRequests = "ChangeRequest";
    public const string Inbox = "Inbox";
    public const string Statistics = "Statistics";
    public const string StatisticsTopPois = "Statistics/top-pois";
    public const string StatisticsAverageListening = "Statistics/average-listening";
    public const string StatisticsHeatmap = "Statistics/heatmap";
    public const string ActivityLogs = "ActivityLog";
    public const string LocationQr = "LocationQr";
    public const string TelemetryIngestRouteHistoryV1 = "Telemetry/v1/route-history";
    public const string TelemetryIngestAudioPlayEventsV1 = "Telemetry/v1/audio-play-events";
    public const string TelemetryIngestAudioListeningSessionsV1 = "Telemetry/v1/audio-listening-sessions";
    public const string PublicLocationQrDownloadPage = "LocationQr/public/download";
    public const string PublicAndroidApkDownload = "LocationQr/public/android-apk";
    public const string PublicAndroidApkQr = "LocationQr/public/android-apk/qr";

    public static string GetPublicLocationAudio(int locationId) =>
        $"Audio/public/location/{locationId}";

    public static string GetPublicLocationDefaultAudio(int locationId) =>
        $"Audio/public/location/{locationId}/default";

    public static string GetLocationQrStatus(int locationId) =>
        $"{LocationQr}/location/{locationId}/status";

    public static string GetLocationQrGenerate(int locationId) =>
        $"{LocationQr}/location/{locationId}/generate";

    public static string GetLocationQrBulkGenerate() =>
        $"{LocationQr}/bulk/generate";

    public static string GetPublicLocationQrLanding(int locationId) =>
        $"{LocationQr}/public/location/{locationId}";

    public static string GetPublicLocationQrDownloadPage(
        int? locationId = null,
        bool autoplay = true,
        int? audioTrackId = null)
    {
        var querySegments = new List<string>();
        if (locationId is > 0)
        {
            querySegments.Add($"locationId={locationId.Value}");
        }

        if (autoplay)
        {
            querySegments.Add("autoplay=true");
        }

        if (audioTrackId is > 0)
        {
            querySegments.Add($"audioTrackId={audioTrackId.Value}");
        }

        return querySegments.Count == 0
            ? PublicLocationQrDownloadPage
            : $"{PublicLocationQrDownloadPage}?{string.Join("&", querySegments)}";
    }

    public static bool TryParsePublicLocationQrLandingPath(string? path, out int locationId)
    {
        locationId = 0;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path
            .Trim()
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 4)
        {
            return false;
        }

        for (var index = 0; index <= segments.Length - 4; index++)
        {
            if (!string.Equals(segments[index], LocationQr, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(segments[index + 1], "public", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(segments[index + 2], "location", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(segments[index + 3], out locationId) && locationId > 0)
            {
                return true;
            }
        }

        locationId = 0;
        return false;
    }
}
