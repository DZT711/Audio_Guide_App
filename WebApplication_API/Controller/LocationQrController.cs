using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
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
public class LocationQrController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService,
    LocationQrService qrService,
    ILogger<LocationQrController> logger) : ControllerBase
{
    [HttpGet("location/{locationId:int}/status")]
    public async Task<IActionResult> GetLocationQrStatus(int locationId, CancellationToken cancellationToken)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.QrRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var location = await BuildScopedLocationQuery(access.User!)
            .FirstOrDefaultAsync(item => item.LocationId == locationId, cancellationToken);
        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        return Ok(qrService.BuildStatus(HttpContext, location, ResolveDefaultAudio(location)));
    }

    [HttpPost("location/{locationId:int}/generate")]
    public async Task<IActionResult> GenerateLocationQr(
        int locationId,
        [FromBody] LocationQrGenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.QrManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var location = await BuildScopedLocationQuery(access.User!)
            .FirstOrDefaultAsync(item => item.LocationId == locationId, cancellationToken);
        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        var file = qrService.GenerateLocationQrFile(HttpContext, location, ResolveDefaultAudio(location), request);
        logger.LogInformation(
            "User {UserId} generated a QR code for location {LocationId} in format {Format} at size {Size}.",
            access.User!.UserId,
            location.LocationId,
            request.Format,
            request.Size);

        await activityLogService.LogAsync(
            access.User!,
            "Generate",
            "QR",
            location.LocationId,
            location.Name,
            $"Generated QR for location '{location.Name}' as {request.Format} ({request.Size}px).",
            cancellationToken);

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("bulk/generate")]
    public async Task<IActionResult> GenerateBulkLocationQr(
        [FromBody] LocationQrBulkRequest request,
        CancellationToken cancellationToken)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.QrBulk);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var requestedLocationIds = request.LocationIds
            .Where(item => item > 0)
            .Distinct()
            .ToList();

        if (requestedLocationIds.Count == 0)
        {
            return BadRequest(new { message = "Choose at least one valid location." });
        }

        if (requestedLocationIds.Count > 100)
        {
            return BadRequest(new { message = "Bulk QR export supports up to 100 locations per request." });
        }

        var locations = await BuildScopedLocationQuery(access.User!)
            .Where(item => requestedLocationIds.Contains(item.LocationId))
            .ToListAsync(cancellationToken);

        var accessibleLocationLookup = locations.ToDictionary(item => item.LocationId);
        var failedLocationIds = requestedLocationIds
            .Where(item => !accessibleLocationLookup.ContainsKey(item))
            .ToList();

        await using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var locationId in requestedLocationIds)
            {
                if (!accessibleLocationLookup.TryGetValue(locationId, out var location))
                {
                    continue;
                }

                var file = qrService.GenerateLocationQrFile(
                    HttpContext,
                    location,
                    ResolveDefaultAudio(location),
                    new LocationQrGenerateRequest
                    {
                        Format = request.Format,
                        Size = request.Size,
                        Autoplay = request.Autoplay
                    });

                var entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(file.Content, cancellationToken);
            }
        }

        if (archiveStream.Length == 0)
        {
            return NotFound(new { message = "No accessible locations were available for QR export." });
        }

        if (failedLocationIds.Count > 0)
        {
            Response.Headers["X-SmartTour-Qr-Failed-Ids"] = string.Join(",", failedLocationIds);
            Response.Headers["X-SmartTour-Qr-Failed-Count"] = failedLocationIds.Count.ToString();
        }

        logger.LogInformation(
            "User {UserId} exported {SuccessCount} location QR files as a zip. Failed locations: {FailedIds}.",
            access.User!.UserId,
            requestedLocationIds.Count - failedLocationIds.Count,
            failedLocationIds.Count == 0 ? "none" : string.Join(",", failedLocationIds));

        await activityLogService.LogAsync(
            access.User!,
            "Export",
            "QR",
            null,
            "Bulk location QR",
            $"Exported {requestedLocationIds.Count - failedLocationIds.Count} location QR files in bulk. Failed: {failedLocationIds.Count}.",
            cancellationToken);

        archiveStream.Position = 0;
        return File(
            archiveStream.ToArray(),
            "application/zip",
            $"location-qr-bulk-{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
    }

    [AllowAnonymous]
    [HttpGet("public/location/{locationId:int}")]
    public async Task<IActionResult> OpenLocationLanding(
        int locationId,
        [FromQuery] bool autoplay = true,
        [FromQuery] int? audioTrackId = null,
        CancellationToken cancellationToken = default)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var location = await context.Locations
            .AsNoTracking()
            .Include(item => item.Owner)
            .Include(item => item.AudioContents)
            .FirstOrDefaultAsync(item => item.LocationId == locationId, cancellationToken);

        if (location is null)
        {
            return Content(
                qrService.RenderUnavailablePage(HttpContext, "Location not found", "The requested location QR code is no longer available."),
                "text/html; charset=utf-8");
        }

        if (location.Status != 1)
        {
            return Content(
                qrService.RenderUnavailablePage(HttpContext, "Location unavailable", $"'{location.Name}' is currently inactive."),
                "text/html; charset=utf-8");
        }

        logger.LogInformation(
            "Public location landing opened for location {LocationId} with autoplay={Autoplay} and audioTrackId={AudioTrackId}.",
            locationId,
            autoplay,
            audioTrackId);

        var html = qrService.RenderLocationLandingPage(
            HttpContext,
            location,
            ResolveDefaultAudio(location),
            new LocationQrGenerateRequest
            {
                Autoplay = autoplay,
                AudioTrackId = audioTrackId
            });

        return Content(html, "text/html; charset=utf-8");
    }

    [AllowAnonymous]
    [HttpGet("public/download")]
    public async Task<IActionResult> OpenDownloadPage(
        [FromQuery] string? openUrl = null,
        [FromQuery] string? locationName = null,
        [FromQuery] int? locationId = null,
        [FromQuery] bool autoplay = true,
        [FromQuery] int? audioTrackId = null,
        CancellationToken cancellationToken = default)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        if ((string.IsNullOrWhiteSpace(openUrl) || string.IsNullOrWhiteSpace(locationName))
            && locationId is > 0)
        {
            var location = await context.Locations
                .AsNoTracking()
                .Include(item => item.AudioContents)
                .FirstOrDefaultAsync(item => item.LocationId == locationId.Value, cancellationToken);
            if (location is not null)
            {
                var status = qrService.BuildStatus(
                    HttpContext,
                    location,
                    ResolveDefaultAudio(location),
                    new LocationQrGenerateRequest
                    {
                        Autoplay = autoplay,
                        AudioTrackId = audioTrackId
                    });

                openUrl ??= status.DeepLinkUrl;
                locationName ??= status.LocationName;
            }
        }

        logger.LogInformation(
            "Public Smart Tourism download page opened for location {LocationId}.",
            locationId);
        return Content(qrService.RenderDownloadPage(HttpContext, openUrl, locationName), "text/html; charset=utf-8");
    }

    [AllowAnonymous]
    [HttpGet("public/android-apk")]
    public IActionResult RedirectAndroidApk()
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var installUrl = qrService.ResolveConfiguredAndroidInstallUrl();
        if (string.IsNullOrWhiteSpace(installUrl))
        {
            logger.LogWarning("Android APK redirect requested but no Android install URL is configured.");
            return NotFound(new { message = "Android install URL is not configured." });
        }

        logger.LogInformation("Redirecting to the configured Android install URL.");
        return Redirect(installUrl);
    }

    [AllowAnonymous]
    [HttpGet("public/android-apk/qr")]
    public IActionResult GetAndroidApkQr([FromQuery] string? format = null, [FromQuery] int? size = null)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var file = qrService.GenerateAndroidApkQrFile(HttpContext, format, size);
        logger.LogInformation("Generated a public Android APK QR image in format {Format}.", format ?? "png");
        Response.Headers.CacheControl = "public,max-age=300";
        return File(file.Content, file.ContentType, file.FileName);
    }

    private IQueryable<Location> BuildScopedLocationQuery(DashboardUser currentUser)
    {
        var query = context.Locations
            .Include(item => item.Owner)
            .Include(item => item.AudioContents)
            .AsQueryable();

        return AdminOwnershipScope.ApplyLocationScope(query, currentUser);
    }

    private static Audio? ResolveDefaultAudio(Location location) =>
        location.AudioContents
            .Where(item => item.Status == 1)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.AudioId)
            .FirstOrDefault();

    private static int ResolveSourceTypeOrder(string? sourceType) =>
        sourceType?.Trim().ToUpperInvariant() switch
        {
            "RECORDED" => 0,
            "HYBRID" => 1,
            _ => 2
        };
}
