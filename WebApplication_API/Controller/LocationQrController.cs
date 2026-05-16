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
    // l3 quét qr admin
    //l37 POI- Phương thức GET-lấy trạng thái QR (chỉnh format,size QR,...)
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
// l6 quet qr admin 
        return Ok(qrService.BuildStatus(HttpContext, location, ResolveDefaultAudio(location)));
    }
//l37 Locations- Phương thức POST-tạo qr cho POI chỉ định 
//l12 quét qr admin
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
// l13 quét qr admin
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.QrManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
//l13 quét qr admin + l14 quét qr admin
        var location = await BuildScopedLocationQuery(access.User!)
            .FirstOrDefaultAsync(item => item.LocationId == locationId, cancellationToken);
        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }
// l14 quét qr admin + l15 quét qr admin
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
            // l17 quét qr admin
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

        var location = await renLocationAsync(locationId, cancellationToken);

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
            await RecordDeviceCheckAsync(location, cancellationToken);
            var insights = await BuildLocationLandingInsightsAsync(location, cancellationToken);
            var qrOverview = await BuildQrOverviewAsync([location.LocationId], cancellationToken);
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
                    insights,
                    qrOverview),
                "text/html; charset=utf-8");
        }

        return Content(qrService.RenderDownloadPage(HttpContext, openUrl, locationName), "text/html; charset=utf-8");
    }

    [HttpGet("admin/overview")]
    public async Task<IActionResult> GetAdminOverview(
        [FromQuery] int? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AnalyticsView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var accessibleLocations = BuildScopedLocationQuery(access.User!)
            .AsNoTracking()
            .Where(item => item.Status == 1);

        IReadOnlyCollection<int> scopedLocationIds;
        if (locationId is > 0)
        {
            var scopedLocationId = await accessibleLocations
                .Where(item => item.LocationId == locationId.Value)
                .Select(item => (int?)item.LocationId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!scopedLocationId.HasValue)
            {
                return NotFound(new { message = "Location not found." });
            }

            scopedLocationIds = [scopedLocationId.Value];
        }
        else
        {
            scopedLocationIds = await accessibleLocations
                .Select(item => item.LocationId)
                .ToListAsync(cancellationToken);
        }

        var overview = await BuildQrOverviewAsync(scopedLocationIds, cancellationToken);
        return Ok(overview);
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
// l4 quét qr admin
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

    private async Task RecordDeviceCheckAsync(Location location, CancellationToken cancellationToken)
    {
        var weakScore = Random.Shared.Next(0, 2);
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
        var deviceInfo = ResolveDeviceInfo(userAgent);
        if (!deviceInfo.IsMobile)
        {
            return;
        }

        try
        {
            context.QrDeviceCheckLogs.Add(new QrDeviceCheckLog
            {
                LocationId = location.LocationId,
                OpenedAt = DateTime.UtcNow,
                DeviceName = deviceInfo.DeviceName,
                Platform = deviceInfo.Platform,
                OsVersion = deviceInfo.OsVersion,
                QrCode = $"LOC-{location.LocationId:D4}",
                WeakScore = weakScore,
                UserAgent = userAgent
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to persist QR device-check telemetry for location {LocationId}.", location.LocationId);
        }
    }

    private async Task<QrOverviewDto> BuildQrOverviewAsync(
        IReadOnlyCollection<int> locationIds,
        CancellationToken cancellationToken)
    {
        if (locationIds.Count == 0)
        {
            return new QrOverviewDto();
        }

        var scopedLocationIds = locationIds.ToList();
        var last7DaysUtc = DateTime.UtcNow.AddDays(-7);

        var visitCountAllTime = await context.QrLandingVisits
            .AsNoTracking()
            .CountAsync(item => scopedLocationIds.Contains(item.LocationId), cancellationToken);
        var visitCountLast7Days = await context.QrLandingVisits
            .AsNoTracking()
            .CountAsync(item =>
                scopedLocationIds.Contains(item.LocationId)
                && item.OpenedAt >= last7DaysUtc,
                cancellationToken);
        var audioGuideCount = await context.AudioContents
            .AsNoTracking()
            .CountAsync(item =>
                scopedLocationIds.Contains(item.LocationId)
                && item.Status == 1,
                cancellationToken);
        var mobileDeviceChecks = context.QrDeviceCheckLogs
            .AsNoTracking()
            .Where(item =>
                scopedLocationIds.Contains(item.LocationId)
                && (item.Platform == "Android"
                    || item.Platform == "iOS"
                    || item.DeviceName == "Điện thoại"
                    || item.DeviceName == "Máy tính bảng"));
        var deviceCheckTotal = await mobileDeviceChecks
            .CountAsync(cancellationToken);
        var weakCount = await mobileDeviceChecks
            .CountAsync(item => item.WeakScore == 1, cancellationToken);
        var strongCount = Math.Max(0, deviceCheckTotal - weakCount);
        var recentLogItems = await mobileDeviceChecks
            .OrderByDescending(item => item.OpenedAt)
            .ThenByDescending(item => item.QrDeviceCheckLogId)
            .Take(10)
            .Select(item => new
            {
                item.OpenedAt,
                item.DeviceName,
                item.Platform,
                item.OsVersion,
                item.QrCode,
                item.WeakScore
            })
            .ToListAsync(cancellationToken);

        return new QrOverviewDto
        {
            QrAnalytics = new QrAnalyticsOverviewDto
            {
                VisitCountAllTime = visitCountAllTime,
                VisitCountLast7Days = visitCountLast7Days,
                AudioGuideCount = audioGuideCount,
                Rating = 4.8
            },
            DeviceCheck = new QrDeviceCheckOverviewDto
            {
                TotalScans = deviceCheckTotal,
                StrongCount = strongCount,
                WeakCount = weakCount,
                WeakRatePercent = deviceCheckTotal == 0
                    ? 0d
                    : Math.Round((weakCount / (double)deviceCheckTotal) * 100d, 2, MidpointRounding.AwayFromZero)
            },
            RecentLogs = recentLogItems
                .Select(item => new QrDeviceCheckLogDto
                {
                    Time = item.OpenedAt,
                    DeviceName = NormalizeDeviceText(item.DeviceName),
                    Platform = NormalizeDeviceText(item.Platform),
                    OsVersion = NormalizeDeviceText(item.OsVersion),
                    QrCode = string.IsNullOrWhiteSpace(item.QrCode) ? "QR" : item.QrCode.Trim(),
                    WeakScore = item.WeakScore == 1 ? 1 : 0
                })
                .ToList()
        };
    }

    private static DeviceInfo ResolveDeviceInfo(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return new DeviceInfo("Không xác định", "Không xác định", "Không xác định", false);
        }

        var normalized = userAgent.ToLowerInvariant();
        var isTablet = normalized.Contains("ipad", StringComparison.Ordinal)
            || normalized.Contains("tablet", StringComparison.Ordinal)
            || normalized.Contains("kindle", StringComparison.Ordinal)
            || normalized.Contains("silk", StringComparison.Ordinal);
        var isPhone = normalized.Contains("mobi", StringComparison.Ordinal)
            || normalized.Contains("android", StringComparison.Ordinal)
            || normalized.Contains("iphone", StringComparison.Ordinal)
            || normalized.Contains("ipod", StringComparison.Ordinal);
        var deviceName = isTablet
            ? "Máy tính bảng"
            : isPhone
                ? "Điện thoại"
                : "Máy tính để bàn";

        var os = normalized.Contains("android", StringComparison.Ordinal)
            ? "Android"
            : normalized.Contains("iphone", StringComparison.Ordinal)
              || normalized.Contains("ipad", StringComparison.Ordinal)
              || normalized.Contains("ipod", StringComparison.Ordinal)
                ? "iOS"
                : normalized.Contains("windows nt", StringComparison.Ordinal)
                    ? "Windows"
                    : normalized.Contains("mac os x", StringComparison.Ordinal)
                      || normalized.Contains("macintosh", StringComparison.Ordinal)
                        ? "macOS"
                        : normalized.Contains("linux", StringComparison.Ordinal)
                            ? "Linux"
                            : "Không xác định";

        return new DeviceInfo(deviceName, os, os, isTablet || isPhone);
    }

    private static string NormalizeDeviceText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Không xác định" : value.Trim();

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

    private sealed record DeviceInfo(
        string DeviceName,
        string Platform,
        string OsVersion,
        bool IsMobile);
}
