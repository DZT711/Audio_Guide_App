using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Project_SharedClassLibrary.Contracts;

namespace WebApplication_API.Services;

public sealed class WalkingRouteService(
    HttpClient httpClient,
    Microsoft.Extensions.Options.IOptions<RoutePlanningOptions> options,
    ILogger<WalkingRouteService> logger)
{
    private readonly RoutePlanningOptions _options = options.Value;

    public async Task<WalkingRouteSegmentResult> CalculateSegmentAsync(
        TourRoutePreviewStopRequest from,
        TourRoutePreviewStopRequest to,
        CancellationToken cancellationToken)
    {
        if (Math.Abs(from.Latitude - to.Latitude) < 0.000001d
            && Math.Abs(from.Longitude - to.Longitude) < 0.000001d)
        {
            return CreateStationaryResult(to);
        }

        try
        {
            using var response = await httpClient.GetAsync(
                BuildRoutePath(from, to),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Road routing request failed with status {StatusCode} for segment {FromLocationId}->{ToLocationId}. Falling back to straight-line distance.",
                    response.StatusCode,
                    from.LocationId,
                    to.LocationId);

                return CreateFallbackResult(from, to);
            }

            var payload = await response.Content.ReadFromJsonAsync<OsrmRouteResponse>(cancellationToken);
            var route = payload?.Routes
                ?.OrderBy(item => item.DistanceMeters)
                .FirstOrDefault();

            if (route is null)
            {
                logger.LogWarning(
                    "Road routing response did not include a route for segment {FromLocationId}->{ToLocationId}. Falling back to straight-line distance.",
                    from.LocationId,
                    to.LocationId);

                return CreateFallbackResult(from, to);
            }

            var geometry = route.Geometry?.Coordinates?
                .Where(item => item.Count >= 2)
                .Select(item => new TourRoutePointDto
                {
                    Latitude = item[1],
                    Longitude = item[0]
                })
                .ToList() ?? [];

            if (geometry.Count == 0)
            {
                geometry =
                [
                    new TourRoutePointDto { Latitude = from.Latitude, Longitude = from.Longitude },
                    new TourRoutePointDto { Latitude = to.Latitude, Longitude = to.Longitude }
                ];
            }

            return new WalkingRouteSegmentResult(
                route.DistanceMeters / 1000d,
                geometry,
                UsesRoadRouting: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Road routing request timed out for segment {FromLocationId}->{ToLocationId}. Falling back to straight-line distance.",
                from.LocationId,
                to.LocationId);

            return CreateFallbackResult(from, to);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Road routing request failed for segment {FromLocationId}->{ToLocationId}. Falling back to straight-line distance.",
                from.LocationId,
                to.LocationId);

            return CreateFallbackResult(from, to);
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(
                ex,
                "Road routing response format was unsupported for segment {FromLocationId}->{ToLocationId}. Falling back to straight-line distance.",
                from.LocationId,
                to.LocationId);

            return CreateFallbackResult(from, to);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Road routing response could not be parsed for segment {FromLocationId}->{ToLocationId}. Falling back to straight-line distance.",
                from.LocationId,
                to.LocationId);

            return CreateFallbackResult(from, to);
        }
    }

    private string BuildRoutePath(TourRoutePreviewStopRequest from, TourRoutePreviewStopRequest to)
    {
        var fromLongitude = from.Longitude.ToString(CultureInfo.InvariantCulture);
        var fromLatitude = from.Latitude.ToString(CultureInfo.InvariantCulture);
        var toLongitude = to.Longitude.ToString(CultureInfo.InvariantCulture);
        var toLatitude = to.Latitude.ToString(CultureInfo.InvariantCulture);

        return $"route/v1/{_options.WalkingProfile}/{fromLongitude},{fromLatitude};{toLongitude},{toLatitude}?alternatives=false&overview=full&steps=false&geometries=geojson";
    }

    private static WalkingRouteSegmentResult CreateStationaryResult(TourRoutePreviewStopRequest stop) =>
        new(
            0d,
            [new TourRoutePointDto { Latitude = stop.Latitude, Longitude = stop.Longitude }],
            UsesRoadRouting: true);

    private static WalkingRouteSegmentResult CreateFallbackResult(
        TourRoutePreviewStopRequest from,
        TourRoutePreviewStopRequest to) =>
        new(
            TourPlanningService.CalculateDistanceKm(from.Latitude, from.Longitude, to.Latitude, to.Longitude),
            [
                new TourRoutePointDto { Latitude = from.Latitude, Longitude = from.Longitude },
                new TourRoutePointDto { Latitude = to.Latitude, Longitude = to.Longitude }
            ],
            UsesRoadRouting: false);

    private sealed class OsrmRouteResponse
    {
        [JsonPropertyName("routes")]
        public List<OsrmRouteDto>? Routes { get; init; }
    }

    private sealed class OsrmRouteDto
    {
        [JsonPropertyName("distance")]
        public double DistanceMeters { get; init; }

        [JsonPropertyName("geometry")]
        public OsrmGeometryDto? Geometry { get; init; }
    }

    private sealed class OsrmGeometryDto
    {
        [JsonPropertyName("coordinates")]
        public List<List<double>>? Coordinates { get; init; }
    }
}

public sealed record WalkingRouteSegmentResult(
    double DistanceKm,
    IReadOnlyList<TourRoutePointDto> Path,
    bool UsesRoadRouting);
