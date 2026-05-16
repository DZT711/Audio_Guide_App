using Project_SharedClassLibrary.Contracts;

namespace WebApplication_API.Services;

public sealed class TourRoutePlanningService(WalkingRouteService walkingRouteService)
{
    public async Task<TourRoutePreviewDto> CalculatePreviewAsync(
        IEnumerable<TourRoutePreviewStopRequest> orderedStops,
        double walkingSpeedKph,
        string? startTime,
        CancellationToken cancellationToken)
    {
        var stops = orderedStops
            .OrderBy(item => item.SequenceOrder)
            .ThenBy(item => item.LocationId)
            .ToList();

        if (stops.Count == 0)
        {
            var emptyStartTime = TourPlanningService.NormalizeTime(startTime);
            return new TourRoutePreviewDto
            {
                TotalDistanceKm = 0d,
                EstimatedDurationMinutes = 0,
                WalkingSpeedKph = walkingSpeedKph,
                StartTime = emptyStartTime,
                FinishTime = emptyStartTime,
                Segments = [],
                Path = []
            };
        }

        var segments = new List<TourRouteSegmentDto>(stops.Count);
        var path = new List<TourRoutePointDto>();
        var totalDistanceKm = 0d;
        var usesRoadRouting = true;

        for (var index = 0; index < stops.Count; index++)
        {
            var stop = stops[index];
            if (index == 0)
            {
                segments.Add(new TourRouteSegmentDto
                {
                    SequenceOrder = stop.SequenceOrder,
                    LocationId = stop.LocationId,
                    DistanceKm = 0d
                });

                AppendPath(path, [new TourRoutePointDto
                {
                    Latitude = stop.Latitude,
                    Longitude = stop.Longitude
                }]);

                continue;
            }

            var segment = await walkingRouteService.CalculateSegmentAsync(
                stops[index - 1],
                stop,
                cancellationToken);

            totalDistanceKm += segment.DistanceKm;
            usesRoadRouting &= segment.UsesRoadRouting;

            segments.Add(new TourRouteSegmentDto
            {
                SequenceOrder = stop.SequenceOrder,
                LocationId = stop.LocationId,
                DistanceKm = Math.Round(segment.DistanceKm, 2, MidpointRounding.AwayFromZero)
            });

            AppendPath(path, segment.Path);
        }

        var roundedTotalDistanceKm = Math.Round(totalDistanceKm, 2, MidpointRounding.AwayFromZero);
        var estimatedDurationMinutes = TourPlanningService.CalculateDurationMinutes(roundedTotalDistanceKm, walkingSpeedKph);
        var normalizedStartTime = TourPlanningService.NormalizeTime(startTime);

        return new TourRoutePreviewDto
        {
            TotalDistanceKm = roundedTotalDistanceKm,
            EstimatedDurationMinutes = estimatedDurationMinutes,
            WalkingSpeedKph = walkingSpeedKph,
            StartTime = normalizedStartTime,
            FinishTime = TourPlanningService.CalculateFinishTime(normalizedStartTime, estimatedDurationMinutes),
            UsesRoadRouting = usesRoadRouting,
            Segments = segments,
            Path = path
        };
    }

    private static void AppendPath(ICollection<TourRoutePointDto> target, IReadOnlyList<TourRoutePointDto> source)
    {
        if (source.Count == 0)
        {
            return;
        }

        foreach (var point in source)
        {
            if (target.Count > 0
                && target.Last() is { } lastPoint
                && Math.Abs(lastPoint.Latitude - point.Latitude) < 0.000001d
                && Math.Abs(lastPoint.Longitude - point.Longitude) < 0.000001d)
            {
                continue;
            }

            target.Add(point);
        }
    }
}
