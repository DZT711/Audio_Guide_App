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
    public const string ActivityLogs = "ActivityLog";

    public static string GetPublicLocationAudio(int locationId) =>
        $"Audio/public/location/{locationId}";

    public static string GetPublicLocationDefaultAudio(int locationId) =>
        $"Audio/public/location/{locationId}/default";
}
