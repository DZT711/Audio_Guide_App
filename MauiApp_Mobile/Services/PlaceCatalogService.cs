using System.Globalization;
using System.Diagnostics;
using System.Net;
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
    private static readonly string AudioTrackCacheFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "place-audio-cache.json");
    private static readonly string ImageCacheDirectoryPath = Path.Combine(FileSystem.Current.CacheDirectory, "place-images");
    private static readonly SemaphoreSlim ImageWarmupSemaphore = new(1, 1);
    private readonly SemaphoreSlim _catalogLoadSemaphore = new(1, 1);
    private readonly SemaphoreSlim _audioCacheSemaphore = new(1, 1);
    private readonly List<PlaceItem> _places = [];
    private readonly List<CategoryDto> _categories = [];
    private readonly Dictionary<string, List<PublicAudioTrackDto>> _audioTracksByPlaceId = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasLoadedAudioTrackCache;

    private PlaceCatalogService()
    {
    }

    public IReadOnlyList<PlaceItem> GetPlaces() => _places.ToList();

    public IReadOnlyList<CategoryDto> GetCategories() => _categories.ToList();

    public async Task<CatalogSnapshot> GetCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await EnsureCatalogLoadedAsync(forceRefresh, cancellationToken);
        return new CatalogSnapshot(GetPlaces(), GetCategories());
    }

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
        await EnsureCatalogLoadedAsync(forceRefresh, cancellationToken);
    }

    public async Task EnsureCategoriesLoadedAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await EnsureCatalogLoadedAsync(forceRefresh, cancellationToken);
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

    public async Task<IReadOnlyList<PublicAudioTrackDto>> GetAudioTracksAsync(
        string? placeId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return [];
        }

        await EnsureLoadedAsync(false, cancellationToken);

        var normalizedPlaceId = placeId.Trim();
        if (!forceRefresh && _audioTracksByPlaceId.TryGetValue(normalizedPlaceId, out var cachedTracks))
        {
            return cachedTracks.ToList();
        }

        if (!int.TryParse(normalizedPlaceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId))
        {
            return [];
        }

        await EnsureAudioTrackCacheLoadedAsync(cancellationToken);

        if (!AppDataModeService.Instance.IsApiEnabled)
        {
            return _audioTracksByPlaceId.TryGetValue(normalizedPlaceId, out var offlineTracks)
                ? offlineTracks.ToList()
                : [];
        }

        try
        {
            var route = ApiRoutes.GetPublicLocationAudio(locationId);
            var audioTracks = await HttpClient.GetFromJsonAsync<List<PublicAudioTrackDto>>(route, cancellationToken) ?? [];
            _audioTracksByPlaceId[normalizedPlaceId] = audioTracks;
            await SaveAudioTrackCacheAsync(cancellationToken);

            var place = _places.FirstOrDefault(item => string.Equals(item.Id, normalizedPlaceId, StringComparison.OrdinalIgnoreCase));
                if (place is not null)
                {
                    place.AudioTracks = audioTracks;
                    place.AudioCountText = $"{audioTracks.Count} audio";
                    place.AvailableVoiceGenders = audioTracks
                    .Select(item => NormalizeVoiceGender(item.VoiceGender))
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Cast<string>()
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    place.LanguageBadgeSummaryText = LanguageBadgeService.BuildSummary(audioTracks);
                }

            return audioTracks.ToList();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (FriendlyMessageService.IsServerFailure(ex))
            {
                AppDataModeService.Instance.SwitchToOfflineFallback();
            }

            await EnsureAudioTrackCacheLoadedAsync(cancellationToken);
            return _audioTracksByPlaceId.TryGetValue(normalizedPlaceId, out var fallbackTracks)
                ? fallbackTracks.ToList()
                : [];
        }
    }

    public async Task<PublicAudioTrackDto?> GetDefaultAudioTrackAsync(
        string? placeId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var tracks = await GetAudioTracksAsync(placeId, forceRefresh, cancellationToken);
        return tracks.FirstOrDefault(item => item.IsDefault)
            ?? tracks
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
                .ThenBy(item => item.Id)
                .FirstOrDefault();
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
                Image = string.IsNullOrWhiteSpace(item.PreferenceImage) ? item.Image : item.PreferenceImage,
                GalleryImages = SelectMapGalleryImages(item)
            })
            .ToList();
    }

    public async Task TrySyncCatalogInBackgroundAsync(CancellationToken cancellationToken = default)
    {
        if (!AppDataModeService.Instance.IsApiEnabled)
        {
            return;
        }

        try
        {
            await EnsureCatalogLoadedAsync(forceRefresh: true, cancellationToken);
        }
        catch
        {
        }
    }

    private static PlaceItem MapToPlaceItem(LocationDto location, IReadOnlyList<PublicAudioTrackDto>? audioTracks)
    {
        audioTracks ??= Array.Empty<PublicAudioTrackDto>();
        var categoryColors = ResolveCategoryPalette(location.Category);
        var primaryImage = ResolveImageUrl(location.CoverImageUrl);
        var preferenceImage = ResolveImageUrl(location.PreferenceImageUrl);
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
            PreferenceImage = string.IsNullOrWhiteSpace(preferenceImage) ? primaryImage : preferenceImage,
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
            AudioCountText = $"{Math.Max(location.AudioCount, audioTracks.Count)} audio",
            AudioTracks = audioTracks,
            AvailableVoiceGenders = (audioTracks.Count > 0
                    ? audioTracks.Select(item => item.VoiceGender)
                    : location.AvailableVoiceGenders)
                .Select(NormalizeVoiceGender)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LanguageBadgeSummaryText = LanguageBadgeService.BuildSummary(audioTracks),
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            CategoryColor = categoryColors.Background,
            CategoryTextColor = categoryColors.Foreground
        };
    }

    private static IReadOnlyList<string> SelectMapGalleryImages(PlaceItem item)
    {
        var markerImage = string.IsNullOrWhiteSpace(item.PreferenceImage) ? item.Image : item.PreferenceImage;
        var galleryImages = item.GalleryImages
            .Where(image => !string.IsNullOrWhiteSpace(image))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(image => string.Equals(image, markerImage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(image => ComputeStableHash($"{item.Id}|{image}"))
            .Take(5)
            .ToList();

        if (galleryImages.Count == 0 && !string.IsNullOrWhiteSpace(markerImage))
        {
            galleryImages.Add(markerImage);
        }

        if (galleryImages.Count < 5 &&
            !string.IsNullOrWhiteSpace(item.Image) &&
            !galleryImages.Contains(item.Image, StringComparer.OrdinalIgnoreCase))
        {
            galleryImages.Add(item.Image);
        }

        return galleryImages;
    }

    private static string ResolveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        var normalizedImageUrl = MobileApiOptions.ResolveImageUrl(imageUrl);

        if (Uri.TryCreate(normalizedImageUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
            {
                if (HasUnsupportedVectorExtension(absoluteUri.AbsolutePath))
                {
                    return "location.png";
                }

                return File.Exists(absoluteUri.LocalPath)
                    ? absoluteUri.AbsoluteUri
                    : string.Empty;
            }

            if (HasUnsupportedVectorExtension(absoluteUri.AbsolutePath))
            {
                return "location.png";
            }

            return TryResolveCachedRemoteImageUri(absoluteUri, out var cachedRemoteImageUri)
                ? cachedRemoteImageUri
                : absoluteUri.AbsoluteUri;
        }

        if (Path.IsPathRooted(normalizedImageUrl) && !normalizedImageUrl.StartsWith("/", StringComparison.Ordinal))
        {
            if (HasUnsupportedVectorExtension(normalizedImageUrl))
            {
                return "location.png";
            }

            return File.Exists(normalizedImageUrl)
                ? new Uri(normalizedImageUrl).AbsoluteUri
                : string.Empty;
        }

        if (IsBundledImageAsset(normalizedImageUrl))
        {
            return normalizedImageUrl;
        }

        var remoteUri = new Uri(normalizedImageUrl, UriKind.Absolute);
        if (HasUnsupportedVectorExtension(remoteUri.AbsolutePath))
        {
            return "location.png";
        }

        return TryResolveCachedRemoteImageUri(remoteUri, out var cachedImageUri)
            ? cachedImageUri
            : remoteUri.AbsoluteUri;
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
        return MobileApiHttpClientFactory.Create(TimeSpan.FromSeconds(10), 8);
    }

    private async Task EnsureCatalogLoadedAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _places.Count > 0 && _categories.Count > 0)
        {
            return;
        }

        await _catalogLoadSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _places.Count > 0 && _categories.Count > 0)
            {
                return;
            }

            var catalogData = await LoadCatalogWithFallbackAsync(forceRefresh, cancellationToken);
            if (!catalogData.HasContent && (_places.Count > 0 || _categories.Count > 0))
            {
                return;
            }

            ApplyCatalogData(catalogData);
        }
        finally
        {
            _catalogLoadSemaphore.Release();
        }
    }

    private void ApplyCatalogData(CachedCatalogData catalogData)
    {
        _categories.Clear();
        _categories.AddRange(catalogData.Categories);

        _audioTracksByPlaceId.Clear();
        foreach (var pair in catalogData.AudioTracksByPlaceId)
        {
            _audioTracksByPlaceId[pair.Key] = pair.Value;
        }

        _hasLoadedAudioTrackCache = true;

        _places.Clear();
        _places.AddRange(catalogData.Locations.Select(location =>
        {
            var locationKey = location.Id.ToString(CultureInfo.InvariantCulture);
            IReadOnlyList<PublicAudioTrackDto> audioTracks = catalogData.AudioTracksByPlaceId.TryGetValue(locationKey, out var tracks)
                ? tracks
                : Array.Empty<PublicAudioTrackDto>();

            return MapToPlaceItem(location, audioTracks);
        }));
    }

    private async Task<CachedCatalogData> LoadCatalogWithFallbackAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!AppDataModeService.Instance.IsApiEnabled)
        {
            return await LoadCatalogFromCacheAsync(cancellationToken);
        }

        if (!forceRefresh)
        {
            var cachedCatalog = await LoadCatalogFromCacheAsync(cancellationToken);
            if (cachedCatalog.HasContent)
            {
                _ = WarmLocationImagesInBackgroundAsync(cachedCatalog.Locations);
                return cachedCatalog;
            }
        }

        try
        {
            var snapshot = await HttpClient.GetFromJsonAsync<PublicCatalogSnapshotDto>(ApiRoutes.PublicCatalog, cancellationToken)
                ?? new PublicCatalogSnapshotDto();
            var audioTracksByPlaceId = GroupAudioTracksByPlaceId(snapshot.AudioTracks);

            await MobileDatabaseService.Instance.SaveCatalogSnapshotAsync(snapshot, cancellationToken);
            await SaveCacheAsync(snapshot.Locations, cancellationToken);
            await SaveCategoryCacheAsync(snapshot.Categories, cancellationToken);
            await SaveAudioTrackCacheAsync(audioTracksByPlaceId, cancellationToken);

            _ = WarmLocationImagesInBackgroundAsync(snapshot.Locations);
            Debug.WriteLine(
                $"[MobileApi] Catalog refresh synchronized {snapshot.Locations.Count} POIs, {snapshot.Categories.Count} categories, {snapshot.AudioTracks.Count} audio tracks.");

            return new CachedCatalogData(snapshot.Locations, snapshot.Categories, audioTracksByPlaceId);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (FriendlyMessageService.IsServerFailure(ex))
            {
                AppDataModeService.Instance.SwitchToOfflineFallback();
            }

            var cachedCatalog = await LoadCatalogFromCacheAsync(cancellationToken);
            if (cachedCatalog.HasContent)
            {
                _ = WarmLocationImagesInBackgroundAsync(cachedCatalog.Locations);
                return cachedCatalog;
            }

            if (forceRefresh)
            {
                throw;
            }

            return CachedCatalogData.Empty;
        }
    }

    private static async Task<CachedCatalogData> LoadCatalogFromCacheAsync(CancellationToken cancellationToken)
    {
        var locationsTask = LoadCacheAsync(cancellationToken);
        var categoriesTask = LoadCategoryCacheAsync(cancellationToken);
        var audioTracksTask = LoadAudioTrackCacheAsync(cancellationToken);
        await Task.WhenAll(locationsTask, categoriesTask, audioTracksTask);

        return new CachedCatalogData(locationsTask.Result, categoriesTask.Result, audioTracksTask.Result);
    }

    private static Task WarmLocationImagesInBackgroundAsync(IReadOnlyList<LocationDto> locations)
    {
        if (locations.Count == 0 || !AppDataModeService.Instance.IsApiEnabled)
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await ImageWarmupSemaphore.WaitAsync();
            try
            {
                using var parallelism = new SemaphoreSlim(4, 4);
                var tasks = locations
                    .SelectMany(GetImageCandidates)
                    .Where(image => !string.IsNullOrWhiteSpace(image))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(async imageUrl =>
                    {
                        await parallelism.WaitAsync();
                        try
                        {
                            await CacheImageAsync(imageUrl, CancellationToken.None);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            parallelism.Release();
                        }
                    });

                await Task.WhenAll(tasks);
            }
            finally
            {
                ImageWarmupSemaphore.Release();
            }
        });
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

            var targetFilePath = ResolveCachedImageFilePath(imageUri);
            if (File.Exists(targetFilePath) && new FileInfo(targetFilePath).Length > 0)
            {
                return new Uri(targetFilePath).AbsoluteUri;
            }

            var partialFilePath = $"{targetFilePath}.download";
            using var response = await HttpClient.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var localStream = File.Create(partialFilePath);
            await remoteStream.CopyToAsync(localStream, cancellationToken);
            await localStream.FlushAsync(cancellationToken);

            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }

            File.Move(partialFilePath, targetFilePath);

            return new Uri(targetFilePath).AbsoluteUri;
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            var partialFilePath = $"{ResolveCachedImageFilePath(imageUri)}.download";
            try
            {
                if (File.Exists(partialFilePath))
                {
                    File.Delete(partialFilePath);
                }
            }
            catch
            {
            }

            return resolvedImageUrl;
        }
    }

    private static string GetImageFileExtension(Uri imageUri)
    {
        var extension = Path.GetExtension(imageUri.AbsolutePath);
        return string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
    }

    private static string ResolveCachedImageFilePath(Uri imageUri)
    {
        var fileExtension = GetImageFileExtension(imageUri);
        return Path.Combine(ImageCacheDirectoryPath, $"{ComputeHash(imageUri.AbsoluteUri)}{fileExtension}");
    }

    private static bool TryResolveCachedRemoteImageUri(Uri imageUri, out string cachedImageUri)
    {
        cachedImageUri = string.Empty;
        if (imageUri.IsFile)
        {
            return false;
        }

        var cachedFilePath = ResolveCachedImageFilePath(imageUri);
        if (!File.Exists(cachedFilePath))
        {
            return false;
        }

        cachedImageUri = new Uri(cachedFilePath).AbsoluteUri;
        return true;
    }

    private static bool HasUnsupportedVectorExtension(string imagePath) =>
        string.Equals(Path.GetExtension(imagePath), ".svg", StringComparison.OrdinalIgnoreCase);

    private static bool IsBundledImageAsset(string imagePath) =>
        !string.IsNullOrWhiteSpace(imagePath) &&
        !imagePath.Contains('/') &&
        !imagePath.Contains('\\');

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int ComputeStableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }

    private static async Task SaveCacheAsync(IReadOnlyList<LocationDto> locations, CancellationToken cancellationToken)
    {
        try
        {
            var existingSnapshot = await MobileDatabaseService.Instance.LoadCatalogSnapshotAsync(cancellationToken);
            await MobileDatabaseService.Instance.SaveCatalogSnapshotAsync(new PublicCatalogSnapshotDto
            {
                RefreshedAtUtc = existingSnapshot.RefreshedAtUtc,
                Categories = existingSnapshot.Categories,
                Locations = locations,
                AudioTracks = existingSnapshot.AudioTracks
            }, cancellationToken);

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
            var existingSnapshot = await MobileDatabaseService.Instance.LoadCatalogSnapshotAsync(cancellationToken);
            await MobileDatabaseService.Instance.SaveCatalogSnapshotAsync(new PublicCatalogSnapshotDto
            {
                RefreshedAtUtc = existingSnapshot.RefreshedAtUtc,
                Categories = categories,
                Locations = existingSnapshot.Locations,
                AudioTracks = existingSnapshot.AudioTracks
            }, cancellationToken);

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
            var snapshot = await MobileDatabaseService.Instance.LoadCatalogSnapshotAsync(cancellationToken);
            if (snapshot.Locations.Count > 0)
            {
                return snapshot.Locations.ToList();
            }

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
            var snapshot = await MobileDatabaseService.Instance.LoadCatalogSnapshotAsync(cancellationToken);
            if (snapshot.Categories.Count > 0)
            {
                return snapshot.Categories.ToList();
            }

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

    private async Task EnsureAudioTrackCacheLoadedAsync(CancellationToken cancellationToken)
    {
        if (_hasLoadedAudioTrackCache)
        {
            return;
        }

        await _audioCacheSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_hasLoadedAudioTrackCache)
            {
                return;
            }

            var cachedAudioTracks = await LoadAudioTrackCacheAsync(cancellationToken);
            _audioTracksByPlaceId.Clear();

            foreach (var pair in cachedAudioTracks)
            {
                _audioTracksByPlaceId[pair.Key] = pair.Value;
            }

            _hasLoadedAudioTrackCache = true;
        }
        finally
        {
            _audioCacheSemaphore.Release();
        }
    }

    private async Task SaveAudioTrackCacheAsync(CancellationToken cancellationToken)
    {
        await SaveAudioTrackCacheAsync(_audioTracksByPlaceId, cancellationToken);
    }

    private static async Task SaveAudioTrackCacheAsync(
        IReadOnlyDictionary<string, List<PublicAudioTrackDto>> audioTracksByPlaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var existingSnapshot = await MobileDatabaseService.Instance.LoadCatalogSnapshotAsync(cancellationToken);
            await MobileDatabaseService.Instance.SaveCatalogSnapshotAsync(new PublicCatalogSnapshotDto
            {
                RefreshedAtUtc = existingSnapshot.RefreshedAtUtc,
                Categories = existingSnapshot.Categories,
                Locations = existingSnapshot.Locations,
                AudioTracks = audioTracksByPlaceId.Values.SelectMany(item => item).ToList()
            }, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(AudioTrackCacheFilePath)!);
            await using var stream = File.Create(AudioTrackCacheFilePath);
            await JsonSerializer.SerializeAsync(stream, audioTracksByPlaceId, CacheJsonOptions, cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task<Dictionary<string, List<PublicAudioTrackDto>>> LoadAudioTrackCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await MobileDatabaseService.Instance.LoadCatalogSnapshotAsync(cancellationToken);
            if (snapshot.AudioTracks.Count > 0)
            {
                return GroupAudioTracksByPlaceId(snapshot.AudioTracks);
            }

            if (!File.Exists(AudioTrackCacheFilePath))
            {
                return new Dictionary<string, List<PublicAudioTrackDto>>(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = File.OpenRead(AudioTrackCacheFilePath);
            var cache = await JsonSerializer.DeserializeAsync<Dictionary<string, List<PublicAudioTrackDto>>>(
                stream,
                CacheJsonOptions,
                cancellationToken);

            return cache is null
                ? new Dictionary<string, List<PublicAudioTrackDto>>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, List<PublicAudioTrackDto>>(cache, StringComparer.OrdinalIgnoreCase);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return new Dictionary<string, List<PublicAudioTrackDto>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, List<PublicAudioTrackDto>> GroupAudioTracksByPlaceId(
        IEnumerable<PublicAudioTrackDto> audioTracks)
    {
        return audioTracks
            .Where(item => item.LocationId > 0)
            .GroupBy(item => item.LocationId.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Priority)
                    .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
                    .ThenBy(item => item.Id)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeVoiceGender(string? voiceGender)
    {
        if (string.IsNullOrWhiteSpace(voiceGender))
        {
            return null;
        }

        return voiceGender.Trim().ToUpperInvariant() switch
        {
            "MALE" => "Male",
            "NAM" => "Male",
            "FEMALE" => "Female",
            "NU" => "Female",
            "NỮ" => "Female",
            _ => voiceGender.Trim()
        };
    }

    private static int ResolveSourceTypeOrder(string? sourceType) =>
        sourceType?.Trim().ToUpperInvariant() switch
        {
            "RECORDED" => 0,
            "HYBRID" => 1,
            _ => 2
        };

    private static IEnumerable<string?> GetImageCandidates(LocationDto location)
    {
        yield return location.PreferenceImageUrl;
        yield return location.CoverImageUrl;

        foreach (var image in location.ImageUrls)
        {
            yield return image;
        }
    }

    public sealed record CatalogSnapshot(
        IReadOnlyList<PlaceItem> Places,
        IReadOnlyList<CategoryDto> Categories);

    private sealed record CachedCatalogData(
        IReadOnlyList<LocationDto> Locations,
        IReadOnlyList<CategoryDto> Categories,
        IReadOnlyDictionary<string, List<PublicAudioTrackDto>> AudioTracksByPlaceId)
    {
        public static CachedCatalogData Empty { get; } = new(
            Array.Empty<LocationDto>(),
            Array.Empty<CategoryDto>(),
            new Dictionary<string, List<PublicAudioTrackDto>>(StringComparer.OrdinalIgnoreCase));

        public bool HasContent => Locations.Count > 0 || Categories.Count > 0 || AudioTracksByPlaceId.Count > 0;
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
