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

        var candidateBaseUris = GetCandidateBaseUris();
        Exception? lastException = null;
        var failures = new List<string>();

        foreach (var candidateBaseUri in candidateBaseUris)
        {
            try
            {
                using var response = await httpClient.GetAsync(
                    BuildRouteUri(candidateBaseUri, from, to),
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{candidateBaseUri} returned {(int)response.StatusCode}");
                    continue;
                }

                var route = await ReadBestRouteAsync(response, cancellationToken);
                if (route is null)
                {
                    failures.Add($"{candidateBaseUri} returned no route");
                    continue;
                }

                if (!UriEquals(candidateBaseUri, candidateBaseUris[0]))
                {
                    logger.LogInformation(
                        "Walking route service switched to fallback endpoint {RouteEndpoint} for segment {FromLocationId}->{ToLocationId}.",
                        candidateBaseUri,
                        from.LocationId,
                        to.LocationId);
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
                failures.Add($"{candidateBaseUri} timed out");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                failures.Add($"{candidateBaseUri} request failed");
            }
            catch (NotSupportedException ex)
            {
                lastException = ex;
                failures.Add($"{candidateBaseUri} returned an unsupported payload");
            }
            catch (JsonException ex)
            {
                lastException = ex;
                failures.Add($"{candidateBaseUri} returned invalid JSON");
            }
        }

        if (lastException is not null)
        {
            logger.LogWarning(
                lastException,
                "Road routing request failed for segment {FromLocationId}->{ToLocationId}. Tried endpoints: {Endpoints}. Falling back to straight-line distance.",
                from.LocationId,
                to.LocationId,
                string.Join(", ", candidateBaseUris.Select(item => item.ToString())));
        }
        else
        {
            logger.LogWarning(
                "Road routing request failed for segment {FromLocationId}->{ToLocationId}. Tried endpoints: {Endpoints}. Failures: {Failures}. Falling back to straight-line distance.",
                from.LocationId,
                to.LocationId,
                string.Join(", ", candidateBaseUris.Select(item => item.ToString())),
                failures.Count == 0 ? "Unknown failure" : string.Join(" | ", failures));
        }

        return CreateFallbackResult(from, to);
    }

    private async Task<OsrmRouteDto?> ReadBestRouteAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<OsrmRouteResponse>(cancellationToken);
        return payload?.Routes?
            .OrderBy(item => item.DistanceMeters)
            .FirstOrDefault();
    }

    private Uri BuildRouteUri(Uri baseUri, TourRoutePreviewStopRequest from, TourRoutePreviewStopRequest to) =>
        new(baseUri, BuildRoutePath(from, to));

    private string BuildRoutePath(TourRoutePreviewStopRequest from, TourRoutePreviewStopRequest to)
    {
        var fromLongitude = from.Longitude.ToString(CultureInfo.InvariantCulture);
        var fromLatitude = from.Latitude.ToString(CultureInfo.InvariantCulture);
        var toLongitude = to.Longitude.ToString(CultureInfo.InvariantCulture);
        var toLatitude = to.Latitude.ToString(CultureInfo.InvariantCulture);

        return $"route/v1/{_options.WalkingProfile}/{fromLongitude},{fromLatitude};{toLongitude},{toLatitude}?alternatives=false&overview=full&steps=false&geometries=geojson";
    }

    private IReadOnlyList<Uri> GetCandidateBaseUris()
    {
        var candidates = new List<string>();
        AddCandidate(candidates, _options.BaseUrl);

        foreach (var fallbackBaseUrl in _options.FallbackBaseUrls)
        {
            AddCandidate(candidates, fallbackBaseUrl);
        }

        if (Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var primaryBaseUri)
            && string.Equals(primaryBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate(candidates, new UriBuilder(primaryBaseUri)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = -1
            }.Uri.ToString());
        }

        if (candidates.Count == 0 && httpClient.BaseAddress is not null)
        {
            AddCandidate(candidates, httpClient.BaseAddress.ToString());
        }

        return candidates
            .Select(item => Uri.TryCreate(item, UriKind.Absolute, out var baseUri) ? baseUri : null)
            .Where(item => item is not null)
            .Cast<Uri>()
            .ToList();
    }

    private static void AddCandidate(ICollection<string> candidates, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalizedValue = value.Trim();
        if (!normalizedValue.EndsWith('/'))
        {
            normalizedValue += "/";
        }

        if (!candidates.Contains(normalizedValue, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(normalizedValue);
        }
    }

    private static bool UriEquals(Uri left, Uri right) =>
        string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);

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
