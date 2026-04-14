using Microsoft.Maui.Devices.Sensors;
using MauiApp_Mobile.Models;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public sealed class TourCatalogService
{
    public static TourCatalogService Instance { get; } = new();

    private TourCatalogService()
    {
    }

    public async Task<IReadOnlyList<MobileTourDescriptor>> GetPublicToursAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var places = await PlaceCatalogService.Instance.GetPlacesAsync(forceRefresh, cancellationToken);
        return BuildTours(places);
    }

    public string? ResolveImageUrl(string? imagePath) => imagePath;

    private static IReadOnlyList<MobileTourDescriptor> BuildTours(IReadOnlyList<PlaceItem> places)
    {
        var activePlaces = places
            .Where(item => item.Latitude != 0d || item.Longitude != 0d)
            .OrderByDescending(item => ParsePriority(item.PriorityText))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<MobileTourDescriptor>();
        if (activePlaces.Count >= 2)
        {
            result.Add(CreateTour(1, "Top POI nổi bật", "Hành trình ghé qua các POI nổi bật nhất trong dữ liệu hiện có.", activePlaces.Take(4).ToList()));
        }

        var groupedByCategory = activePlaces
            .GroupBy(item => item.Category)
            .Where(group => group.Count() >= 2)
            .Take(3)
            .ToList();

        var tourId = 2;
        foreach (var group in groupedByCategory)
        {
            result.Add(CreateTour(
                tourId++,
                $"Tuyến {group.Key}",
                $"Tuyến tham quan theo nhóm {group.Key}.",
                group.Take(4).ToList()));
        }

        return result;
    }

    private static MobileTourDescriptor CreateTour(int id, string name, string description, IReadOnlyList<PlaceItem> places)
    {
        var stops = places.Select((place, index) => new MobileTourStopDescriptor
        {
            PlaceId = place.Id,
            Name = place.Name,
            Address = place.Address,
            Category = place.Category,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            SequenceOrder = index + 1,
            ImageUrl = string.IsNullOrWhiteSpace(place.PreferenceImage) ? place.Image : place.PreferenceImage
        }).ToList();

        var path = stops.Select(item => new TourRoutePointDto
        {
            Latitude = item.Latitude,
            Longitude = item.Longitude
        }).ToList();

        var segments = new List<TourRouteSegmentDto>();
        var totalDistanceKm = 0d;
        for (var index = 1; index < stops.Count; index++)
        {
            var previous = stops[index - 1];
            var current = stops[index];
            var distanceKm = Location.CalculateDistance(
                previous.Latitude,
                previous.Longitude,
                current.Latitude,
                current.Longitude,
                DistanceUnits.Kilometers);
            totalDistanceKm += distanceKm;
            segments.Add(new TourRouteSegmentDto
            {
                SequenceOrder = current.SequenceOrder,
                LocationId = int.TryParse(current.PlaceId, out var locationId) ? locationId : current.SequenceOrder,
                DistanceKm = Math.Round(distanceKm, 2, MidpointRounding.AwayFromZero)
            });
        }

        var preview = new TourRoutePreviewDto
        {
            TotalDistanceKm = Math.Round(totalDistanceKm, 2, MidpointRounding.AwayFromZero),
            EstimatedDurationMinutes = Math.Max(5, (int)Math.Ceiling(totalDistanceKm / 4.5d * 60d)),
            WalkingSpeedKph = 4.5d,
            UsesRoadRouting = false,
            Segments = segments,
            Path = path
        };

        return new MobileTourDescriptor
        {
            Id = id,
            Name = name,
            Description = description,
            StopCount = stops.Count,
            TotalDistanceKm = preview.TotalDistanceKm,
            EstimatedDurationMinutes = preview.EstimatedDurationMinutes,
            Stops = stops,
            RoutePreview = preview
        };
    }

    private static int ParsePriority(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }
}

public sealed class MobileTourDescriptor
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int StopCount { get; init; }
    public double TotalDistanceKm { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public IReadOnlyList<MobileTourStopDescriptor> Stops { get; init; } = Array.Empty<MobileTourStopDescriptor>();
    public TourRoutePreviewDto RoutePreview { get; init; } = new();
}

public sealed class MobileTourStopDescriptor
{
    public string PlaceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string Category { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int SequenceOrder { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
}
