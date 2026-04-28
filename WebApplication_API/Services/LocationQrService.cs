using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using SkiaSharp;
using WebApplication_API.Model;
using ZXing;
using ZXing.Common;

namespace WebApplication_API.Services;

public sealed class LocationQrService(
    IOptions<QrLinkOptions> options,
    IWebHostEnvironment hostEnvironment,
    ILogger<LocationQrService> logger)
{
    private readonly QrLinkOptions _options = options.Value;
    private readonly ILogger<LocationQrService> _logger = logger;
    private readonly string _landingTemplatePath = Path.Combine(
        hostEnvironment.ContentRootPath,
        "Templates",
        "smart-tourism-qr-landing.template.html");

    public bool IsEnabled => _options.Enabled;

    public LocationQrStatusDto BuildStatus(
        HttpContext httpContext,
        Location location,
        Audio? defaultAudio,
        LocationQrGenerateRequest? request = null)
    {
        var normalizedRequest = request is null
            ? NormalizeStoredRequest(location)
            : NormalizeRequest(request);
        var links = BuildLocationLinks(
            httpContext,
            location.LocationId,
            location.Name,
            defaultAudio?.AudioId,
            normalizedRequest.Autoplay,
            normalizedRequest.AudioTrackId);

        return new LocationQrStatusDto
        {
            LocationId = location.LocationId,
            LocationName = location.Name,
            Status = location.Status,
            OwnerId = location.OwnerId,
            OwnerName = location.Owner?.FullName ?? location.Owner?.Username,
            FeatureEnabled = IsEnabled,
            HasDefaultAudio = defaultAudio is not null,
            DefaultAudioId = defaultAudio?.AudioId,
            DefaultAudioTitle = defaultAudio?.Title,
            LandingUrl = links.LandingUrl,
            DeepLinkUrl = links.DeepLinkUrl,
            DownloadPageUrl = links.DownloadPageUrl,
            AndroidApkUrl = links.AndroidApkUrl,
            AndroidApkQrUrl = links.AndroidApkQrUrl,
            SuggestedFileNameBase = BuildFileNameBase(location.LocationId, location.Name),
            DefaultSize = normalizedRequest.Size,
            DefaultFormat = normalizedRequest.Format,
            DefaultAutoplay = normalizedRequest.Autoplay,
            DefaultAudioTrackId = normalizedRequest.AudioTrackId
        };
    }

    public QrRenderedFile GenerateLocationQrFile(
        HttpContext httpContext,
        Location location,
        Audio? defaultAudio,
        LocationQrGenerateRequest? request = null)
    {
        var normalizedRequest = NormalizeRequest(request);
        var links = BuildLocationLinks(
            httpContext,
            location.LocationId,
            location.Name,
            defaultAudio?.AudioId,
            normalizedRequest.Autoplay,
            normalizedRequest.AudioTrackId);

        return RenderQrFile(
            links.LandingUrl,
            BuildFileNameBase(location.LocationId, location.Name),
            normalizedRequest.Format,
            normalizedRequest.Size);
    }

    public QrRenderedFile GenerateAndroidApkQrFile(
        HttpContext httpContext,
        string? format = null,
        int? size = null)
    {
        var normalizedFormat = NormalizeFormat(format);
        var normalizedSize = NormalizeSize(size);
        var installUrl = BuildAbsoluteUrl(httpContext, ApiRoutes.PublicAndroidApkDownload);

        return RenderQrFile(
            installUrl,
            "smarttour-android-download",
            normalizedFormat,
            normalizedSize);
    }

    public string RenderLocationLandingPage(
        HttpContext httpContext,
        Location location,
        Audio? defaultAudio,
        LocationQrGenerateRequest? request = null,
        LocationLandingInsights? insights = null)
    {
        var normalizedRequest = NormalizeRequest(request);
        var links = BuildLocationLinks(
            httpContext,
            location.LocationId,
            location.Name,
            defaultAudio?.AudioId,
            normalizedRequest.Autoplay,
            normalizedRequest.AudioTrackId);
        return RenderRichLocationPage(
            httpContext,
            location,
            defaultAudio,
            insights,
            links,
            autoOpenDeepLink: true);
    }

    public string RenderDownloadPage(
        HttpContext httpContext,
        string? openUrl = null,
        string? locationName = null,
        Location? location = null,
        Audio? defaultAudio = null,
        LocationQrGenerateRequest? request = null,
        LocationLandingInsights? insights = null)
    {
        if (location is not null)
        {
            var normalizedRequest = NormalizeRequest(request);
            var links = BuildLocationLinks(
                httpContext,
                location.LocationId,
                location.Name,
                defaultAudio?.AudioId,
                normalizedRequest.Autoplay,
                normalizedRequest.AudioTrackId);

            return RenderRichLocationPage(
                httpContext,
                location,
                defaultAudio,
                insights,
                links,
                autoOpenDeepLink: false);
        }

        var apkUrl = WebUtility.HtmlEncode(BuildAbsoluteUrl(httpContext, ApiRoutes.PublicAndroidApkDownload));
        var apkQrUrl = WebUtility.HtmlEncode(
            $"{BuildAbsoluteUrl(httpContext, ApiRoutes.PublicAndroidApkQr)}?size=320&format=png");
        var sanitizedOpenUrl = SanitizeOpenUrl(openUrl);
        var openUrlHtml = sanitizedOpenUrl is null ? string.Empty : WebUtility.HtmlEncode(sanitizedOpenUrl);
        var storeUrlHtml = string.IsNullOrWhiteSpace(_options.AndroidStoreUrl)
            ? string.Empty
            : WebUtility.HtmlEncode(_options.AndroidStoreUrl);
        var locationLine = string.IsNullOrWhiteSpace(locationName)
            ? "Download the Android app, then return here and tap Open App to continue."
            : $"Install the Android app to continue to {WebUtility.HtmlEncode(locationName)}.";
        var installConfigured = !string.IsNullOrWhiteSpace(_options.AndroidApkUrl)
            || !string.IsNullOrWhiteSpace(_options.AndroidStoreUrl)
            || _options.EnableDynamicAndroidApkBuild;

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Download Smart Tourism</title>
    <style>
        :root {
            color-scheme: light;
            font-family: "Segoe UI", system-ui, sans-serif;
            --bg: #fffdf5;
            --panel: rgba(255,255,255,0.96);
            --text: #111827;
            --muted: #4b5563;
            --accent: #b45309;
            --accent-soft: #fef3c7;
            --border: rgba(148, 163, 184, 0.26);
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 1.5rem;
            background:
                radial-gradient(circle at top left, rgba(245, 158, 11, 0.18), transparent 28%),
                linear-gradient(160deg, #fff7ed 0%, #fffbeb 100%);
            color: var(--text);
        }

        main {
            width: min(100%, 860px);
            display: grid;
            gap: 1.4rem;
            padding: 2rem;
            border-radius: 28px;
            border: 1px solid var(--border);
            background: var(--panel);
            box-shadow: 0 24px 60px rgba(15, 23, 42, 0.12);
        }

        .hero {
            display: grid;
            gap: 0.8rem;
        }

        .eyebrow {
            display: inline-flex;
            width: fit-content;
            padding: 0.35rem 0.75rem;
            border-radius: 999px;
            background: var(--accent-soft);
            color: var(--accent);
            font-size: 0.78rem;
            font-weight: 700;
            letter-spacing: 0.08em;
            text-transform: uppercase;
        }

        h1 {
            margin: 0;
            font-size: clamp(2rem, 4vw, 3.2rem);
            line-height: 1.1;
        }

        p {
            margin: 0;
            color: var(--muted);
            line-height: 1.7;
        }

        .grid {
            display: grid;
            grid-template-columns: minmax(0, 1.2fr) minmax(260px, 320px);
            gap: 1.2rem;
        }

        .card {
            display: grid;
            gap: 1rem;
            padding: 1.25rem;
            border-radius: 22px;
            border: 1px solid var(--border);
            background: rgba(255,255,255,0.92);
        }

        .actions {
            display: flex;
            flex-wrap: wrap;
            gap: 0.8rem;
        }

        .btn {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-height: 3rem;
            padding: 0.85rem 1.25rem;
            border-radius: 16px;
            border: 1px solid transparent;
            text-decoration: none;
            font-weight: 700;
        }

        .btn-primary {
            color: white;
            background: var(--accent);
        }

        .btn-secondary {
            color: var(--text);
            background: white;
            border-color: var(--border);
        }

        .qr-box {
            justify-items: center;
            text-align: center;
        }

        .qr-box img {
            width: min(100%, 260px);
            border-radius: 20px;
            background: white;
            border: 1px solid var(--border);
            padding: 0.8rem;
        }

        .warning {
            padding: 0.95rem 1rem;
            border-radius: 16px;
            background: rgba(254, 243, 199, 0.8);
            color: #92400e;
            font-weight: 600;
        }

        @media (max-width: 760px) {
            .grid {
                grid-template-columns: 1fr;
            }
        }
    </style>
</head>
<body>
    <main>
        <div class="hero">
            <span class="eyebrow">Smart Tourism</span>
            <h1>Download the Android app</h1>
            <p>{{locationLine}}</p>
        </div>

        <div class="grid">
            <section class="card">
                <h2>Install on Android</h2>
                <p>Use the direct APK download, or open the store listing if your deployment also publishes there.</p>

                {{(installConfigured ? string.Empty : """<div class="warning">Android install URLs are not configured yet. Update the QrLinks settings before sharing this page.</div>""")}}

                <div class="actions">
                    <a class="btn btn-primary" href="{{apkUrl}}">Download APK</a>
                    {{(string.IsNullOrWhiteSpace(storeUrlHtml) ? string.Empty : $"<a class=\"btn btn-secondary\" href=\"{storeUrlHtml}\">Open Store</a>")}}
                    {{(string.IsNullOrWhiteSpace(openUrlHtml) ? string.Empty : $"<a class=\"btn btn-secondary\" href=\"{openUrlHtml}\">Open App</a>")}}
                </div>
            </section>

            <aside class="card qr-box">
                <h2>Scan to download</h2>
                <img src="{{apkQrUrl}}" alt="QR code for Android APK download" />
                <p>Share this QR if users need a direct Android installer.</p>
            </aside>
        </div>
    </main>
</body>
</html>
""";
    }

    public string RenderUnavailablePage(HttpContext httpContext, string title, string message)
    {
        var downloadPageUrl = WebUtility.HtmlEncode(BuildAbsoluteUrl(httpContext, ApiRoutes.GetPublicLocationQrDownloadPage()));
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeMessage = WebUtility.HtmlEncode(message);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{safeTitle}}</title>
    <style>
        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 1.5rem;
            background: #f8fafc;
            color: #111827;
            font-family: "Segoe UI", system-ui, sans-serif;
        }

        main {
            width: min(100%, 640px);
            padding: 2rem;
            border-radius: 24px;
            border: 1px solid rgba(148, 163, 184, 0.24);
            background: white;
            box-shadow: 0 18px 44px rgba(15, 23, 42, 0.1);
        }

        h1 { margin-top: 0; }
        p { line-height: 1.7; color: #475569; }

        a {
            display: inline-flex;
            margin-top: 1rem;
            padding: 0.85rem 1.2rem;
            border-radius: 14px;
            background: #0f766e;
            color: white;
            text-decoration: none;
            font-weight: 700;
        }
    </style>
</head>
<body>
    <main>
        <h1>{{safeTitle}}</h1>
        <p>{{safeMessage}}</p>
        <a href="{{downloadPageUrl}}">Download Smart Tourism</a>
    </main>
</body>
</html>
""";
    }

    private string RenderRichLocationPage(
        HttpContext httpContext,
        Location location,
        Audio? defaultAudio,
        LocationLandingInsights? insights,
        LocationQrResolvedLinks links,
        bool autoOpenDeepLink)
    {
        var resolvedInsights = insights ?? new LocationLandingInsights();
        var theme = ResolveThemeDefinition(location);
        var categoryName = string.IsNullOrWhiteSpace(resolvedInsights.CategoryName)
            ? location.Category?.Name ?? "Uncategorized"
            : resolvedInsights.CategoryName.Trim();
        var openingHours = string.IsNullOrWhiteSpace(resolvedInsights.OpeningHours)
            ? _options.LandingDefaultOpeningHours
            : resolvedInsights.OpeningHours.Trim();
        var heroImageUrl = ResolveLocationHeroImageUrl(httpContext, location, resolvedInsights.ImageUrl);
        var galleryImages = BuildGalleryImages(httpContext, location, heroImageUrl);
        var audioTrack = defaultAudio ?? ResolvePreferredAudio(location);
        var audioUrl = NormalizePublicAssetUrl(httpContext, audioTrack?.FilePath);
        var audioGuideCount = location.AudioContents.Count(item => item.Status == 1);
        var relatedLocations = resolvedInsights.RelatedLocations
            .Select(item => new
            {
                name = item.Name,
                categoryName = item.CategoryName,
                icon = string.IsNullOrWhiteSpace(item.CategoryIcon) ? "📍" : item.CategoryIcon,
                audioCount = item.AudioCount,
                distanceLabel = item.DistanceLabel,
                url = item.Url
            })
            .ToList();
        var nextStop = resolvedInsights.NextStop;
        if (nextStop is null)
        {
            var inferredNextStop = resolvedInsights.RelatedLocations.FirstOrDefault();
            if (inferredNextStop is not null)
            {
                nextStop = new LocationLandingNextStopInfo
                {
                    Name = inferredNextStop.Name,
                    CategoryIcon = inferredNextStop.CategoryIcon,
                    DistanceLabel = inferredNextStop.DistanceLabel,
                    Url = inferredNextStop.Url
                };
            }
        }
        var hoursCard = BuildHoursCard(openingHours);
        var websiteUrl = SanitizeWebsiteUrl(location.WebURL);
        var phoneUrl = BuildTelephoneUrl(location.PhoneContact);
        var mapUrl = BuildMapUrl(location);
        var badge = string.IsNullOrWhiteSpace(resolvedInsights.Badge)
            ? $"{theme.DisplayName} · Smart Tourism"
            : resolvedInsights.Badge.Trim();
        var establishedYear = location.EstablishedYear is > 0
            ? location.EstablishedYear.Value.ToString()
            : "—";
        var rankLabel = string.IsNullOrWhiteSpace(resolvedInsights.RankLabel)
            ? $"Featured {theme.DisplayName}"
            : resolvedInsights.RankLabel.Trim();
        var funFact = string.IsNullOrWhiteSpace(resolvedInsights.FunFact)
            ? BuildFallbackFunFact(location, categoryName)
            : resolvedInsights.FunFact.Trim();
        var tip = string.IsNullOrWhiteSpace(resolvedInsights.Tip)
            ? BuildFallbackTip(location, openingHours)
            : resolvedInsights.Tip.Trim();
        var pageTitle = autoOpenDeepLink
            ? $"Open {location.Name} | Smart Tourism"
            : $"Download Smart Tourism for {location.Name}";
        var appSubtitle = autoOpenDeepLink
            ? $"If the app does not open automatically, install Smart Tourism to continue exploring {location.Name} with offline maps, audio guides and more."
            : $"Install the app to continue exploring {location.Name} with offline maps, audio guides and more.";

        var pageData = new
        {
            pageTitle,
            locationId = location.LocationId,
            locationName = location.Name,
            categoryName,
            categoryTheme = theme.ThemeName,
            categoryIcon = string.IsNullOrWhiteSpace(location.Category?.IconEmoji) ? theme.Emoji : location.Category!.IconEmoji,
            categoryLineSuffix = string.IsNullOrWhiteSpace(resolvedInsights.ProjectName) ? "Smart Tourism" : resolvedInsights.ProjectName.Trim(),
            badge,
            description = string.IsNullOrWhiteSpace(location.Description)
                ? "Smart Tourism is preparing more stories and travel context for this stop."
                : location.Description.Trim(),
            address = string.IsNullOrWhiteSpace(location.Address) ? "Address is being updated." : location.Address.Trim(),
            openingHours,
            establishedLabel = location.EstablishedYear is > 0 ? $"Serving since {location.EstablishedYear.Value}" : "Year unavailable",
            establishedYear,
            heroImageUrl,
            images = galleryImages,
            audio = audioTrack is null
                ? new
                {
                    title = "Audio Guide",
                    titles = new { vi = "Audio Guide", en = "Audio Guide" },
                    durationSeconds = 0,
                    sourceType = "Audio Guide",
                    defaultLanguage = "vi",
                    url = (string?)null
                }
                : new
                {
                    title = audioTrack.Title,
                    titles = new
                    {
                        vi = ResolveLanguageAudioTitle(location, "vi", audioTrack),
                        en = ResolveLanguageAudioTitle(location, "en", audioTrack)
                    },
                    durationSeconds = audioTrack.DurationSeconds ?? 0,
                    sourceType = audioTrack.SourceType,
                    defaultLanguage = audioTrack.LanguageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi",
                    url = audioUrl
                },
            analytics = new
            {
                visitCount = resolvedInsights.VisitCountAllTime,
                visitCountRecent = resolvedInsights.VisitCountLast7Days,
                audioPlayCount = resolvedInsights.AudioPlayCount,
                audioGuideCount = audioGuideCount,
                rating = resolvedInsights.Rating <= 0 ? 4.8 : resolvedInsights.Rating
            },
            links = new
            {
                landingUrl = links.LandingUrl,
                deepLink = links.DeepLinkUrl,
                downloadPage = links.DownloadPageUrl,
                apkUrl = links.AndroidApkUrl,
                apkQrUrl = links.AndroidApkQrUrl,
                websiteUrl,
                phoneUrl,
                mapUrl
            },
            relatedLocations,
            nextStop = nextStop is null
                ? null
                : new
                {
                    label = string.IsNullOrWhiteSpace(nextStop.Label) ? "🗺 Smart Tourism Route" : nextStop.Label.Trim(),
                    icon = string.IsNullOrWhiteSpace(nextStop.CategoryIcon) ? "📍" : nextStop.CategoryIcon,
                    name = nextStop.Name,
                    distanceLabel = string.IsNullOrWhiteSpace(nextStop.DistanceLabel) ? "Next stop · nearby" : nextStop.DistanceLabel,
                    buttonLabel = string.IsNullOrWhiteSpace(nextStop.ButtonLabel) ? "Continue Tour → Next Stop" : nextStop.ButtonLabel,
                    url = nextStop.Url
                },
            sideColumns = new
            {
                established = establishedYear,
                owner = location.Owner?.FullName ?? location.Owner?.Username ?? "Smart Tourism",
                phone = location.PhoneContact ?? "",
                rank = rankLabel,
                nextStop = nextStop?.Name ?? "Next venue",
                nextDist = nextStop?.DistanceLabel ?? "Nearby",
                icon = string.IsNullOrWhiteSpace(location.Category?.IconEmoji) ? theme.Emoji : location.Category!.IconEmoji,
                funFact,
                tip
            },
            hoursCard = new
            {
                status = hoursCard.Status,
                opens = hoursCard.Opens,
                lastOrder = hoursCard.LastOrder,
                days = hoursCard.Days
            },
            footer = new
            {
                venueCount = resolvedInsights.VenueCount > 0 ? resolvedInsights.VenueCount : 21,
                audioGuideCount = resolvedInsights.AudioGuideCountTotal > 0 ? resolvedInsights.AudioGuideCountTotal : audioGuideCount,
                locationLine = BuildFooterLocationLine(location, openingHours),
                qrId = string.IsNullOrWhiteSpace(resolvedInsights.QrId) ? $"LOC-{location.LocationId:D4}" : resolvedInsights.QrId.Trim(),
                appVersionBadge = "Latest Android APK",
                appVersionText = "Android 8.0+ · Offline audio · Smart Tourism"
            },
            appSubtitle,
            appInstallDescription = !string.IsNullOrWhiteSpace(_options.AndroidStoreUrl)
                ? "Use the direct APK download, or open the Play Store listing if your deployment publishes there."
                : "Use the direct APK download, install Smart Tourism, then return here to keep exploring in the app.",
            behavior = new
            {
                autoOpenDeepLink,
                openDelayMs = Math.Max(0, _options.LandingOpenDelayMs),
                fallbackDelayMs = Math.Max(600, _options.LandingFallbackDelayMs)
            }
        };

        var template = LoadLandingTemplate();
        return template
            .Replace("__SMARTTOUR_PAGE_TITLE__", WebUtility.HtmlEncode(pageTitle), StringComparison.Ordinal)
            .Replace("__SMARTTOUR_BODY_CLASS__", $"theme-{theme.ThemeName}", StringComparison.Ordinal)
            .Replace("__SMARTTOUR_POI_DATA__", JsonSerializer.Serialize(pageData), StringComparison.Ordinal);
    }

    private string LoadLandingTemplate()
    {
        if (!File.Exists(_landingTemplatePath))
        {
            _logger.LogWarning("QR landing template file was not found at {TemplatePath}.", _landingTemplatePath);
            return "<!DOCTYPE html><html><body><p>QR landing template is unavailable.</p></body></html>";
        }

        return File.ReadAllText(_landingTemplatePath);
    }

    private LocationQrThemeDefinition ResolveThemeDefinition(Location location)
    {
        if (!string.IsNullOrWhiteSpace(location.Category?.ThemeName))
        {
            return LocationQrThemeCatalog.Resolve(location.Category.ThemeName);
        }

        return LocationQrThemeCatalog.Resolve(GuessThemeName(location.Category?.Name, location.Name));
    }

    private static string GuessThemeName(string? categoryName, string? locationName)
    {
        var searchText = $"{categoryName} {locationName}".Trim().ToLowerInvariant();

        if (searchText.Contains("snail", StringComparison.Ordinal) || searchText.Contains("ốc", StringComparison.Ordinal))
        {
            return "snail";
        }

        if (searchText.Contains("seafood", StringComparison.Ordinal) || searchText.Contains("hải sản", StringComparison.Ordinal))
        {
            return "seafood";
        }

        if (searchText.Contains("coffee", StringComparison.Ordinal) || searchText.Contains("cà phê", StringComparison.Ordinal))
        {
            return "coffee";
        }

        if (searchText.Contains("smoothie", StringComparison.Ordinal) || searchText.Contains("sinh tố", StringComparison.Ordinal))
        {
            return "smoothie";
        }

        if (searchText.Contains("pho", StringComparison.Ordinal) || searchText.Contains("phở", StringComparison.Ordinal))
        {
            return "pho";
        }

        if (searchText.Contains("noodle", StringComparison.Ordinal) || searchText.Contains("mì", StringComparison.Ordinal))
        {
            return "noodle";
        }

        if (searchText.Contains("bakery", StringComparison.Ordinal) || searchText.Contains("bánh", StringComparison.Ordinal))
        {
            return "bakery";
        }

        if (searchText.Contains("canteen", StringComparison.Ordinal))
        {
            return "canteen";
        }

        if (searchText.Contains("market", StringComparison.Ordinal) || searchText.Contains("food hall", StringComparison.Ordinal))
        {
            return "foodcourt";
        }

        if (searchText.Contains("drink", StringComparison.Ordinal))
        {
            return "coffee";
        }

        return "hotpot";
    }

    private static Audio? ResolvePreferredAudio(Location location) =>
        location.AudioContents
            .Where(item => item.Status == 1)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.AudioId)
            .FirstOrDefault();

    private static string ResolveLanguageAudioTitle(Location location, string languagePrefix, Audio fallbackAudio)
    {
        var matchingAudio = location.AudioContents
            .Where(item => item.Status == 1 && item.LanguageCode.StartsWith(languagePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => ResolveSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.AudioId)
            .FirstOrDefault();

        return matchingAudio?.Title ?? fallbackAudio.Title;
    }

    private List<object> BuildGalleryImages(HttpContext httpContext, Location location, string? heroImageUrl)
    {
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var images = new List<object>();

        if (!string.IsNullOrWhiteSpace(heroImageUrl) && seenUrls.Add(heroImageUrl))
        {
            images.Add(new
            {
                url = heroImageUrl,
                alt = location.Name,
                title = "Hero image"
            });
        }

        foreach (var image in location.Images
                     .OrderBy(item => item.SortOrder)
                     .ThenBy(item => item.ImageId))
        {
            var imageUrl = NormalizePublicAssetUrl(httpContext, image.ImageUrl);
            if (string.IsNullOrWhiteSpace(imageUrl) || !seenUrls.Add(imageUrl))
            {
                continue;
            }

            images.Add(new
            {
                url = imageUrl,
                alt = string.IsNullOrWhiteSpace(image.Description) ? location.Name : image.Description.Trim(),
                title = string.IsNullOrWhiteSpace(image.Description) ? $"Image {images.Count + 1}" : image.Description.Trim()
            });
        }

        return images;
    }

    private string? NormalizePublicAssetUrl(HttpContext httpContext, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Uri.TryCreate(path.Trim(), UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        return BuildAbsoluteUrl(httpContext, path.Trim().TrimStart('/'));
    }

    private static string? SanitizeWebsiteUrl(string? websiteUrl)
    {
        if (string.IsNullOrWhiteSpace(websiteUrl)
            || !Uri.TryCreate(websiteUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;
    }

    private static string? BuildTelephoneUrl(string? phoneContact)
    {
        if (string.IsNullOrWhiteSpace(phoneContact))
        {
            return null;
        }

        var sanitized = new string(phoneContact
            .Trim()
            .Where(character => char.IsDigit(character) || character == '+')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? null : $"tel:{sanitized}";
    }

    private static string BuildMapUrl(Location location)
    {
        if (location.Latitude is >= -90 and <= 90 && location.Longitude is >= -180 and <= 180)
        {
            return $"https://maps.google.com/?q={location.Latitude},{location.Longitude}";
        }

        return string.IsNullOrWhiteSpace(location.Address)
            ? "https://maps.google.com/"
            : $"https://maps.google.com/?q={Uri.EscapeDataString(location.Address)}";
    }

    private static string BuildFooterLocationLine(Location location, string openingHours)
    {
        var address = string.IsNullOrWhiteSpace(location.Address)
            ? "Smart Tourism destination"
            : location.Address.Trim();

        return $"{address} · {openingHours}";
    }

    private static string BuildFallbackFunFact(Location location, string categoryName)
    {
        if (location.EstablishedYear is > 0)
        {
            return $"{location.Name} has welcomed guests since {location.EstablishedYear.Value}, building a lasting local reputation in {categoryName.ToLowerInvariant()}.";
        }

        return $"{location.Name} is one of the Smart Tourism stops helping visitors discover the local story behind {categoryName.ToLowerInvariant()}.";
    }

    private static string BuildFallbackTip(Location location, string openingHours)
    {
        if (!string.IsNullOrWhiteSpace(location.PhoneContact))
        {
            return $"Call ahead if you want to confirm availability before visiting. Current hours: {openingHours}";
        }

        return $"Plan your visit around {openingHours} for the smoothest experience.";
    }

    private static HoursCardInfo BuildHoursCard(string openingHours)
    {
        if (string.IsNullOrWhiteSpace(openingHours))
        {
            return new HoursCardInfo("Open today", "Check venue", "Check on arrival", "📅 Schedule may vary");
        }

        var normalized = openingHours.Trim();
        var separators = new[] { " - ", " – ", " to " };
        foreach (var separator in separators)
        {
            var parts = normalized.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                return new HoursCardInfo("Open today", parts[0], parts[1], "📅 Smart Tourism verified");
            }
        }

        return new HoursCardInfo("Open today", normalized, "Check on arrival", "📅 Smart Tourism verified");
    }

    public string? ResolveConfiguredAndroidInstallUrl()
    {
        if (!string.IsNullOrWhiteSpace(_options.AndroidApkUrl))
        {
            return _options.AndroidApkUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_options.AndroidStoreUrl))
        {
            return _options.AndroidStoreUrl.Trim();
        }

        return null;
    }

    private QrRenderedFile RenderQrFile(
        string payload,
        string fileNameBase,
        string format,
        int size)
    {
        var normalizedFormat = NormalizeFormat(format);
        var normalizedSize = NormalizeSize(size);
        var fileName = $"{fileNameBase}.{normalizedFormat}";

        if (string.Equals(normalizedFormat, QrCodeFormats.Svg, StringComparison.OrdinalIgnoreCase))
        {
            var svgWriter = new ZXing.BarcodeWriterSvg
            {
                Format = BarcodeFormat.QR_CODE,
                Options = CreateEncodingOptions(normalizedSize)
            };

            var svg = svgWriter.Write(payload);
            return new QrRenderedFile(
                fileName,
                "image/svg+xml",
                Encoding.UTF8.GetBytes(svg.Content));
        }

        var writer = new ZXing.BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = CreateEncodingOptions(normalizedSize)
        };

        var pixelData = writer.Write(payload);
        using var bitmap = new SKBitmap(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        Marshal.Copy(pixelData.Pixels, 0, bitmap.GetPixels(), pixelData.Pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        var imageFormat = string.Equals(normalizedFormat, QrCodeFormats.Jpg, StringComparison.OrdinalIgnoreCase)
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;
        var contentType = imageFormat == SKEncodedImageFormat.Jpeg ? "image/jpeg" : "image/png";
        var quality = imageFormat == SKEncodedImageFormat.Jpeg ? 92 : 100;
        using var data = image.Encode(imageFormat, quality);

        return new QrRenderedFile(fileName, contentType, data.ToArray());
    }

    private LocationQrResolvedLinks BuildLocationLinks(
        HttpContext httpContext,
        int locationId,
        string locationName,
        int? defaultAudioId,
        bool autoplay,
        int? requestedAudioTrackId)
    {
        var resolvedAudioTrackId = ResolveAudioTrackId(defaultAudioId, requestedAudioTrackId, autoplay);
        var deepLinkUrl = BuildDeepLinkUrl(locationId, autoplay, resolvedAudioTrackId);
        var landingUrl = BuildAbsoluteUrl(
            httpContext,
            BuildLocationLandingRoute(locationId, autoplay, resolvedAudioTrackId));
        var downloadPageUrl = BuildAbsoluteUrl(
            httpContext,
            BuildDownloadPageRoute(locationId, locationName, deepLinkUrl, autoplay, resolvedAudioTrackId));
        var androidApkUrl = BuildAbsoluteUrl(httpContext, ApiRoutes.PublicAndroidApkDownload);
        var androidApkQrUrl = $"{BuildAbsoluteUrl(httpContext, ApiRoutes.PublicAndroidApkQr)}?size=320&format=png";

        return new LocationQrResolvedLinks(
            landingUrl,
            deepLinkUrl,
            downloadPageUrl,
            androidApkUrl,
            androidApkQrUrl,
            resolvedAudioTrackId);
    }

    private string BuildLocationLandingRoute(int locationId, bool autoplay, int? audioTrackId)
    {
        var route = ApiRoutes.GetPublicLocationQrLanding(locationId);
        var querySegments = new List<string>();

        if (autoplay)
        {
            querySegments.Add("autoplay=true");
        }

        if (audioTrackId is > 0)
        {
            querySegments.Add($"audioTrackId={audioTrackId.Value}");
        }

        return querySegments.Count == 0 ? route : $"{route}?{string.Join("&", querySegments)}";
    }

    private string BuildDownloadPageRoute(
        int locationId,
        string locationName,
        string deepLinkUrl,
        bool autoplay,
        int? audioTrackId)
    {
        var querySegments = new List<string>
        {
            $"locationId={locationId}",
            $"locationName={Uri.EscapeDataString(locationName)}",
            $"openUrl={Uri.EscapeDataString(deepLinkUrl)}"
        };

        if (autoplay)
        {
            querySegments.Add("autoplay=true");
        }

        if (audioTrackId is > 0)
        {
            querySegments.Add($"audioTrackId={audioTrackId.Value}");
        }

        return $"{ApiRoutes.PublicLocationQrDownloadPage}?{string.Join("&", querySegments)}";
    }

    private string BuildDeepLinkUrl(int locationId, bool autoplay, int? audioTrackId)
    {
        var baseUrl = $"{_options.AppDeepLinkScheme}://{_options.AppDeepLinkHost}/location/{locationId}";
        var querySegments = new List<string>();

        if (autoplay)
        {
            querySegments.Add("autoplay=true");
        }

        if (audioTrackId is > 0)
        {
            querySegments.Add($"audioTrackId={audioTrackId.Value}");
        }

        return querySegments.Count == 0
            ? baseUrl
            : $"{baseUrl}?{string.Join("&", querySegments)}";
    }

    private string BuildAbsoluteUrl(HttpContext httpContext, string relativeRoute)
    {
        var baseUri = ResolveBaseUri(httpContext);
        return new Uri(baseUri, relativeRoute.TrimStart('/')).AbsoluteUri;
    }

    private Uri ResolveBaseUri(HttpContext httpContext)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            && Uri.TryCreate(_options.PublicBaseUrl.Trim(), UriKind.Absolute, out var configuredUri))
        {
            if (IsLocalHost(configuredUri)
                && !string.IsNullOrWhiteSpace(httpContext.Request.Host.Host)
                && !IsLocalHost(httpContext.Request.Host.Host))
            {
                _logger.LogWarning(
                    "QrLinks.PublicBaseUrl is localhost ({ConfiguredBaseUrl}) but request host is public ({RequestHost}). Using request host for QR links.",
                    configuredUri.AbsoluteUri,
                    httpContext.Request.Host.Host);

                return BuildRequestBaseUri(httpContext);
            }

            return configuredUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
                ? configuredUri
                : new Uri($"{configuredUri.AbsoluteUri.TrimEnd('/')}/");
        }

        return BuildRequestBaseUri(httpContext);
    }

    private static Uri BuildRequestBaseUri(HttpContext httpContext)
    {
        var pathBase = httpContext.Request.PathBase.HasValue
            ? httpContext.Request.PathBase.Value!.Trim('/').Trim()
            : string.Empty;
        var baseUrl = string.IsNullOrWhiteSpace(pathBase)
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/"
            : $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{pathBase}/";

        return new Uri(baseUrl, UriKind.Absolute);
    }

    private static bool IsLocalHost(Uri uri) =>
        IsLocalHost(uri.Host);

    private static bool IsLocalHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase);

    private LocationQrGenerateRequest NormalizeRequest(LocationQrGenerateRequest? request)
    {
        var normalized = request ?? new LocationQrGenerateRequest();
        normalized.Format = NormalizeFormat(normalized.Format);
        normalized.Size = NormalizeSize(normalized.Size);
        return normalized;
    }

    private LocationQrGenerateRequest NormalizeStoredRequest(Location location) =>
        NormalizeRequest(new LocationQrGenerateRequest
        {
            Format = location.QrFormat,
            Size = location.QrSize,
            Autoplay = location.QrAutoplay,
            AudioTrackId = location.QrAudioTrackId
        });

    private string NormalizeFormat(string? format)
    {
        var normalizedFormat = QrCodeFormats.Normalize(format);
        return QrCodeFormats.All.Contains(normalizedFormat, StringComparer.OrdinalIgnoreCase)
            ? normalizedFormat
            : QrCodeFormats.Normalize(_options.DefaultQrFormat);
    }

    private int NormalizeSize(int? size)
    {
        var requestedSize = size.GetValueOrDefault(_options.DefaultQrSize);
        return Math.Clamp(requestedSize, 128, 2048);
    }

    private static int? ResolveAudioTrackId(int? defaultAudioId, int? requestedAudioTrackId, bool autoplay)
    {
        if (requestedAudioTrackId is > 0)
        {
            return requestedAudioTrackId.Value;
        }

        return autoplay && defaultAudioId is > 0
            ? defaultAudioId.Value
            : null;
    }

    private static int ResolveSourceTypeOrder(string? sourceType) =>
        sourceType?.Trim().ToUpperInvariant() switch
        {
            "RECORDED" => 0,
            "HYBRID" => 1,
            _ => 2
        };

    private static EncodingOptions CreateEncodingOptions(int size) =>
        new()
        {
            Width = size,
            Height = size,
            Margin = 1,
            PureBarcode = true
        };

    private static string BuildFileNameBase(int locationId, string locationName)
    {
        var slug = BuildSlug(locationName);
        return string.IsNullOrWhiteSpace(slug)
            ? $"location-{locationId}-qr"
            : $"location-{locationId}-{slug}-qr";
    }

    private static string BuildSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousDash = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
                continue;
            }

            if (previousDash)
            {
                continue;
            }

            builder.Append('-');
            previousDash = true;
        }

        return builder.ToString().Trim('-');
    }

    private string? SanitizeOpenUrl(string? openUrl)
    {
        if (string.IsNullOrWhiteSpace(openUrl)
            || !Uri.TryCreate(openUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return string.Equals(uri.Scheme, _options.AppDeepLinkScheme, StringComparison.OrdinalIgnoreCase)
            ? uri.AbsoluteUri
            : null;
    }

    private string? ResolveLocationHeroImageUrl(HttpContext httpContext, Location location, string? overrideImageUrl)
    {
        var imageUrl = overrideImageUrl;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            imageUrl = location.PreferenceImageUrl
                ?? location.Images.OrderBy(item => item.SortOrder).Select(item => item.ImageUrl).FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsoluteUri;
        }

        var normalized = imageUrl.Trim().TrimStart('/');
        return BuildAbsoluteUrl(httpContext, normalized);
    }

    public sealed record QrRenderedFile(
        string FileName,
        string ContentType,
        byte[] Content);

    public sealed record LocationLandingInsights
    {
        public string? CategoryName { get; init; }

        public string? OpeningHours { get; init; }

        public string? ImageUrl { get; init; }

        public string? Badge { get; init; }

        public string? FunFact { get; init; }

        public string? Tip { get; init; }

        public string? RankLabel { get; init; }

        public string? ProjectName { get; init; }

        public string? QrId { get; init; }

        public int VisitCountAllTime { get; init; }

        public int VisitCountLast7Days { get; init; }

        public int AudioPlayCount { get; init; }

        public int VenueCount { get; init; }

        public int AudioGuideCountTotal { get; init; }

        public double Rating { get; init; } = 4.8;

        public IReadOnlyList<LocationLandingRelatedLocationInfo> RelatedLocations { get; init; } = [];

        public LocationLandingNextStopInfo? NextStop { get; init; }

        public DateTime? LastUpdatedUtc { get; init; }
    }

    public sealed record LocationLandingRelatedLocationInfo
    {
        public int LocationId { get; init; }

        public string Name { get; init; } = "";

        public string CategoryName { get; init; } = "";

        public string? CategoryIcon { get; init; }

        public int AudioCount { get; init; }

        public string DistanceLabel { get; init; } = "";

        public string Url { get; init; } = "";
    }

    public sealed record LocationLandingNextStopInfo
    {
        public string Name { get; init; } = "";

        public string? CategoryIcon { get; init; }

        public string? DistanceLabel { get; init; }

        public string? Label { get; init; }

        public string? ButtonLabel { get; init; }

        public string? Url { get; init; }
    }

    private sealed record LocationQrResolvedLinks(
        string LandingUrl,
        string DeepLinkUrl,
        string DownloadPageUrl,
        string AndroidApkUrl,
        string AndroidApkQrUrl,
        int? AudioTrackId);

    private sealed record HoursCardInfo(
        string Status,
        string Opens,
        string LastOrder,
        string Days);
}
