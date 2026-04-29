using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Geofencing;
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
    AndroidApkPackagingService apkPackagingService,
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

        var location = await LoadPublicLocationAsync(locationId, cancellationToken);

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

        try
        {
            context.QrLandingVisits.Add(new QrLandingVisit
            {
                LocationId = locationId,
                OpenedAt = DateTime.UtcNow,
                Source = "qr-landing",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString(),
                Referrer = HttpContext.Request.Headers.Referer.ToString()
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to persist QR landing visit telemetry for location {LocationId}.", locationId);
        }

        var insights = await BuildLocationLandingInsightsAsync(location, cancellationToken);

        var html = qrService.RenderLocationLandingPage(
            HttpContext,
            location,
            ResolveDefaultAudio(location),
            new LocationQrGenerateRequest
            {
                Autoplay = autoplay,
                AudioTrackId = audioTrackId
            },
            insights);

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

        Location? location = null;
        if (locationId is > 0)
        {
            location = await LoadPublicLocationAsync(locationId.Value, cancellationToken);
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

        if (location is not null && location.Status == 1)
        {
            var insights = await BuildLocationLandingInsightsAsync(location, cancellationToken);
            return Content(
                qrService.RenderDownloadPage(
                    HttpContext,
                    openUrl,
                    locationName,
                    location,
                    ResolveDefaultAudio(location),
                    new LocationQrGenerateRequest
                    {
                        Autoplay = autoplay,
                        AudioTrackId = audioTrackId
                    },
                    insights),
                "text/html; charset=utf-8");
        }

        return Content(qrService.RenderDownloadPage(HttpContext, openUrl, locationName), "text/html; charset=utf-8");
    }

    [AllowAnonymous]
    [HttpGet("public/android-apk")]
    public async Task<IActionResult> RedirectAndroidApk(CancellationToken cancellationToken)
    {
        if (!qrService.IsEnabled)
        {
            return NotFound(new { message = "QR features are disabled." });
        }

        var publicBaseUrl = ResolvePublicBaseUrl();
        var localPackage = apkPackagingService.TryGetLatestLocalPackage(publicBaseUrl);
        if (localPackage is not null)
        {
            logger.LogInformation(
                "Serving Android APK from local latest artifact {PhysicalPath}.",
                localPackage.PhysicalPath);
            Response.Headers.CacheControl = "public,max-age=300";
            return PhysicalFile(
                localPackage.PhysicalPath,
                localPackage.ContentType,
                localPackage.DownloadFileName,
                enableRangeProcessing: true);
        }

        logger.LogInformation("No valid local latest APK artifact found. Evaluating configured fallback URLs.");

        var configuredApkUrl = apkPackagingService.ResolveConfiguredAndroidApkUrl();
        if (!string.IsNullOrWhiteSpace(configuredApkUrl))
        {
            logger.LogInformation("Redirecting Android APK request to configured AndroidApkUrl.");
            return Redirect(configuredApkUrl);
        }

        var storeUrl = apkPackagingService.ResolveConfiguredAndroidStoreUrl();
        if (!string.IsNullOrWhiteSpace(storeUrl))
        {
            logger.LogInformation("AndroidApkUrl is not configured. Redirecting Android APK request to AndroidStoreUrl.");
            return Redirect(storeUrl);
        }

        if (apkPackagingService.IsDynamicBuildEnabled)
        {
            logger.LogInformation("No local APK and no configured URL fallback. Trying dynamic APK packaging as last resort.");
            try
            {
                var package = await apkPackagingService.GetOrBuildPackageAsync(
                    publicBaseUrl,
                    cancellationToken);
                logger.LogInformation("Serving dynamically packaged Android APK from {PhysicalPath}.", package.PhysicalPath);
                Response.Headers.CacheControl = "public,max-age=300";
                return PhysicalFile(
                    package.PhysicalPath,
                    package.ContentType,
                    package.DownloadFileName,
                    enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dynamic Android APK packaging failed.");
            }
        }

        logger.LogWarning(
            "Android APK redirect requested but no local artifact or configured URLs are available. Dynamic enabled={DynamicEnabled}.",
            apkPackagingService.IsDynamicBuildEnabled);
        return StatusCode(503, new { message = "Android APK is not available right now. Configure local latest artifact, AndroidApkUrl, or AndroidStoreUrl." });
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

    private Task<Location?> LoadPublicLocationAsync(int locationId, CancellationToken cancellationToken) =>
        context.Locations
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Owner)
            .Include(item => item.Category)
            .Include(item => item.AudioContents)
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.LocationId == locationId, cancellationToken);

    private async Task<LocationQrService.LocationLandingInsights> BuildLocationLandingInsightsAsync(
        Location location,
        CancellationToken cancellationToken)
    {
        var locationId = location.LocationId;
        var last7DaysUtc = DateTime.UtcNow.AddDays(-7);

        var allTimeVisits = await context.QrLandingVisits
            .AsNoTracking()
            .CountAsync(item => item.LocationId == locationId, cancellationToken);
        var recentVisits = await context.QrLandingVisits
            .AsNoTracking()
            .CountAsync(item => item.LocationId == locationId && item.OpenedAt >= last7DaysUtc, cancellationToken);
        var audioPlayCount = await context.PlaybackEvents
            .AsNoTracking()
            .CountAsync(item => item.LocationId == locationId, cancellationToken);
        var latestPlaybackUtc = await context.PlaybackEvents
            .AsNoTracking()
            .Where(item => item.LocationId == locationId)
            .Select(item => (DateTime?)item.EventAt)
            .OrderByDescending(item => item)
            .FirstOrDefaultAsync(cancellationToken);
        var latestListeningUtc = await context.AudioListeningSessions
            .AsNoTracking()
            .Where(item => item.LocationId == locationId)
            .Select(item => (DateTime?)item.EndedAt)
            .OrderByDescending(item => item)
            .FirstOrDefaultAsync(cancellationToken);
        var latestVisitUtc = await context.QrLandingVisits
            .AsNoTracking()
            .Where(item => item.LocationId == locationId)
            .Select(item => (DateTime?)item.OpenedAt)
            .OrderByDescending(item => item)
            .FirstOrDefaultAsync(cancellationToken);
        var latestUpdatedUtc = new[]
        {
            location.UpdatedAt,
            location.CreatedAt,
            latestPlaybackUtc,
            latestListeningUtc,
            latestVisitUtc
        }.Where(item => item.HasValue)
            .Max();

        var activeVenueCount = await context.Locations
            .AsNoTracking()
            .CountAsync(item => item.Status == 1, cancellationToken);
        var activeAudioCount = await context.AudioContents
            .AsNoTracking()
            .CountAsync(item => item.Status == 1, cancellationToken);

        var candidates = await context.Locations
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.AudioContents)
            .Where(item => item.Status == 1 && item.LocationId != locationId)
            .ToListAsync(cancellationToken);

        var relatedLocations = candidates
            .Select(item => new
            {
                Location = item,
                SameCategory = item.CategoryId.HasValue && item.CategoryId == location.CategoryId,
                DistanceMeters = HaversineDistanceCalculator.CalculateMeters(
                    location.Latitude,
                    location.Longitude,
                    item.Latitude,
                    item.Longitude)
            })
            .OrderByDescending(item => item.SameCategory)
            .ThenBy(item => item.DistanceMeters)
            .ThenByDescending(item => item.Location.Priority)
            .Take(3)
            .Select(item =>
            {
                var status = qrService.BuildStatus(HttpContext, item.Location, ResolveDefaultAudio(item.Location));
                return new LocationQrService.LocationLandingRelatedLocationInfo
                {
                    LocationId = item.Location.LocationId,
                    Name = item.Location.Name,
                    CategoryName = item.Location.Category?.Name ?? "Location",
                    CategoryIcon = item.Location.Category?.IconEmoji,
                    AudioCount = item.Location.AudioContents.Count(audio => audio.Status == 1),
                    DistanceLabel = FormatDistanceLabel(item.DistanceMeters),
                    Url = status.LandingUrl
                };
            })
            .ToList();

        var nextStop = relatedLocations.FirstOrDefault();
        var projectName = ResolveProjectName(location);

        return new LocationQrService.LocationLandingInsights
        {
            CategoryName = location.Category?.Name,
            OpeningHours = null,
            ImageUrl = location.PreferenceImageUrl,
            VisitCountAllTime = allTimeVisits,
            VisitCountLast7Days = recentVisits,
            AudioPlayCount = audioPlayCount,
            LastUpdatedUtc = latestUpdatedUtc,
            ProjectName = projectName,
            QrId = $"LOC-{location.LocationId:D4}",
            VenueCount = activeVenueCount,
            AudioGuideCountTotal = activeAudioCount,
            Rating = 4.8,
            RankLabel = allTimeVisits > 0
                ? $"{allTimeVisits} QR scans tracked"
                : $"Featured on {projectName}",
            FunFact = location.EstablishedYear is > 0
                ? $"{location.Name} has been part of the local story since {location.EstablishedYear.Value}."
                : null,
            Tip = string.IsNullOrWhiteSpace(location.PhoneContact)
                ? null
                : "Use the call button if you want to check availability before you head over.",
            RelatedLocations = relatedLocations,
            NextStop = nextStop is null
                ? null
                : new LocationQrService.LocationLandingNextStopInfo
                {
                    Name = nextStop.Name,
                    CategoryIcon = nextStop.CategoryIcon,
                    DistanceLabel = $"Next stop · {nextStop.DistanceLabel}",
                    Label = $"🗺 {projectName} · Next stop",
                    ButtonLabel = "Continue Tour → Next Stop",
                    Url = nextStop.Url
                }
        };
    }

    private static string ResolveProjectName(Location location)
    {
        var address = location.Address ?? string.Empty;
        if (address.Contains("Vinh Khanh", StringComparison.OrdinalIgnoreCase))
        {
            return "Vinh Khanh Food Street";
        }

        if (address.Contains("District 4", StringComparison.OrdinalIgnoreCase))
        {
            return "District 4 Discovery";
        }

        return "Smart Tourism";
    }

    private static string FormatDistanceLabel(double distanceMeters)
    {
        if (distanceMeters < 1000d)
        {
            return $"{Math.Round(distanceMeters, MidpointRounding.AwayFromZero)}m";
        }

        return $"{distanceMeters / 1000d:0.0}km";
    }

    private string ResolvePublicBaseUrl()
    {
        // Prioritize the configured PublicBaseUrl from appsettings.json if available
        if (!string.IsNullOrWhiteSpace(qrService.ResolveConfiguredAndroidInstallUrl()) &&
            Uri.TryCreate(qrService.ResolveConfiguredAndroidInstallUrl(), UriKind.Absolute, out var uri))
        {
            return uri.AbsoluteUri.EndsWith("/") ? uri.AbsoluteUri : uri.AbsoluteUri + "/";
        }

        var pathBase = HttpContext.Request.PathBase.HasValue
            ? HttpContext.Request.PathBase.Value!.Trim('/').Trim()
            : string.Empty;

        return string.IsNullOrWhiteSpace(pathBase)
            ? $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/"
            : $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/{pathBase}/";
    }
}
