using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MauiApp_Mobile.Models;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public sealed class PlaceCatalogService
{
    public static PlaceCatalogService Instance { get; } = new();

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions CacheJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string CacheFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "places-cache.json");
    private static readonly string CategoryCacheFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "place-categories-cache.json");
    private static readonly string ImageCacheDirectoryPath = Path.Combine(FileSystem.Current.AppDataDirectory, "place-images");
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private readonly SemaphoreSlim _categoryLoadSemaphore = new(1, 1);
    private readonly List<PlaceItem> _places = [];
    private readonly List<CategoryDto> _categories = [];

    private PlaceCatalogService()
    {
    }

    public IReadOnlyList<PlaceItem> GetPlaces() => _places.ToList();

    public IReadOnlyList<CategoryDto> GetCategories() => _categories.ToList();

    public async Task<IReadOnlyList<PlaceItem>> GetPlacesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(forceRefresh, cancellationToken);
        return GetPlaces();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await EnsureCategoriesLoadedAsync(forceRefresh, cancellationToken);
        return GetCategories();
    }

    public async Task EnsureLoadedAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _places.Count > 0)
        {
            return;
        }

        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _places.Count > 0)
            {
                return;
            }

            var locations = await LoadLocationsWithFallbackAsync(forceRefresh, cancellationToken);

            if (locations.Count == 0 && _places.Count > 0)
            {
                return;
            }

            _places.Clear();
            _places.AddRange(locations.Select(MapToPlaceItem));
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public async Task EnsureCategoriesLoadedAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _categories.Count > 0)
        {
            return;
        }

        await _categoryLoadSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _categories.Count > 0)
            {
                return;
            }

            var categories = await LoadCategoriesWithFallbackAsync(forceRefresh, cancellationToken);

            if (categories.Count == 0 && _categories.Count > 0)
            {
                return;
            }

            _categories.Clear();
            _categories.AddRange(categories);
        }
        finally
        {
            _categoryLoadSemaphore.Release();
        }
    }

    public PlaceItem? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _places.FirstOrDefault(item =>
            string.Equals(item.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<PlaceItem> SearchByName(string keyword, int maxResults = 6)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<PlaceItem>();
        }

        var trimmedKeyword = keyword.Trim();

        return _places
            .Select(item => new
            {
                Place = item,
                Score = GetSearchScore(item, trimmedKeyword)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Place.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .Select(item => item.Place)
            .ToList();
    }

    public async Task<IReadOnlyList<MapPlacePoint>> GetMapPointsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(forceRefresh, cancellationToken);

        return _places
            .Select(item => new MapPlacePoint
            {
                Id = item.Id,
                Title = item.Name,
                Description = item.Description,
                Address = item.Address,
                Category = item.Category,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Image = item.Image,
                GalleryImages = item.GalleryImages
            })
            .ToList();
    }

    private static PlaceItem MapToPlaceItem(LocationDto location)
    {
        var categoryColors = ResolveCategoryPalette(location.Category);
        var primaryImage = ResolveImageUrl(location.CoverImageUrl);
        var galleryImages = location.ImageUrls
            .Select(ResolveImageUrl)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(primaryImage))
        {
            galleryImages.Insert(0, primaryImage);
            galleryImages = galleryImages
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (galleryImages.Count == 0 && !string.IsNullOrWhiteSpace(primaryImage))
        {
            galleryImages.Add(primaryImage);
        }

        if (galleryImages.Count == 0)
        {
            galleryImages.Add("location.png");
        }

        primaryImage = galleryImages[0];

        return new PlaceItem
        {
            Id = location.Id.ToString(CultureInfo.InvariantCulture),
            Name = location.Name,
            Description = location.Description ?? location.Address ?? "Chưa có mô tả.",
            AudioDescription = location.Description ?? $"Khám phá địa điểm {location.Name}.",
            Category = string.IsNullOrWhiteSpace(location.Category) ? "Khác" : location.Category,
            Rating = location.Priority.ToString(CultureInfo.InvariantCulture),
            Image = primaryImage,
            GalleryImages = galleryImages,
            Address = location.Address ?? "Chưa có địa chỉ",
            Phone = location.Phone ?? "Chưa cập nhật",
            Email = location.Email ?? "Chưa cập nhật",
            Website = location.WebURL ?? "Chưa cập nhật",
            EstablishedYear = location.EstablishedYear.ToString(CultureInfo.InvariantCulture),
            RadiusText = $"{location.Radius:0}m",
            StandbyRadiusText = $"{location.StandbyRadius:0}m",
            GpsText = $"{location.Latitude:F6}, {location.Longitude:F6}",
            PriorityText = location.Priority.ToString(CultureInfo.InvariantCulture),
            DebounceText = $"{location.DebounceSeconds}s",
            OwnerName = string.IsNullOrWhiteSpace(location.OwnerName) ? "Chưa gán người quản lý" : location.OwnerName,
            StatusText = location.Status == 1 ? "Đang hoạt động" : "Ngừng hoạt động",
            GpsTriggerText = location.IsGpsTriggerEnabled ? "Bật GPS trigger" : "Tắt GPS trigger",
            AudioCountText = $"{location.AudioCount} audio",
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            CategoryColor = categoryColors.Background,
            CategoryTextColor = categoryColors.Foreground
        };
    }

    private static string ResolveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
            {
                return File.Exists(absoluteUri.LocalPath)
                    ? absoluteUri.AbsoluteUri
                    : string.Empty;
            }

            return imageUrl;
        }

        if (Path.IsPathRooted(imageUrl) && !imageUrl.StartsWith("/", StringComparison.Ordinal))
        {
            return File.Exists(imageUrl)
                ? new Uri(imageUrl).AbsoluteUri
                : string.Empty;
        }

        return new Uri(new Uri(MobileApiOptions.BaseUrl), imageUrl.TrimStart('/')).ToString();
    }

    private static (Color Background, Color Foreground) ResolveCategoryPalette(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return (Color.FromArgb("#E5E7EB"), Color.FromArgb("#374151"));
        }

        return category.Trim().ToLowerInvariant() switch
        {
            var name when name.Contains("ăn") || name.Contains("food") =>
                (Color.FromArgb("#FFE3E3"), Color.FromArgb("#E53935")),
            var name when name.Contains("quán") || name.Contains("restaurant") =>
                (Color.FromArgb("#FFF7D6"), Color.FromArgb("#CA8A04")),
            var name when name.Contains("uống") || name.Contains("drink") || name.Contains("cafe") =>
                (Color.FromArgb("#E6F4FF"), Color.FromArgb("#2563EB")),
            var name when name.Contains("văn hóa") || name.Contains("culture") =>
                (Color.FromArgb("#F3E8FF"), Color.FromArgb("#7C3AED")),
            var name when name.Contains("tiện ích") || name.Contains("utility") =>
                (Color.FromArgb("#DCFCE7"), Color.FromArgb("#15803D")),
            _ => (Color.FromArgb("#E5F4F1"), Color.FromArgb("#0F766E"))
        };
    }

    private static int GetSearchScore(PlaceItem item, string keyword)
    {
        if (item.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return 300;

        if (item.Name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            return 220;

        if (item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return 180;

        if (item.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return 120;

        if (item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return 90;

        return 0;
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri(MobileApiOptions.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static async Task<IReadOnlyList<LocationDto>> LoadLocationsWithFallbackAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh)
        {
            var cachedLocations = await LoadCacheAsync(cancellationToken);
            if (cachedLocations.Count > 0)
            {
                return cachedLocations;
            }
        }

        try
        {
            var locations = await HttpClient.GetFromJsonAsync<List<LocationDto>>(ApiRoutes.PublicLocations, cancellationToken) ?? [];
            var cachedLocations = await CacheLocationImagesAsync(locations, cancellationToken);
            await SaveCacheAsync(cachedLocations, cancellationToken);
            return cachedLocations;
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            var cachedLocations = await LoadCacheAsync(cancellationToken);
            if (cachedLocations.Count > 0)
            {
                return cachedLocations;
            }

            if (forceRefresh)
            {
                throw;
            }

            return [];
        }
    }

    private static async Task<IReadOnlyList<CategoryDto>> LoadCategoriesWithFallbackAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh)
        {
            var cachedCategories = await LoadCategoryCacheAsync(cancellationToken);
            if (cachedCategories.Count > 0)
            {
                return cachedCategories;
            }
        }

        try
        {
            var categories = await HttpClient.GetFromJsonAsync<List<CategoryDto>>(ApiRoutes.PublicCategories, cancellationToken) ?? [];
            await SaveCategoryCacheAsync(categories, cancellationToken);
            return categories;
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            var cachedCategories = await LoadCategoryCacheAsync(cancellationToken);
            if (cachedCategories.Count > 0)
            {
                return cachedCategories;
            }

            if (forceRefresh)
            {
                throw;
            }

            return [];
        }
    }

    private static async Task<List<LocationDto>> CacheLocationImagesAsync(
        IReadOnlyList<LocationDto> locations,
        CancellationToken cancellationToken)
    {
        var normalizedLocations = new List<LocationDto>(locations.Count);

        foreach (var location in locations)
        {
            var cachedCoverImage = await CacheImageAsync(location.CoverImageUrl, cancellationToken);
            var cachedGalleryImages = new List<string>(location.ImageUrls.Count);

            foreach (var imageUrl in location.ImageUrls)
            {
                var cachedImage = await CacheImageAsync(imageUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cachedImage))
                {
                    cachedGalleryImages.Add(cachedImage);
                }
            }

            if (cachedGalleryImages.Count == 0 && !string.IsNullOrWhiteSpace(cachedCoverImage))
            {
                cachedGalleryImages.Add(cachedCoverImage);
            }

            normalizedLocations.Add(new LocationDto
            {
                Id = location.Id,
                CategoryId = location.CategoryId,
                Category = location.Category,
                OwnerId = location.OwnerId,
                OwnerName = location.OwnerName,
                Name = location.Name,
                Description = location.Description,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Radius = location.Radius,
                StandbyRadius = location.StandbyRadius,
                Priority = location.Priority,
                DebounceSeconds = location.DebounceSeconds,
                IsGpsTriggerEnabled = location.IsGpsTriggerEnabled,
                Address = location.Address,
                CoverImageUrl = string.IsNullOrWhiteSpace(cachedCoverImage) ? location.CoverImageUrl : cachedCoverImage,
                ImageUrls = cachedGalleryImages.Count == 0 ? location.ImageUrls : cachedGalleryImages,
                WebURL = location.WebURL,
                Email = location.Email,
                Phone = location.Phone,
                EstablishedYear = location.EstablishedYear,
                AudioCount = location.AudioCount,
                Status = location.Status,
                CreatedAt = location.CreatedAt,
                UpdatedAt = location.UpdatedAt
            });
        }

        return normalizedLocations;
    }

    private static async Task<string> CacheImageAsync(string? imageUrl, CancellationToken cancellationToken)
    {
        var resolvedImageUrl = ResolveImageUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(resolvedImageUrl))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(resolvedImageUrl, UriKind.Absolute, out var imageUri) || imageUri.IsFile)
        {
            return resolvedImageUrl;
        }

        try
        {
            Directory.CreateDirectory(ImageCacheDirectoryPath);

            var fileExtension = GetImageFileExtension(imageUri);
            var targetFilePath = Path.Combine(ImageCacheDirectoryPath, $"{ComputeHash(resolvedImageUrl)}{fileExtension}");
            if (File.Exists(targetFilePath))
            {
                return targetFilePath;
            }

            await using var remoteStream = await HttpClient.GetStreamAsync(imageUri, cancellationToken);
            await using var localStream = File.Create(targetFilePath);
            await remoteStream.CopyToAsync(localStream, cancellationToken);

            return targetFilePath;
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return resolvedImageUrl;
        }
    }

    private static string GetImageFileExtension(Uri imageUri)
    {
        var extension = Path.GetExtension(imageUri.AbsolutePath);
        return string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task SaveCacheAsync(IReadOnlyList<LocationDto> locations, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
            await using var stream = File.Create(CacheFilePath);
            await JsonSerializer.SerializeAsync(stream, locations, CacheJsonOptions, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task SaveCategoryCacheAsync(IReadOnlyList<CategoryDto> categories, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CategoryCacheFilePath)!);
            await using var stream = File.Create(CategoryCacheFilePath);
            await JsonSerializer.SerializeAsync(stream, categories, CacheJsonOptions, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<List<LocationDto>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(CacheFilePath);
            return await JsonSerializer.DeserializeAsync<List<LocationDto>>(stream, CacheJsonOptions, cancellationToken) ?? [];
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private static async Task<List<CategoryDto>> LoadCategoryCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CategoryCacheFilePath))
            {
                return [];
            }

            await using var stream = File.OpenRead(CategoryCacheFilePath);
            return await JsonSerializer.DeserializeAsync<List<CategoryDto>>(stream, CacheJsonOptions, cancellationToken) ?? [];
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    public sealed class MapPlacePoint
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string Image { get; init; } = string.Empty;
        public IReadOnlyList<string> GalleryImages { get; init; } = Array.Empty<string>();
    }
}
