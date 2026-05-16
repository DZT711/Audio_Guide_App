using Project_SharedClassLibrary.Contracts;

namespace WebApplication_API.Services;

public sealed class TtsPreviewService(
    HttpClient httpClient)
{
    private const int MaxPreviewCharacters = 220;

    public async Task<TtsPreviewResult> GeneratePreviewAsync(AudioTtsPreviewRequest request, CancellationToken cancellationToken)
    {
        var previewText = NormalizePreviewText(request.Text);
        if (string.IsNullOrWhiteSpace(previewText))
        {
            throw new InvalidOperationException("Preview text is required.");
        }

        var normalizedLanguage = NormalizeLanguage(request.Language);
        if (string.IsNullOrWhiteSpace(normalizedLanguage))
        {
            throw new InvalidOperationException("A valid preview language is required.");
        }

        var freeProviderResult = await TryFreeTranslateAsync(previewText, normalizedLanguage, cancellationToken);
        if (freeProviderResult is not null)
        {
            return freeProviderResult;
        }

        throw new InvalidOperationException("No TTS preview provider is available right now.");
    }

    private async Task<TtsPreviewResult?> TryFreeTranslateAsync(
        string text,
        string language,
        CancellationToken cancellationToken)
    {
        var providerLanguage = ResolveFreeProviderLanguage(language);
        var url =
            $"https://translate.googleapis.com/translate_tts?ie=UTF-8&client=gtx&tl={Uri.EscapeDataString(providerLanguage)}&q={Uri.EscapeDataString(text)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 SmartTourismAdmin");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (audioBytes.Length == 0)
        {
            return null;
        }

        return new TtsPreviewResult(
            audioBytes,
            response.Content.Headers.ContentType?.ToString() ?? "audio/mpeg",
            "FreeTranslate",
            providerLanguage);
    }

    private static string NormalizePreviewText(string text)
    {
        var normalized = text.Trim();
        if (normalized.Length <= MaxPreviewCharacters)
        {
            return normalized;
        }

        var candidate = normalized[..MaxPreviewCharacters];
        var lastSpace = candidate.LastIndexOf(' ');
        return lastSpace > 60 ? candidate[..lastSpace] : candidate;
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "";
        }

        var parts = language.Trim()
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "";
        }

        var prefix = parts[0].ToLowerInvariant() switch
        {
            "vn" => "vi",
            _ => parts[0].ToLowerInvariant()
        };

        if (parts.Length == 1)
        {
            return prefix switch
            {
                "vi" => "vi-VN",
                "en" => "en-US",
                _ => prefix
            };
        }

        var region = parts[1].ToUpperInvariant();
        if (prefix == "vi" && region == "VI")
        {
            region = "VN";
        }

        var normalized = $"{prefix}-{region}";
        return parts.Length == 2
            ? normalized
            : $"{normalized}-{string.Join("-", parts[2..])}";
    }

    private static string GetLanguagePrefix(string language)
    {
        var normalized = NormalizeLanguage(language).ToLowerInvariant();
        var separatorIndex = normalized.IndexOf('-');
        return separatorIndex > 0 ? normalized[..separatorIndex] : normalized;
    }

    private static string ResolveFreeProviderLanguage(string language)
    {
        var prefix = GetLanguagePrefix(language);
        if (prefix == "vi")
        {
            return "vi";
        }

        if (prefix == "en")
        {
            return "en";
        }

        return prefix;
    }
}

public sealed record TtsPreviewResult(
    byte[] Content,
    string ContentType,
    string Provider,
    string VoiceName);
