using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Project_SharedClassLibrary.Contracts;

namespace WebApplication_API.Services;

public sealed class TtsPreviewService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<TtsPreviewService> logger)
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

        try
        {
            var azureResult = await TryAzureAsync(previewText, normalizedLanguage, request.VoiceGender, cancellationToken);
            if (azureResult is not null)
            {
                return azureResult;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Azure TTS preview was unavailable. Falling back to the free provider.");
        }

        var freeProviderResult = await TryFreeTranslateAsync(previewText, normalizedLanguage, cancellationToken);
        if (freeProviderResult is not null)
        {
            return freeProviderResult;
        }

        throw new InvalidOperationException("No TTS preview provider is available right now.");
    }

    private async Task<TtsPreviewResult?> TryAzureAsync(
        string text,
        string language,
        string? voiceGender,
        CancellationToken cancellationToken)
    {
        var azureKey = configuration["Tts:Azure:Key"] ?? configuration["AZURE_SPEECH_KEY"];
        var azureRegion = configuration["Tts:Azure:Region"] ?? configuration["AZURE_SPEECH_REGION"];
        if (string.IsNullOrWhiteSpace(azureKey) || string.IsNullOrWhiteSpace(azureRegion))
        {
            return null;
        }

        var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{azureRegion}.api.cognitive.microsoft.com/sts/v1.0/issueToken");
        tokenRequest.Headers.Add("Ocp-Apim-Subscription-Key", azureKey);

        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Azure Speech token acquisition failed.");
        }

        var accessToken = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Azure Speech returned an empty token.");
        }

        var voiceName = ResolveAzureVoice(language, voiceGender);
        var ssml = BuildSsml(text, language, voiceName);

        using var ttsRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{azureRegion}.tts.speech.microsoft.com/cognitiveservices/v1");
        ttsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        ttsRequest.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-32kbitrate-mono-mp3");
        ttsRequest.Headers.Add("User-Agent", "SmartTourismAdmin");
        ttsRequest.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

        using var ttsResponse = await httpClient.SendAsync(ttsRequest, cancellationToken);
        if (!ttsResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Azure Speech synthesis failed.");
        }

        var audioBytes = await ttsResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        if (audioBytes.Length == 0)
        {
            throw new InvalidOperationException("Azure Speech returned an empty preview.");
        }

        return new TtsPreviewResult(
            audioBytes,
            ttsResponse.Content.Headers.ContentType?.ToString() ?? "audio/mpeg",
            "AzureSpeech",
            voiceName);
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

    private static string NormalizeLanguage(string language) =>
        string.IsNullOrWhiteSpace(language) ? "" : language.Trim();

    private static string ResolveFreeProviderLanguage(string language)
    {
        var normalized = NormalizeLanguage(language).ToLowerInvariant();
        if (normalized.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
        {
            return "vi";
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        var separatorIndex = normalized.IndexOf('-');
        return separatorIndex > 0 ? normalized[..separatorIndex] : normalized;
    }

    private static string ResolveAzureVoice(string language, string? voiceGender)
    {
        var normalizedLanguage = NormalizeLanguage(language).ToLowerInvariant();
        var isMale = string.Equals(voiceGender, "Male", StringComparison.OrdinalIgnoreCase);

        return normalizedLanguage switch
        {
            var value when value.StartsWith("vi", StringComparison.OrdinalIgnoreCase) =>
                isMale ? "vi-VN-NamMinhNeural" : "vi-VN-HoaiMyNeural",
            var value when value.StartsWith("en", StringComparison.OrdinalIgnoreCase) =>
                isMale ? "en-US-GuyNeural" : "en-US-AriaNeural",
            _ => isMale ? "en-US-GuyNeural" : "en-US-AriaNeural"
        };
    }

    private static string BuildSsml(string text, string language, string voiceName)
    {
        var document = new XDocument(
            new XElement("speak",
                new XAttribute("version", "1.0"),
                new XAttribute(XNamespace.Xml + "lang", language),
                new XElement("voice",
                    new XAttribute("name", voiceName),
                    text)));

        return document.ToString(SaveOptions.DisableFormatting);
    }
}

public sealed record TtsPreviewResult(
    byte[] Content,
    string ContentType,
    string Provider,
    string VoiceName);
