namespace WebApplication_API.Services;

public sealed class RoutePlanningOptions
{
    public const string SectionName = "RoutePlanning";

    public string BaseUrl { get; set; } = "https://routing.openstreetmap.de/routed-foot/";

    public List<string> FallbackBaseUrls { get; set; } = ["http://routing.openstreetmap.de/routed-foot/"];

    public string WalkingProfile { get; set; } = "walking";

    public int RequestTimeoutSeconds { get; set; } = 20;
}
