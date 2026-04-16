using MauiApp_Mobile.Models;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public static class LanguageBadgeService
{
    public static IReadOnlyList<LanguageBadgeChip> BuildItems(IEnumerable<PublicAudioTrackDto>? tracks, string? summaryText = null)
    {
        var normalizedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (tracks is not null)
        {
            foreach (var code in tracks
                         .Select(track => NormalizeCode(track.Language))
                         .Where(code => !string.IsNullOrWhiteSpace(code)))
            {
                normalizedCodes.Add(code);
            }
        }

        if (normalizedCodes.Count == 0 && !string.IsNullOrWhiteSpace(summaryText))
        {
            foreach (var code in ParseCodesFromSummary(summaryText))
            {
                normalizedCodes.Add(code);
            }
        }

        return normalizedCodes
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Select(CreateChip)
            .ToList();
    }

    public static string BuildSummary(IEnumerable<PublicAudioTrackDto>? tracks)
    {
        var badges = BuildItems(tracks)
            .Select(chip => chip.Label)
            .ToList();

        return badges.Count == 0 ? string.Empty : string.Join("  ", badges);
    }

    public static string BuildSingleBadge(string? languageCode)
    {
        var normalized = NormalizeCode(languageCode);
        return string.IsNullOrWhiteSpace(normalized) ? "🌐 --" : FormatBadge(normalized);
    }

    public static string NormalizeCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return string.Empty;
        }

        return languageCode.Trim().ToLowerInvariant() switch
        {
            var value when value.StartsWith("vi") => "vn",
            var value when value.StartsWith("en") => "en",
            var value when value.StartsWith("fr") => "fr",
            var value when value.StartsWith("ja") || value.StartsWith("jp") => "jp",
            var value when value.StartsWith("ko") || value.StartsWith("kr") => "kr",
            var value when value.StartsWith("zh") || value.StartsWith("cn") => "cn",
            var value => value.Length > 2 ? value[..2].ToLowerInvariant() : value
        };
    }

    private static string FormatBadge(string normalizedCode) => normalizedCode switch
    {
        "vn" => "🇻🇳 VN",
        "en" => "🇬🇧 EN",
        "fr" => "🇫🇷 FR",
        "jp" => "🇯🇵 JP",
        "kr" => "🇰🇷 KR",
        "cn" => "🇨🇳 CN",
        _ => $"🌐 {normalizedCode.ToUpperInvariant()}"
    };

    private static IEnumerable<string> ParseCodesFromSummary(string summaryText)
    {
        var tokens = summaryText
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().ToLowerInvariant());

        foreach (var token in tokens)
        {
            var normalized = token switch
            {
                "vn" => "vn",
                "en" => "en",
                "fr" => "fr",
                "jp" => "jp",
                "ja" => "jp",
                "kr" => "kr",
                "ko" => "kr",
                "cn" => "cn",
                "zh" => "cn",
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static LanguageBadgeChip CreateChip(string normalizedCode)
    {
        var (background, foreground) = normalizedCode switch
        {
            "vn" => (Color.FromArgb("#FFF1F1"), Color.FromArgb("#C62828")),
            "en" => (Color.FromArgb("#EEF4FF"), Color.FromArgb("#2563EB")),
            "fr" => (Color.FromArgb("#ECFDF3"), Color.FromArgb("#15803D")),
            "jp" => (Color.FromArgb("#FFF7ED"), Color.FromArgb("#C2410C")),
            "kr" => (Color.FromArgb("#F5F3FF"), Color.FromArgb("#7C3AED")),
            "cn" => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#B45309")),
            _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"))
        };

        return new LanguageBadgeChip
        {
            Label = FormatBadge(normalizedCode),
            BackgroundColor = background,
            TextColor = foreground
        };
    }
}
