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
    AdminRequestAuthorizationService authService) : ControllerBase
{
    [HttpGet("public")]
    public async Task<IActionResult> GetPublicLocations(CancellationToken cancellationToken)
    {
        var locations = await context.Locations
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

        var invalidImageFileName = GetInvalidImageFileName(imageFiles);
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

        await AddImagesAsync(location, imageFiles, cancellationToken);

        var savedLocation = await BuildLocationQuery(access.User!)
            .FirstAsync(item => item.LocationId == location.LocationId, cancellationToken);

        return CreatedAtAction(nameof(GetLocationById), new { id = location.LocationId }, savedLocation.ToDto());
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateLocation(
        int id,
        [FromForm] LocationUpsertRequest request,
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

        var invalidImageFileName = GetInvalidImageFileName(imageFiles);
        if (!string.IsNullOrWhiteSpace(invalidImageFileName))
        {
            return BadRequest(new { message = $"'{invalidImageFileName}' is not a supported image file." });
        }

        var location = await context.Locations.FirstOrDefaultAsync(item => item.LocationId == id, cancellationToken);
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

        await context.SaveChangesAsync(cancellationToken);
        await SyncExistingImagesAsync(location, request.RetainedImageUrls, cancellationToken);
        await AddImagesAsync(location, imageFiles, cancellationToken);
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

        return Ok(new ApiMessageResponse { Message = "Location archived successfully." });
    }

    private IQueryable<Location> BuildLocationQuery(DashboardUser currentUser)
    {
        var query = context.Locations
            .Include(item => item.Category)
            .Include(item => item.Owner)
            .Include(item => item.Images)
            .Include(item => item.AudioContents)
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

    private async Task AddImagesAsync(Location location, IEnumerable<IFormFile>? imageFiles, CancellationToken cancellationToken)
    {
        if (imageFiles is null)
        {
            return;
        }

        var files = imageFiles
            .Where(item => item is not null && item.Length > 0)
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        var currentSortOrder = await context.LocationImages
            .Where(item => item.LocationId == location.LocationId)
            .Select(item => (int?)item.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        foreach (var file in files)
        {
            currentSortOrder++;
            var publicPath = await imageStorage.SaveImageAsync(file, location.Name, currentSortOrder, cancellationToken);
            context.LocationImages.Add(new LocationImage
            {
                LocationId = location.LocationId,
                ImageUrl = SharedStoragePaths.NormalizePublicImagePath(publicPath) ?? publicPath,
                SortOrder = currentSortOrder,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncExistingImagesAsync(
        Location location,
        IEnumerable<string>? retainedImageUrls,
        CancellationToken cancellationToken)
    {
        var retainedSet = retainedImageUrls?
            .Select(SharedStoragePaths.NormalizePublicImagePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var existingImages = await context.LocationImages
            .Where(item => item.LocationId == location.LocationId)
            .ToListAsync(cancellationToken);

        var removedImages = existingImages
            .Where(item => !retainedSet.Contains(SharedStoragePaths.NormalizePublicImagePath(item.ImageUrl) ?? item.ImageUrl))
            .ToList();

        if (removedImages.Count == 0)
        {
            return;
        }

        foreach (var removedImage in removedImages)
        {
            imageStorage.DeleteIfManaged(removedImage.ImageUrl);
        }

        context.LocationImages.RemoveRange(removedImages);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? GetInvalidImageFileName(IEnumerable<IFormFile>? imageFiles) =>
        imageFiles?
            .Where(item => item is not null && item.Length > 0)
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
}
