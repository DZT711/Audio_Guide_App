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
    ILogger<LocationQrService> logger)
{
    private readonly QrLinkOptions _options = options.Value;
    private readonly ILogger<LocationQrService> _logger = logger;

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

        var locationName = WebUtility.HtmlEncode(location.Name);
        var deepLinkUrl = WebUtility.HtmlEncode(links.DeepLinkUrl);
        var downloadPageUrl = WebUtility.HtmlEncode(links.DownloadPageUrl);
        var deepLinkJson = JsonSerializer.Serialize(links.DeepLinkUrl);
        var downloadPageJson = JsonSerializer.Serialize(links.DownloadPageUrl);
        var fallbackDelay = Math.Max(600, _options.LandingFallbackDelayMs);
        var openDelay = Math.Max(0, _options.LandingOpenDelayMs);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Open {{locationName}} | Smart Tourism</title>
    <style>
        :root {
            color-scheme: light;
            font-family: "Segoe UI", system-ui, sans-serif;
            --bg: #f5f7fb;
            --panel: rgba(255,255,255,0.94);
            --text: #0f172a;
            --muted: #475569;
            --accent: #0f766e;
            --accent-soft: #ccfbf1;
            --border: rgba(148, 163, 184, 0.26);
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            background:
                radial-gradient(circle at top right, rgba(20, 184, 166, 0.16), transparent 32%),
                linear-gradient(160deg, #f8fafc 0%, #ecfeff 100%);
            color: var(--text);
            padding: 1.5rem;
        }

        main {
            width: min(100%, 720px);
            padding: 2rem;
            border: 1px solid var(--border);
            border-radius: 28px;
            background: var(--panel);
            box-shadow: 0 24px 60px rgba(15, 23, 42, 0.12);
        }

        .eyebrow {
            display: inline-flex;
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
            margin: 1rem 0 0.6rem;
            font-size: clamp(1.9rem, 4vw, 3rem);
            line-height: 1.1;
        }

        p {
            margin: 0;
            color: var(--muted);
            font-size: 1rem;
            line-height: 1.7;
        }

        .status {
            margin-top: 1.4rem;
            padding: 1rem 1.15rem;
            border-radius: 18px;
            background: rgba(15, 118, 110, 0.08);
            color: var(--accent);
            font-weight: 600;
        }

        .actions {
            display: flex;
            flex-wrap: wrap;
            gap: 0.85rem;
            margin-top: 1.5rem;
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
            background: var(--accent);
            color: white;
        }

        .btn-secondary {
            border-color: var(--border);
            color: var(--text);
            background: white;
        }
    </style>
</head>
<body>
    <main>
        <span class="eyebrow">Smart Tourism</span>
        <h1>Opening {{locationName}}</h1>
        <p>We are trying to open the Smart Tourism app for this location. If the app is not installed yet, we will take you to the Android download page.</p>

        <div class="status" id="status">Trying to open the app...</div>

        <div class="actions">
            <a class="btn btn-primary" href="{{deepLinkUrl}}">Open App Now</a>
            <a class="btn btn-secondary" href="{{downloadPageUrl}}">Download App</a>
        </div>
    </main>

    <script>
        (() => {
            const deepLinkUrl = {{deepLinkJson}};
            const downloadPageUrl = {{downloadPageJson}};
            const fallbackDelayMs = {{fallbackDelay}};
            const openDelayMs = {{openDelay}};
            const statusElement = document.getElementById("status");

            const updateStatus = (message) => {
                if (statusElement) {
                    statusElement.textContent = message;
                }
            };

            const tryOpen = () => {
                let handled = false;

                const onVisibilityChange = () => {
                    if (document.hidden) {
                        handled = true;
                        updateStatus("App opened successfully.");
                    }
                };

                document.addEventListener("visibilitychange", onVisibilityChange, { once: true });
                updateStatus("Opening Smart Tourism...");
                window.location.href = deepLinkUrl;

                window.setTimeout(() => {
                    if (handled || document.hidden) {
                        return;
                    }

                    updateStatus("App not detected. Redirecting to the Android download page...");
                    window.location.replace(downloadPageUrl);
                }, fallbackDelayMs);
            };

            window.setTimeout(tryOpen, openDelayMs);
        })();
    </script>
</body>
</html>
""";
    }

    public string RenderDownloadPage(
        HttpContext httpContext,
        string? openUrl = null,
        string? locationName = null)
    {
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

    public sealed record QrRenderedFile(
        string FileName,
        string ContentType,
        byte[] Content);

    private sealed record LocationQrResolvedLinks(
        string LandingUrl,
        string DeepLinkUrl,
        string DownloadPageUrl,
        string AndroidApkUrl,
        string AndroidApkQrUrl,
        int? AudioTrackId);
}
