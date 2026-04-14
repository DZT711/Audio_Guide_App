using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public static class LanguageBadgeService
{
    public static string BuildSummary(IEnumerable<PublicAudioTrackDto>? tracks)
    {
        if (tracks is null)
        {
            return string.Empty;
        }

        var badges = tracks
            .Select(track => NormalizeCode(track.Language))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Select(FormatBadge)
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
}
