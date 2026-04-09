using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class LocationController(
    DBContext context,
    SharedImageFileStorageService imageStorage,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService) : ControllerBase
{
    [HttpGet("public")]
    public async Task<IActionResult> GetPublicLocations(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public,max-age=60";

        var locations = await context.Locations
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.Images)
            .Include(item => item.AudioContents)
            .Where(item => item.Status == 1)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(locations.Select(item => item.ToDto()).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> GetAllLocations()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var locations = await BuildLocationQuery(access.User!)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Name)
            .ToListAsync();

        return Ok(locations.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLocationById(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var location = await BuildLocationQuery(access.User!)
            .FirstOrDefaultAsync(item => item.LocationId == id);

        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        return Ok(location.ToDto());
    }

    [HttpGet("category/{categoryId:int}")]
    public async Task<IActionResult> GetLocationsByCategory(int categoryId)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var categoryExists = await context.Categories.AnyAsync(item => item.CategoryId == categoryId);
        if (!categoryExists)
        {
            return NotFound(new { message = "Category not found." });
        }

        var locations = await BuildLocationQuery(access.User!)
            .Where(item => item.CategoryId == categoryId)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Name)
            .ToListAsync();

        return Ok(locations.Select(item => item.ToDto()).ToList());
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateLocation(
        [FromForm] LocationUpsertRequest request,
        [FromForm(Name = "PreferenceImageFile")] IFormFile? preferenceImageFile,
        [FromForm(Name = "ImageFiles")] List<IFormFile>? imageFiles,
        CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invalidImageFileName = GetInvalidImageFileName(preferenceImageFile, imageFiles);
        if (!string.IsNullOrWhiteSpace(invalidImageFileName))
        {
            return BadRequest(new { message = $"'{invalidImageFileName}' is not a supported image file." });
        }

        var category = await context.Categories.FirstOrDefaultAsync(item => item.CategoryId == request.CategoryId);
        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        if (category.Status != 1)
        {
            return BadRequest(new { message = "Inactive categories cannot be assigned to locations." });
        }

        var ownerId = await ResolveOwnerIdAsync(access.User!, request.OwnerId);
        if (ownerId is null && request.OwnerId is not null && !IsOwnerScoped(access.User!))
        {
            return NotFound(new { message = "Owner account not found." });
        }

        var preferenceImageUrl = await SavePreferenceImageAsync(preferenceImageFile, request.Name, cancellationToken);
        if (string.IsNullOrWhiteSpace(preferenceImageUrl))
        {
            return BadRequest(new { message = "Upload a preference image before creating a POI." });
        }

        var uploadedGalleryImageUrls = await SaveImagesAsync(imageFiles, request.Name, startSortOrder: 2, cancellationToken);

        var location = new Location
        {
            CategoryId = category.CategoryId,
            OwnerId = ownerId,
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Radius = request.Radius,
            StandbyRadius = request.StandbyRadius,
            Priority = request.Priority,
            DebounceSeconds = request.DebounceSeconds,
            IsGpsTriggerEnabled = request.IsGpsTriggerEnabled,
            Address = Normalize(request.Address),
            WebURL = Normalize(request.WebURL),
            Email = Normalize(request.Email),
            PhoneContact = Normalize(request.Phone),
            EstablishedYear = request.EstablishedYear,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);

        await SyncLocationImagesAsync(
            location,
            preferenceImageUrl,
            BuildDesiredImageUrls(preferenceImageUrl, [], uploadedGalleryImageUrls),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var savedLocation = await BuildLocationQuery(access.User!)
            .FirstAsync(item => item.LocationId == location.LocationId, cancellationToken);
        await activityLogService.LogAsync(
            access.User!,
            "Create",
            "POI",
            location.LocationId,
            location.Name,
            $"Created POI '{location.Name}'.",
            cancellationToken);

        return CreatedAtAction(nameof(GetLocationById), new { id = location.LocationId }, savedLocation.ToDto());
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateLocation(
        int id,
        [FromForm] LocationUpsertRequest request,
        [FromForm(Name = "PreferenceImageFile")] IFormFile? preferenceImageFile,
        [FromForm(Name = "ImageFiles")] List<IFormFile>? imageFiles,
        CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invalidImageFileName = GetInvalidImageFileName(preferenceImageFile, imageFiles);
        if (!string.IsNullOrWhiteSpace(invalidImageFileName))
        {
            return BadRequest(new { message = $"'{invalidImageFileName}' is not a supported image file." });
        }

        var location = await context.Locations
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.LocationId == id, cancellationToken);
        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        if (IsOwnerScoped(access.User!) && location.OwnerId != access.User!.UserId)
        {
            return StatusCode(403, new { message = "You can only update your own locations." });
        }

        var category = await context.Categories.FirstOrDefaultAsync(item => item.CategoryId == request.CategoryId);
        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        if (category.Status != 1)
        {
            return BadRequest(new { message = "Inactive categories cannot be assigned to locations." });
        }

        var ownerId = await ResolveOwnerIdAsync(access.User!, request.OwnerId);
        if (ownerId is null && request.OwnerId is not null && !IsOwnerScoped(access.User!))
        {
            return NotFound(new { message = "Owner account not found." });
        }

        location.CategoryId = category.CategoryId;
        location.OwnerId = ownerId;
        location.Name = request.Name.Trim();
        location.Description = Normalize(request.Description);
        location.Latitude = request.Latitude;
        location.Longitude = request.Longitude;
        location.Radius = request.Radius;
        location.StandbyRadius = request.StandbyRadius;
        location.Priority = request.Priority;
        location.DebounceSeconds = request.DebounceSeconds;
        location.IsGpsTriggerEnabled = request.IsGpsTriggerEnabled;
        location.Address = Normalize(request.Address);
        location.WebURL = Normalize(request.WebURL);
        location.Email = Normalize(request.Email);
        location.PhoneContact = Normalize(request.Phone);
        location.EstablishedYear = request.EstablishedYear;
        location.Status = request.Status;
        location.UpdatedAt = DateTime.UtcNow;

        var currentImageSet = location.Images
            .Select(item => NormalizeImagePath(item.ImageUrl))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentPreferenceImageUrl = ResolvePreferenceImageUrl(location);
        var retainedGalleryImageUrls = request.RetainedImageUrls
            .Select(NormalizeImagePath)
            .Where(item => !string.IsNullOrWhiteSpace(item) && currentImageSet.Contains(item))
            .Cast<string>()
            .ToList();

        var retainedPreferenceImageUrl = NormalizeImagePath(request.RetainedPreferenceImageUrl);
        if (!string.IsNullOrWhiteSpace(currentPreferenceImageUrl)
            && string.IsNullOrWhiteSpace(retainedPreferenceImageUrl)
            && retainedGalleryImageUrls.RemoveAll(item => string.Equals(item, currentPreferenceImageUrl, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            retainedPreferenceImageUrl = currentPreferenceImageUrl;
        }

        if (!string.IsNullOrWhiteSpace(retainedPreferenceImageUrl)
            && !string.Equals(retainedPreferenceImageUrl, currentPreferenceImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            retainedPreferenceImageUrl = null;
        }

        var nextPreferenceImageUrl = await SavePreferenceImageAsync(preferenceImageFile, request.Name, cancellationToken)
            ?? retainedPreferenceImageUrl;

        if (string.IsNullOrWhiteSpace(nextPreferenceImageUrl))
        {
            return BadRequest(new { message = "Upload a preference image before saving this POI." });
        }

        var uploadedGalleryImageUrls = await SaveImagesAsync(
            imageFiles,
            request.Name,
            startSortOrder: retainedGalleryImageUrls.Count + 2,
            cancellationToken);

        var removedImageUrls = await SyncLocationImagesAsync(
            location,
            nextPreferenceImageUrl,
            BuildDesiredImageUrls(nextPreferenceImageUrl, retainedGalleryImageUrls, uploadedGalleryImageUrls),
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        DeleteManagedImages(removedImageUrls);
        await activityLogService.LogAsync(
            access.User!,
            "Edit",
            "POI",
            location.LocationId,
            location.Name,
            $"Updated POI '{location.Name}'.",
            cancellationToken);
        return Ok(new ApiMessageResponse { Message = "Location updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var location = await context.Locations.FirstOrDefaultAsync(item => item.LocationId == id);
        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        if (IsOwnerScoped(access.User!) && location.OwnerId != access.User!.UserId)
        {
            return StatusCode(403, new { message = "You can only archive your own locations." });
        }

        location.Status = 0;
        location.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Delete",
            "POI",
            location.LocationId,
            location.Name,
            $"Archived POI '{location.Name}'.");

        return Ok(new ApiMessageResponse { Message = "Location archived successfully." });
    }

    private IQueryable<Location> BuildLocationQuery(DashboardUser currentUser)
    {
        var query = context.Locations
            .Include(item => item.Category)
            .Include(item => item.Owner)
            .Include(item => item.Images)
            .Include(item => item.AudioContents)
            .AsSplitQuery()
            .AsQueryable();

        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.OwnerId == currentUser.UserId)
            : query;
    }

    private async Task<int?> ResolveOwnerIdAsync(DashboardUser currentUser, int? requestedOwnerId)
    {
        if (IsOwnerScoped(currentUser))
        {
            return currentUser.UserId;
        }

        if (requestedOwnerId is null or <= 0)
        {
            return null;
        }

        var owner = await context.DashboardUsers.FirstOrDefaultAsync(item => item.UserId == requestedOwnerId.Value);
        return owner?.UserId;
    }

    private async Task<string?> SavePreferenceImageAsync(
        IFormFile? preferenceImageFile,
        string locationName,
        CancellationToken cancellationToken)
    {
        if (preferenceImageFile is null || preferenceImageFile.Length <= 0)
        {
            return null;
        }

        var publicPath = await imageStorage.SaveImageAsync(preferenceImageFile, locationName, 1, cancellationToken);
        return NormalizeImagePath(publicPath) ?? publicPath;
    }

    private async Task<List<string>> SaveImagesAsync(
        IEnumerable<IFormFile>? imageFiles,
        string locationName,
        int startSortOrder,
        CancellationToken cancellationToken)
    {
        if (imageFiles is null)
        {
            return [];
        }

        var files = imageFiles
            .Where(item => item is not null && item.Length > 0)
            .ToList();

        if (files.Count == 0)
        {
            return [];
        }

        var savedPaths = new List<string>(files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            var publicPath = await imageStorage.SaveImageAsync(files[index], locationName, startSortOrder + index, cancellationToken);
            savedPaths.Add(NormalizeImagePath(publicPath) ?? publicPath);
        }

        return savedPaths;
    }

    private async Task<List<string>> SyncLocationImagesAsync(
        Location location,
        string preferenceImageUrl,
        IReadOnlyList<string> desiredImageUrls,
        CancellationToken cancellationToken)
    {
        var normalizedPreferenceImageUrl = NormalizeImagePath(preferenceImageUrl)
            ?? throw new InvalidOperationException("A valid preference image is required.");

        var normalizedDesired = desiredImageUrls
            .Select(NormalizeImagePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => !string.Equals(item, normalizedPreferenceImageUrl, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        normalizedDesired.Insert(0, normalizedPreferenceImageUrl);

        await context.Entry(location)
            .Collection(item => item.Images)
            .LoadAsync(cancellationToken);

        var existingImages = location.Images.ToList();
        var existingLookup = existingImages.ToDictionary(
            item => NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl,
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var removedImages = existingImages
            .Where(item => !normalizedDesired.Contains(NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var removedImage in removedImages)
        {
            context.LocationImages.Remove(removedImage);
        }

        location.PreferenceImageUrl = normalizedPreferenceImageUrl;

        for (var index = 0; index < normalizedDesired.Count; index++)
        {
            var imageUrl = normalizedDesired[index];
            var sortOrder = index + 1;
            if (existingLookup.TryGetValue(imageUrl, out var existingImage))
            {
                existingImage.SortOrder = sortOrder;
                continue;
            }

            context.LocationImages.Add(new LocationImage
            {
                LocationId = location.LocationId,
                ImageUrl = imageUrl,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow
            });
        }

        return removedImages
            .Select(item => NormalizeImagePath(item.ImageUrl))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }

    private static IReadOnlyList<string> BuildDesiredImageUrls(
        string preferenceImageUrl,
        IEnumerable<string>? retainedGalleryImageUrls,
        IEnumerable<string>? uploadedGalleryImageUrls)
    {
        var normalizedPreferenceImageUrl = NormalizeImagePath(preferenceImageUrl)
            ?? throw new InvalidOperationException("A valid preference image is required.");

        return new[] { normalizedPreferenceImageUrl }
            .Concat(retainedGalleryImageUrls ?? [])
            .Concat(uploadedGalleryImageUrls ?? [])
            .Select(NormalizeImagePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => !string.Equals(item, normalizedPreferenceImageUrl, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Prepend(normalizedPreferenceImageUrl)
            .ToList();
    }

    private static string? ResolvePreferenceImageUrl(Location location) =>
        NormalizeImagePath(location.PreferenceImageUrl)
        ?? location.Images
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ImageId)
            .Select(item => NormalizeImagePath(item.ImageUrl))
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));

    private void DeleteManagedImages(IEnumerable<string> imageUrls)
    {
        foreach (var imageUrl in imageUrls
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            imageStorage.DeleteIfManaged(imageUrl);
        }
    }

    private static string? GetInvalidImageFileName(IFormFile? preferenceImageFile, IEnumerable<IFormFile>? imageFiles) =>
        new[] { preferenceImageFile }
            .OfType<IFormFile>()
            .Where(item => item.Length > 0)
            .Concat(imageFiles?
                .OfType<IFormFile>()
                .Where(item => item.Length > 0)
                ?? [])
            .FirstOrDefault(item => !IsSupportedImageFile(item))
            ?.FileName;

    private static bool IsOwnerScoped(DashboardUser user) =>
        string.Equals(user.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedImageFile(IFormFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType)
            && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(file.FileName);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeImagePath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : SharedStoragePaths.NormalizePublicImagePath(value);
}
