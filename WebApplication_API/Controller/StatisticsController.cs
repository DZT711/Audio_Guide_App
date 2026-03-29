using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class StatisticsController(
    DBContext context,
    AdminRequestAuthorizationService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStatistics([FromQuery] StatisticsQueryDto query)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AnalyticsView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var currentUser = access.User!;
        var ownerScoped = IsOwnerScoped(currentUser);
        var locations = await BuildLocationScopeQuery(currentUser)
            .Include(item => item.Owner)
            .OrderBy(item => item.Name)
            .ToListAsync();
        var locationIds = locations
            .Select(item => item.LocationId)
            .ToHashSet();

        var audios = await context.AudioContents
            .AsNoTracking()
            .Where(item => locationIds.Contains(item.LocationId))
            .OrderBy(item => item.Title)
            .ToListAsync();

        var tourLinks = await context.TourLocations
            .AsNoTracking()
            .Include(item => item.Tour)
            .Where(item => locationIds.Contains(item.LocationId) && item.Tour != null)
            .ToListAsync();

        var wardLookup = locations.ToDictionary(
            item => item.LocationId,
            item => ExtractWard(item.Address));

        var tourNamesByLocation = tourLinks
            .Where(item => item.Tour is not null)
            .GroupBy(item => item.LocationId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(item => item.Tour!.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var filteredLocationIds = FilterLocationIds(query, locations, audios, tourLinks, wardLookup, tourNamesByLocation);
        var scopedLocationIds = HasContextFilters(query) ? filteredLocationIds : locationIds;

        var playbackItems = await BuildPlaybackQuery(query, scopedLocationIds)
            .Include(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .Include(item => item.Audio)
            .OrderByDescending(item => item.EventAt)
            .ToListAsync();

        var playbackBySession = playbackItems
            .GroupBy(GetSessionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PlaybackEvent>)group
                    .OrderBy(item => item.EventAt)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var trackingItems = await BuildTrackingQuery(query)
            .OrderByDescending(item => item.CapturedAt)
            .ToListAsync();

        if (ownerScoped || HasContextFilters(query))
        {
            var allowedSessionKeys = playbackBySession.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            trackingItems = trackingItems
                .Where(item => allowedSessionKeys.Contains(GetSessionKey(item)))
                .ToList();
        }

        var routeHistory = BuildRouteHistory(
            trackingItems,
            playbackBySession,
            wardLookup,
            tourNamesByLocation,
            locations);

        var routeLookup = routeHistory.ToDictionary(
            item => item.SessionKey,
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var playCountItems = SelectPlayCountEvents(playbackItems);
        var filteredLocations = locations
            .Where(item => scopedLocationIds.Contains(item.LocationId))
            .ToList();

        var listeningSamples = playbackItems
            .Where(item => item.ListeningSeconds.HasValue && item.ListeningSeconds.Value > 0)
            .Select(item => item.ListeningSeconds!.Value)
            .ToList();

        var guestKeys = playbackItems
            .Select(item => GetGuestKey(item.SessionId, item.DeviceId))
            .Concat(trackingItems.Select(item => GetGuestKey(item.SessionId, item.DeviceId)))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return Ok(new StatisticsOverviewDto
        {
            AppliedFilters = new StatisticsQueryDto
            {
                From = query.From?.Date,
                To = query.To?.Date,
                TourId = query.TourId,
                Ward = query.Ward,
                Search = query.Search
            },
            IsOwnerScoped = ownerScoped,
            ScopeLabel = ownerScoped
                ? "Showing analytics for POIs owned by your account."
                : "Showing analytics across all accessible POIs and telemetry sessions.",
            Summary = new StatisticsSummaryDto
            {
                TotalPlaybackEvents = playbackItems.Count,
                TotalTrackingPoints = trackingItems.Count,
                RouteSessions = routeHistory.Count,
                UniqueGuests = guestKeys,
                VisiblePois = filteredLocations.Count,
                AverageListeningSeconds = listeningSamples.Count == 0
                    ? 0d
                    : Math.Round(listeningSamples.Average(), 1, MidpointRounding.AwayFromZero)
            },
            TourOptions = BuildTourOptions(tourLinks),
            WardOptions = BuildWardOptions(locations, wardLookup),
            PlaybackTimeline = BuildPlaybackTimeline(playCountItems, trackingItems, query),
            PlaysByWard = BuildPlaysByWard(playCountItems, wardLookup),
            PlaysByTour = BuildPlaysByTour(playCountItems, tourNamesByLocation),
            Locations = filteredLocations
                .Select(item => new StatisticsLocationDto
                {
                    LocationId = item.LocationId,
                    Name = item.Name,
                    OwnerName = item.Owner?.FullName ?? item.Owner?.Username,
                    Ward = wardLookup[item.LocationId],
                    Latitude = item.Latitude,
                    Longitude = item.Longitude,
                    TourNames = tourNamesByLocation.TryGetValue(item.LocationId, out var tourNames)
                        ? tourNames
                        : []
                })
                .ToList(),
            HeatmapPoints = BuildHeatmapPoints(trackingItems, routeLookup, locations),
            RouteHistory = routeHistory,
            TopPoisByPlayCount = BuildTopPoiReport(playCountItems, playbackItems, wardLookup, tourNamesByLocation),
            AverageListeningByPoi = BuildAverageListeningReport(playbackItems, wardLookup, tourNamesByLocation)
        });
    }

    private IQueryable<Location> BuildLocationScopeQuery(DashboardUser currentUser)
    {
        var query = context.Locations
            .AsNoTracking()
            .AsQueryable();

        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.OwnerId == currentUser.UserId)
            : query;
    }

    private IQueryable<PlaybackEvent> BuildPlaybackQuery(
        StatisticsQueryDto query,
        IReadOnlyCollection<int> scopedLocationIds)
    {
        var items = context.PlaybackEvents
            .AsNoTracking()
            .Where(item => item.LocationId.HasValue && scopedLocationIds.Contains(item.LocationId.Value));

        if (query.From.HasValue)
        {
            var fromInclusive = query.From.Value.Date;
            items = items.Where(item => item.EventAt >= fromInclusive);
        }

        if (query.To.HasValue)
        {
            var toExclusive = query.To.Value.Date.AddDays(1);
            items = items.Where(item => item.EventAt < toExclusive);
        }

        return items;
    }

    private IQueryable<LocationTrackingEvent> BuildTrackingQuery(StatisticsQueryDto query)
    {
        var items = context.LocationTrackingEvents
            .AsNoTracking()
            .AsQueryable();

        if (query.From.HasValue)
        {
            var fromInclusive = query.From.Value.Date;
            items = items.Where(item => item.CapturedAt >= fromInclusive);
        }

        if (query.To.HasValue)
        {
            var toExclusive = query.To.Value.Date.AddDays(1);
            items = items.Where(item => item.CapturedAt < toExclusive);
        }

        return items;
    }

    private static HashSet<int> FilterLocationIds(
        StatisticsQueryDto query,
        IReadOnlyCollection<Location> locations,
        IReadOnlyCollection<Audio> audios,
        IReadOnlyCollection<TourLocation> tourLinks,
        IReadOnlyDictionary<int, string> wardLookup,
        IReadOnlyDictionary<int, IReadOnlyList<string>> tourNamesByLocation)
    {
        var filteredIds = locations
            .Select(item => item.LocationId)
            .ToHashSet();

        if (query.TourId.HasValue && query.TourId.Value > 0)
        {
            var tourLocationIds = tourLinks
                .Where(item => item.TourId == query.TourId.Value)
                .Select(item => item.LocationId)
                .ToHashSet();

            filteredIds.IntersectWith(tourLocationIds);
        }

        if (!string.IsNullOrWhiteSpace(query.Ward))
        {
            var wardLocationIds = wardLookup
                .Where(item => string.Equals(item.Value, query.Ward, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Key)
                .ToHashSet();

            filteredIds.IntersectWith(wardLocationIds);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var matchingIds = locations
                .Where(item =>
                    Contains(item.Name, search)
                    || Contains(item.Description, search)
                    || Contains(wardLookup[item.LocationId], search)
                    || (tourNamesByLocation.TryGetValue(item.LocationId, out var tourNames)
                        && tourNames.Any(item => Contains(item, search))))
                .Select(item => item.LocationId)
                .ToHashSet();

            matchingIds.UnionWith(audios
                .Where(item => Contains(item.Title, search) || Contains(item.Description, search))
                .Select(item => item.LocationId));

            filteredIds.IntersectWith(matchingIds);
        }

        return filteredIds;
    }

    private static List<StatisticsFilterOptionDto> BuildTourOptions(IReadOnlyCollection<TourLocation> tourLinks) =>
        tourLinks
            .Where(item => item.Tour is not null)
            .GroupBy(item => new { item.TourId, item.Tour!.Name })
            .OrderBy(item => item.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StatisticsFilterOptionDto
            {
                Value = group.Key.TourId.ToString(),
                Label = group.Key.Name,
                Count = group
                    .Select(item => item.LocationId)
                    .Distinct()
                    .Count()
            })
            .ToList();

    private static List<StatisticsFilterOptionDto> BuildWardOptions(
        IReadOnlyCollection<Location> locations,
        IReadOnlyDictionary<int, string> wardLookup) =>
        locations
            .GroupBy(item => wardLookup[item.LocationId])
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StatisticsFilterOptionDto
            {
                Value = group.Key,
                Label = group.Key,
                Count = group.Count()
            })
            .ToList();

    private static List<StatisticsChartPointDto> BuildPlaybackTimeline(
        IReadOnlyCollection<PlaybackEvent> playCountItems,
        IReadOnlyCollection<LocationTrackingEvent> trackingItems,
        StatisticsQueryDto query)
    {
        var startDate = query.From?.Date;
        var endDate = query.To?.Date;

        if (!startDate.HasValue || !endDate.HasValue)
        {
            var dates = playCountItems
                .Select(item => item.EventAt.Date)
                .Concat(trackingItems.Select(item => item.CapturedAt.Date))
                .ToList();

            if (dates.Count > 0)
            {
                startDate ??= dates.Min();
                endDate ??= dates.Max();
            }
        }

        startDate ??= DateTime.UtcNow.Date.AddDays(-6);
        endDate ??= DateTime.UtcNow.Date;

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var playCountByDate = playCountItems
            .GroupBy(item => item.EventAt.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var results = new List<StatisticsChartPointDto>();
        for (var cursor = startDate.Value.Date; cursor <= endDate.Value.Date; cursor = cursor.AddDays(1))
        {
            playCountByDate.TryGetValue(cursor, out var count);
            results.Add(new StatisticsChartPointDto
            {
                Label = cursor.ToString("dd MMM"),
                Value = count,
                Hint = $"{count} plays on {cursor:dd MMM yyyy}"
            });
        }

        return results;
    }

    private static List<StatisticsChartPointDto> BuildPlaysByWard(
        IReadOnlyCollection<PlaybackEvent> playCountItems,
        IReadOnlyDictionary<int, string> wardLookup) =>
        playCountItems
            .Where(item => item.LocationId.HasValue)
            .GroupBy(item => wardLookup.TryGetValue(item.LocationId!.Value, out var ward) ? ward : "Unassigned")
            .OrderByDescending(item => item.Count())
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(group => new StatisticsChartPointDto
            {
                Label = group.Key,
                Value = group.Count(),
                Hint = $"{group.Count()} plays"
            })
            .ToList();

    private static List<StatisticsChartPointDto> BuildPlaysByTour(
        IReadOnlyCollection<PlaybackEvent> playCountItems,
        IReadOnlyDictionary<int, IReadOnlyList<string>> tourNamesByLocation)
    {
        return playCountItems
            .Where(item => item.LocationId.HasValue)
            .SelectMany(item =>
            {
                if (tourNamesByLocation.TryGetValue(item.LocationId!.Value, out var tourNames) && tourNames.Count > 0)
                {
                    return tourNames;
                }

                return ["No tour"];
            })
            .GroupBy(item => item)
            .OrderByDescending(item => item.Count())
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(group => new StatisticsChartPointDto
            {
                Label = group.Key,
                Value = group.Count(),
                Hint = $"{group.Count()} plays"
            })
            .ToList();
    }

    private static List<StatisticsHeatPointDto> BuildHeatmapPoints(
        IReadOnlyCollection<LocationTrackingEvent> trackingItems,
        IReadOnlyDictionary<string, StatisticsRouteHistoryDto> routeLookup,
        IReadOnlyCollection<Location> locations)
    {
        return trackingItems
            .GroupBy(item => new
            {
                Latitude = Math.Round(item.Latitude, 4),
                Longitude = Math.Round(item.Longitude, 4)
            })
            .Select(group =>
            {
                var sessionKeys = group
                    .Select(GetSessionKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var ward = sessionKeys
                    .Select(item => routeLookup.TryGetValue(item, out var route) ? route.PrimaryWard : null)
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
                    ?? ResolveNearestWard(group.First(), locations);

                return new StatisticsHeatPointDto
                {
                    Latitude = group.Key.Latitude,
                    Longitude = group.Key.Longitude,
                    Intensity = group.Count(),
                    SessionCount = sessionKeys.Count,
                    Ward = ward
                };
            })
            .OrderByDescending(item => item.Intensity)
            .Take(120)
            .ToList();
    }

    private static List<StatisticsRouteHistoryDto> BuildRouteHistory(
        IReadOnlyCollection<LocationTrackingEvent> trackingItems,
        IReadOnlyDictionary<string, IReadOnlyList<PlaybackEvent>> playbackBySession,
        IReadOnlyDictionary<int, string> wardLookup,
        IReadOnlyDictionary<int, IReadOnlyList<string>> tourNamesByLocation,
        IReadOnlyCollection<Location> locations)
    {
        return trackingItems
            .GroupBy(GetSessionKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedPoints = group
                    .OrderBy(item => item.CapturedAt)
                    .ToList();

                playbackBySession.TryGetValue(group.Key, out var relatedPlaybackItems);
                relatedPlaybackItems ??= [];

                var locationNames = relatedPlaybackItems
                    .Select(item => item.Location?.Name)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .Cast<string>()
                    .ToList();

                var audioTitles = relatedPlaybackItems
                    .Select(item => item.Audio?.Title)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .Cast<string>()
                    .ToList();

                var tourNames = relatedPlaybackItems
                    .Where(item => item.LocationId.HasValue && tourNamesByLocation.ContainsKey(item.LocationId.Value))
                    .SelectMany(item => tourNamesByLocation[item.LocationId!.Value])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var routeDistanceKm = CalculateRouteDistanceKm(orderedPoints);
                var primaryWard = relatedPlaybackItems
                    .Where(item => item.LocationId.HasValue && wardLookup.ContainsKey(item.LocationId.Value))
                    .Select(item => wardLookup[item.LocationId!.Value])
                    .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(item => item.Count())
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.Key)
                    .FirstOrDefault()
                    ?? ResolveNearestWard(orderedPoints.FirstOrDefault(), locations);

                return new StatisticsRouteHistoryDto
                {
                    SessionKey = group.Key,
                    DeviceId = orderedPoints.Select(item => item.DeviceId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                    SessionId = orderedPoints.Select(item => item.SessionId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                    PrimaryWard = primaryWard,
                    StartedAt = orderedPoints.First().CapturedAt,
                    EndedAt = orderedPoints.Last().CapturedAt,
                    TrackingPointCount = orderedPoints.Count,
                    PlaybackCount = relatedPlaybackItems.Count(item => IsPlayCountEvent(item.EventType)),
                    RouteDistanceKm = routeDistanceKm,
                    AverageAccuracyMeters = orderedPoints
                        .Where(item => item.AccuracyMeters.HasValue)
                        .Select(item => item.AccuracyMeters!.Value)
                        .DefaultIfEmpty(0d)
                        .Average(),
                    LocationNames = locationNames,
                    AudioTitles = audioTitles,
                    TourNames = tourNames,
                    Points = orderedPoints
                        .Select(item => new StatisticsRoutePointDto
                        {
                            Latitude = item.Latitude,
                            Longitude = item.Longitude,
                            CapturedAt = item.CapturedAt
                        })
                        .ToList()
                };
            })
            .OrderByDescending(item => item.EndedAt)
            .Take(12)
            .ToList();
    }

    private static List<StatisticsPoiReportRowDto> BuildTopPoiReport(
        IReadOnlyCollection<PlaybackEvent> playCountItems,
        IReadOnlyCollection<PlaybackEvent> playbackItems,
        IReadOnlyDictionary<int, string> wardLookup,
        IReadOnlyDictionary<int, IReadOnlyList<string>> tourNamesByLocation)
    {
        var playbackLookup = playbackItems
            .Where(item => item.LocationId.HasValue)
            .GroupBy(item => item.LocationId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        return playCountItems
            .Where(item => item.LocationId.HasValue)
            .GroupBy(item => item.LocationId!.Value)
            .Select(group =>
            {
                var location = group.First().Location;
                playbackLookup.TryGetValue(group.Key, out var locationPlaybackItems);
                locationPlaybackItems ??= [];

                var listeningSamples = locationPlaybackItems
                    .Where(item => item.ListeningSeconds.HasValue && item.ListeningSeconds.Value > 0)
                    .Select(item => item.ListeningSeconds!.Value)
                    .ToList();

                return new StatisticsPoiReportRowDto
                {
                    LocationId = group.Key,
                    LocationName = location?.Name ?? "Unknown location",
                    OwnerName = location?.Owner?.FullName ?? location?.Owner?.Username,
                    Ward = wardLookup.TryGetValue(group.Key, out var ward) ? ward : "Unassigned",
                    PlayCount = group.Count(),
                    AverageListeningSeconds = listeningSamples.Count == 0
                        ? 0d
                        : Math.Round(listeningSamples.Average(), 1, MidpointRounding.AwayFromZero),
                    ListeningSamples = listeningSamples.Count,
                    TopAudioTitle = group
                        .Select(item => item.Audio?.Title)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .GroupBy(item => item!, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(item => item.Count())
                        .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(item => item.Key)
                        .FirstOrDefault(),
                    UniqueGuests = group
                        .Select(item => GetGuestKey(item.SessionId, item.DeviceId))
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    TourNames = tourNamesByLocation.TryGetValue(group.Key, out var tourNames)
                        ? tourNames
                        : []
                };
            })
            .OrderByDescending(item => item.PlayCount)
            .ThenBy(item => item.LocationName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static List<StatisticsPoiReportRowDto> BuildAverageListeningReport(
        IReadOnlyCollection<PlaybackEvent> playbackItems,
        IReadOnlyDictionary<int, string> wardLookup,
        IReadOnlyDictionary<int, IReadOnlyList<string>> tourNamesByLocation)
    {
        return playbackItems
            .Where(item => item.LocationId.HasValue && item.ListeningSeconds.HasValue && item.ListeningSeconds.Value > 0)
            .GroupBy(item => item.LocationId!.Value)
            .Select(group =>
            {
                var location = group.First().Location;
                var listeningSamples = group
                    .Select(item => item.ListeningSeconds!.Value)
                    .ToList();

                return new StatisticsPoiReportRowDto
                {
                    LocationId = group.Key,
                    LocationName = location?.Name ?? "Unknown location",
                    OwnerName = location?.Owner?.FullName ?? location?.Owner?.Username,
                    Ward = wardLookup.TryGetValue(group.Key, out var ward) ? ward : "Unassigned",
                    PlayCount = group.Count(item => IsPlayCountEvent(item.EventType)),
                    AverageListeningSeconds = Math.Round(listeningSamples.Average(), 1, MidpointRounding.AwayFromZero),
                    ListeningSamples = listeningSamples.Count,
                    TopAudioTitle = group
                        .Select(item => item.Audio?.Title)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .GroupBy(item => item!, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(item => item.Count())
                        .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(item => item.Key)
                        .FirstOrDefault(),
                    UniqueGuests = group
                        .Select(item => GetGuestKey(item.SessionId, item.DeviceId))
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    TourNames = tourNamesByLocation.TryGetValue(group.Key, out var tourNames)
                        ? tourNames
                        : []
                };
            })
            .OrderByDescending(item => item.AverageListeningSeconds)
            .ThenByDescending(item => item.ListeningSamples)
            .ThenBy(item => item.LocationName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static List<PlaybackEvent> SelectPlayCountEvents(IReadOnlyCollection<PlaybackEvent> playbackItems)
    {
        var startedItems = playbackItems
            .Where(item => string.Equals(item.EventType, "Started", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (startedItems.Count > 0)
        {
            return startedItems;
        }

        return playbackItems
            .Where(item => string.Equals(item.EventType, "Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool IsPlayCountEvent(string? eventType) =>
        string.Equals(eventType, "Started", StringComparison.OrdinalIgnoreCase)
        || string.Equals(eventType, "Completed", StringComparison.OrdinalIgnoreCase);

    private static bool HasContextFilters(StatisticsQueryDto query) =>
        query.TourId.GetValueOrDefault() > 0
        || !string.IsNullOrWhiteSpace(query.Ward)
        || !string.IsNullOrWhiteSpace(query.Search);

    private static string GetSessionKey(PlaybackEvent item) =>
        !string.IsNullOrWhiteSpace(item.SessionId)
            ? item.SessionId!
            : !string.IsNullOrWhiteSpace(item.DeviceId)
                ? item.DeviceId!
                : $"playback-{item.PlaybackEventId}";

    private static string GetSessionKey(LocationTrackingEvent item) =>
        !string.IsNullOrWhiteSpace(item.SessionId)
            ? item.SessionId!
            : !string.IsNullOrWhiteSpace(item.DeviceId)
                ? item.DeviceId
                : $"tracking-{item.TrackingEventId}";

    private static string? GetGuestKey(string? sessionId, string? deviceId) =>
        !string.IsNullOrWhiteSpace(sessionId)
            ? sessionId
            : !string.IsNullOrWhiteSpace(deviceId)
                ? deviceId
                : null;

    private static bool Contains(string? value, string search) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static string ExtractWard(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return "Unassigned";
        }

        var parts = address
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var ward = parts.FirstOrDefault(item =>
            item.Contains("ward", StringComparison.OrdinalIgnoreCase)
            || item.Contains("phuong", StringComparison.OrdinalIgnoreCase)
            || item.Contains("phường", StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(ward) ? "Unassigned" : ward;
    }

    private static string ResolveNearestWard(LocationTrackingEvent? trackingPoint, IReadOnlyCollection<Location> locations)
    {
        if (trackingPoint is null || locations.Count == 0)
        {
            return "Unassigned";
        }

        var nearestLocation = locations
            .OrderBy(item => CalculateDistanceKm(
                trackingPoint.Latitude,
                trackingPoint.Longitude,
                item.Latitude,
                item.Longitude))
            .FirstOrDefault();

        return ExtractWard(nearestLocation?.Address);
    }

    private static double CalculateRouteDistanceKm(IReadOnlyList<LocationTrackingEvent> orderedPoints)
    {
        if (orderedPoints.Count < 2)
        {
            return 0d;
        }

        var totalDistance = 0d;
        for (var index = 1; index < orderedPoints.Count; index++)
        {
            totalDistance += CalculateDistanceKm(
                orderedPoints[index - 1].Latitude,
                orderedPoints[index - 1].Longitude,
                orderedPoints[index].Latitude,
                orderedPoints[index].Longitude);
        }

        return Math.Round(totalDistance, 2, MidpointRounding.AwayFromZero);
    }

    private static double CalculateDistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371d;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var startLatitude = DegreesToRadians(latitude1);
        var endLatitude = DegreesToRadians(latitude2);

        var haversine = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(startLatitude) * Math.Cos(endLatitude)
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var arc = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return earthRadiusKm * arc;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180d);

    private static bool IsOwnerScoped(DashboardUser user) =>
        string.Equals(user.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);
}
