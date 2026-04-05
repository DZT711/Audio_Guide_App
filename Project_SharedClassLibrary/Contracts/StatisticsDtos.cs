namespace Project_SharedClassLibrary.Contracts;

public sealed class StatisticsQueryDto
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int? TourId { get; init; }
    public string? Ward { get; init; }
    public string? Search { get; init; }
}

public sealed class StatisticsSummaryDto
{
    public int TotalPlaybackEvents { get; init; }
    public int TotalTrackingPoints { get; init; }
    public int RouteSessions { get; init; }
    public int UniqueGuests { get; init; }
    public int VisiblePois { get; init; }
    public double AverageListeningSeconds { get; init; }
}

public sealed class StatisticsFilterOptionDto
{
    public string Value { get; init; } = "";
    public string Label { get; init; } = "";
    public int Count { get; init; }
}

public sealed class StatisticsChartPointDto
{
    public string Label { get; init; } = "";
    public double Value { get; init; }
    public string? Hint { get; init; }
}

public sealed class StatisticsLocationDto
{
    public int LocationId { get; init; }
    public string Name { get; init; } = "";
    public string? PreferenceImageUrl { get; init; }
    public string? OwnerName { get; init; }
    public string Ward { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public IReadOnlyList<string> TourNames { get; init; } = [];
}

public sealed class StatisticsHeatPointDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int Intensity { get; init; }
    public int SessionCount { get; init; }
    public string Ward { get; init; } = "";
}

public sealed class StatisticsRoutePointDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public DateTime CapturedAt { get; init; }
}

public sealed class StatisticsRouteHistoryDto
{
    public string SessionKey { get; init; } = "";
    public string? DeviceId { get; init; }
    public string? SessionId { get; init; }
    public string PrimaryWard { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public int TrackingPointCount { get; init; }
    public int PlaybackCount { get; init; }
    public double RouteDistanceKm { get; init; }
    public double AverageAccuracyMeters { get; init; }
    public IReadOnlyList<string> LocationNames { get; init; } = [];
    public IReadOnlyList<string> AudioTitles { get; init; } = [];
    public IReadOnlyList<string> TourNames { get; init; } = [];
    public IReadOnlyList<StatisticsRoutePointDto> Points { get; init; } = [];
}

public sealed class StatisticsPoiReportRowDto
{
    public int LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public string? PreferenceImageUrl { get; init; }
    public string? OwnerName { get; init; }
    public string Ward { get; init; } = "";
    public int PlayCount { get; init; }
    public double AverageListeningSeconds { get; init; }
    public int ListeningSamples { get; init; }
    public string? TopAudioTitle { get; init; }
    public int UniqueGuests { get; init; }
    public IReadOnlyList<string> TourNames { get; init; } = [];
}

public sealed class StatisticsOverviewDto
{
    public StatisticsQueryDto AppliedFilters { get; init; } = new();
    public bool IsOwnerScoped { get; init; }
    public string ScopeLabel { get; init; } = "";
    public StatisticsSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<StatisticsFilterOptionDto> TourOptions { get; init; } = [];
    public IReadOnlyList<StatisticsFilterOptionDto> WardOptions { get; init; } = [];
    public IReadOnlyList<StatisticsChartPointDto> PlaybackTimeline { get; init; } = [];
    public IReadOnlyList<StatisticsChartPointDto> PlaysByWard { get; init; } = [];
    public IReadOnlyList<StatisticsChartPointDto> PlaysByTour { get; init; } = [];
    public IReadOnlyList<StatisticsLocationDto> Locations { get; init; } = [];
    public IReadOnlyList<StatisticsHeatPointDto> HeatmapPoints { get; init; } = [];
    public IReadOnlyList<StatisticsRouteHistoryDto> RouteHistory { get; init; } = [];
    public IReadOnlyList<StatisticsPoiReportRowDto> TopPoisByPlayCount { get; init; } = [];
    public IReadOnlyList<StatisticsPoiReportRowDto> AverageListeningByPoi { get; init; } = [];
}
