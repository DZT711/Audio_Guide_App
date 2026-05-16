using System.ComponentModel.DataAnnotations;

namespace Project_SharedClassLibrary.Contracts;

public static class QrCodeFormats
{
    public const string Png = "png";
    public const string Jpg = "jpg";
    public const string Svg = "svg";

    public static readonly IReadOnlyList<string> All = [Png, Jpg, Svg];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Png;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "jpeg" => Jpg,
            Jpg => Jpg,
            Svg => Svg,
            _ => Png
        };
    }
}

public sealed class LocationQrGenerateRequest
{
    [Range(128, 2048)]
    public int Size { get; set; } = 512;

    [Required]
    [RegularExpression("^(png|jpg|jpeg|svg)$", ErrorMessage = "Format must be png, jpg, jpeg, or svg.")]
    public string Format { get; set; } = QrCodeFormats.Png;

    public bool Autoplay { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int? AudioTrackId { get; set; }
}

public sealed class LocationQrBulkRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Choose at least one location.")]
    public List<int> LocationIds { get; set; } = [];

    [Range(128, 2048)]
    public int Size { get; set; } = 512;

    [Required]
    [RegularExpression("^(png|jpg|jpeg|svg)$", ErrorMessage = "Format must be png, jpg, jpeg, or svg.")]
    public string Format { get; set; } = QrCodeFormats.Png;

    public bool Autoplay { get; set; } = true;
}

public sealed class LocationQrStatusDto
{
    public int LocationId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public int Status { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public bool FeatureEnabled { get; init; }
    public bool HasDefaultAudio { get; init; }
    public int? DefaultAudioId { get; init; }
    public string? DefaultAudioTitle { get; init; }
    public string LandingUrl { get; init; } = string.Empty;
    public string DeepLinkUrl { get; init; } = string.Empty;
    public string DownloadPageUrl { get; init; } = string.Empty;
    public string AndroidApkUrl { get; init; } = string.Empty;
    public string AndroidApkQrUrl { get; init; } = string.Empty;
    public string SuggestedFileNameBase { get; init; } = string.Empty;
    public int DefaultSize { get; init; } = 512;
    public string DefaultFormat { get; init; } = QrCodeFormats.Png;
    public bool DefaultAutoplay { get; init; } = true;
    public int? DefaultAudioTrackId { get; init; }
}

public sealed class QrOverviewDto
{
    public QrAnalyticsOverviewDto QrAnalytics { get; init; } = new();

    public QrDeviceCheckOverviewDto DeviceCheck { get; init; } = new();

    public IReadOnlyList<QrDeviceCheckLogDto> RecentLogs { get; init; } = [];
}

public sealed class QrAnalyticsOverviewDto
{
    public int VisitCountAllTime { get; init; }

    public int VisitCountLast7Days { get; init; }

    public int AudioGuideCount { get; init; }

    public double Rating { get; init; } = 4.8;
}

public sealed class QrDeviceCheckOverviewDto
{
    public int TotalScans { get; init; }

    public int StrongCount { get; init; }

    public int WeakCount { get; init; }

    public double WeakRatePercent { get; init; }
}

public sealed class QrDeviceCheckLogDto
{
    public DateTime Time { get; init; }

    public string DeviceName { get; init; } = string.Empty;

    public string Platform { get; init; } = string.Empty;

    public string OsVersion { get; init; } = string.Empty;

    public string QrCode { get; init; } = string.Empty;

    public int WeakScore { get; init; }
}
