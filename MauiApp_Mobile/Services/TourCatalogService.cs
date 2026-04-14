using System.Net.Http.Json;
using System.Text.Json;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public sealed class TourCatalogService
{
    public static TourCatalogService Instance { get; } = new();
    private static readonly HttpClient HttpClient = MobileApiHttpClientFactory.Create(TimeSpan.FromSeconds(12), 4);
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string TourCacheFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "public-tours-cache.json");
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);

    private TourCatalogService()
    {
    }

    public async Task<IReadOnlyList<MobileTourDescriptor>> GetPublicToursAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (AppDataModeService.Instance.IsApiEnabled)
        {
            try
            {
                var tours = await HttpClient.GetFromJsonAsync<List<TourDto>>(ApiRoutes.PublicTours, cancellationToken) ?? [];
                var mappedTours = tours.Select(MapFromDto).ToList();
                await SaveCachedToursAsync(tours, cancellationToken);
                return mappedTours;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (FriendlyMessageService.IsServerFailure(ex))
                {
                    AppDataModeService.Instance.SwitchToOfflineFallback();
                }
            }
        }

        return (await LoadCachedToursAsync(cancellationToken))
            .Select(MapFromDto)
            .ToList();
    }

    public string? ResolveImageUrl(string? imagePath) => imagePath;

    private static MobileTourDescriptor MapFromDto(TourDto tour)
    {
        var orderedStops = tour.Stops.OrderBy(item => item.SequenceOrder).ToList();
        var preview = tour.RoutePreview ?? CreateFallbackPreview(tour, orderedStops);

        return new MobileTourDescriptor
        {
            Id = tour.Id,
            OwnerId = tour.OwnerId,
            OwnerName = tour.OwnerName,
            Name = tour.Name,
            Description = tour.Description ?? string.Empty,
            StopCount = tour.StopCount,
            TotalDistanceKm = tour.TotalDistanceKm,
            EstimatedDurationMinutes = tour.EstimatedDurationMinutes,
            WalkingSpeedKph = tour.WalkingSpeedKph,
            StartTime = tour.StartTime,
            FinishTime = tour.FinishTime,
            Stops = orderedStops.Select(stop => new MobileTourStopDescriptor
            {
                PlaceId = stop.LocationId.ToString(),
                Name = stop.LocationName,
                Address = stop.Address,
                Category = stop.Category,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                SequenceOrder = stop.SequenceOrder,
                ImageUrl = stop.PreferenceImageUrl ?? string.Empty
            }).ToList(),
            RoutePreview = preview
        };
    }

    private static TourRoutePreviewDto CreateFallbackPreview(TourDto tour, IReadOnlyList<TourStopDto> orderedStops) =>
        new()
        {
            TotalDistanceKm = tour.TotalDistanceKm,
            EstimatedDurationMinutes = tour.EstimatedDurationMinutes,
            WalkingSpeedKph = tour.WalkingSpeedKph,
            StartTime = tour.StartTime,
            FinishTime = tour.FinishTime,
            UsesRoadRouting = false,
            Segments = orderedStops.Select(stop => new TourRouteSegmentDto
            {
                SequenceOrder = stop.SequenceOrder,
                LocationId = stop.LocationId,
                DistanceKm = stop.SegmentDistanceKm
            }).ToList(),
            Path = orderedStops.Select(stop => new TourRoutePointDto
            {
                Latitude = stop.Latitude,
                Longitude = stop.Longitude
            }).ToList()
        };

    private async Task SaveCachedToursAsync(IReadOnlyList<TourDto> tours, CancellationToken cancellationToken)
    {
        await _cacheSemaphore.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TourCacheFilePath)!);
            await using var stream = File.Create(TourCacheFilePath);
            await JsonSerializer.SerializeAsync(stream, tours, CacheJsonOptions, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }

    private async Task<IReadOnlyList<TourDto>> LoadCachedToursAsync(CancellationToken cancellationToken)
    {
        await _cacheSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(TourCacheFilePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(TourCacheFilePath);
            return await JsonSerializer.DeserializeAsync<List<TourDto>>(stream, CacheJsonOptions, cancellationToken) ?? [];
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }
}

public sealed class MobileTourDescriptor
{
    public int Id { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int StopCount { get; init; }
    public double TotalDistanceKm { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public double WalkingSpeedKph { get; init; }
    public string? StartTime { get; init; }
    public string? FinishTime { get; init; }
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
