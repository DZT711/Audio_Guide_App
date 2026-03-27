namespace WebApplication_API.Services;

public sealed class RoutePlanningOptions
{
    public const string SectionName = "RoutePlanning";

    public string BaseUrl { get; set; } = "https://router.project-osrm.org/";

    public string WalkingProfile { get; set; } = "foot";

    public int RequestTimeoutSeconds { get; set; } = 20;
}
