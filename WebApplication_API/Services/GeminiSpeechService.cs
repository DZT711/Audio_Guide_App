using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Project_SharedClassLibrary.Contracts;

namespace WebApplication_API.Services;

public sealed class GeminiSpeechService(
    HttpClient httpClient,
    IOptions<GeminiSpeechOptions> optionsAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly GeminiSpeechOptions _options = optionsAccessor.Value;

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<GeminiSpeechResult> TranslateAndGenerateSpeechAsync(
        PublicAudioTranslateTtsRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Cloud translation TTS is not configured.");
        }

        var normalizedText = NormalizeText(request.Text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new InvalidOperationException("Text is required.");
        }

        var sourceLanguage = NormalizeLanguage(request.SourceLanguage);
        var targetLanguage = NormalizeLanguage(request.TargetLanguage);
        if (string.IsNullOrWhiteSpace(sourceLanguage) || string.IsNullOrWhiteSpace(targetLanguage))
        {
            throw new InvalidOperationException("Source and target languages are required.");
        }

        var translatedText = LanguagesMatch(sourceLanguage, targetLanguage)
            ? normalizedText
            : await TranslateAsync(normalizedText, sourceLanguage, targetLanguage, cancellationToken);

        var waveBytes = await GenerateSpeechAsync(translatedText, targetLanguage, request.VoiceGender, cancellationToken);
        return new GeminiSpeechResult(
            translatedText,
            targetLanguage,
            waveBytes,
            "audio/wav",
            ResolveVoiceName(request.VoiceGender));
    }

    private async Task<string> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] =
                                $"Translate the following tourism narration from {DescribeLanguage(sourceLanguage)} to {DescribeLanguage(targetLanguage)}. Return only the translated text with no commentary. Preserve place names and proper nouns unless they have a standard translated form.\n\nText:\n{text}"
                        }
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2
            }
        };

        using var response = await SendGenerateContentAsync(_options.TranslationModel, payload, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var translatedText = ExtractTextResponse(responseBody);
        if (string.IsNullOrWhiteSpace(translatedText))
        {
            throw new InvalidOperationException("Gemini translation returned an empty response.");
        }

        return translatedText.Trim();
    }

    private async Task<byte[]> GenerateSpeechAsync(
        string translatedText,
        string targetLanguage,
        string? voiceGender,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["parts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["text"] = translatedText
                        }
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["responseModalities"] = new JsonArray("AUDIO"),
                ["speechConfig"] = new JsonObject
                {
                    ["voiceConfig"] = new JsonObject
                    {
                        ["prebuiltVoiceConfig"] = new JsonObject
                        {
                            ["voiceName"] = ResolveVoiceName(voiceGender)
                        }
                    }
                }
            }
        };

        using var response = await SendGenerateContentAsync(_options.TtsModel, payload, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var pcmBytes = ExtractAudioResponse(responseBody);
        if (pcmBytes.Length == 0)
        {
            throw new InvalidOperationException("Gemini TTS returned no audio.");
        }

        return WrapPcmAsWave(pcmBytes, sampleRate: 24000, channels: 1, bitsPerSample: 16);
    }

    private async Task<HttpResponseMessage> SendGenerateContentAsync(
        string model,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add("x-goog-api-key", _options.ApiKey);
        request.Content = new StringContent(payload.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static string ExtractTextResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates))
        {
            return "";
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts))
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "";
                }
            }
        }

        return "";
    }

    private static byte[] ExtractAudioResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates))
        {
            return [];
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts))
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (!part.TryGetProperty("inlineData", out var inlineData)
                    || !inlineData.TryGetProperty("data", out var dataElement))
                {
                    continue;
                }

                var base64Data = dataElement.GetString();
                if (string.IsNullOrWhiteSpace(base64Data))
                {
                    continue;
                }

                return Convert.FromBase64String(base64Data);
            }
        }

        return [];
    }

    private static byte[] WrapPcmAsWave(byte[] pcmBytes, int sampleRate, short channels, short bitsPerSample)
    {
        using var stream = new MemoryStream(capacity: pcmBytes.Length + 44);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        var blockAlign = (short)(channels * bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);
        writer.Flush();

        return stream.ToArray();
    }

    private static string NormalizeText(string text) =>
        string.IsNullOrWhiteSpace(text) ? "" : text.Trim();

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
            "cn" => "zh",
            "jp" => "ja",
            "kr" => "ko",
            _ => parts[0].ToLowerInvariant()
        };

        if (parts.Length == 1)
        {
            return prefix switch
            {
                "vi" => "vi-VN",
                "en" => "en-US",
                "zh" => "zh-CN",
                "ja" => "ja-JP",
                "ko" => "ko-KR",
                "fr" => "fr-FR",
                _ => prefix
            };
        }

        return $"{prefix}-{parts[1].ToUpperInvariant()}";
    }

    private static bool LanguagesMatch(string left, string right) =>
        string.Equals(NormalizeLanguage(left), NormalizeLanguage(right), StringComparison.OrdinalIgnoreCase);

    private static string DescribeLanguage(string language) =>
        NormalizeLanguage(language) switch
        {
            "vi-VN" => "Vietnamese (vi-VN)",
            "en-US" => "English (en-US)",
            "zh-CN" => "Chinese Simplified (zh-CN)",
            "ja-JP" => "Japanese (ja-JP)",
            "ko-KR" => "Korean (ko-KR)",
            "fr-FR" => "French (fr-FR)",
            var normalized when !string.IsNullOrWhiteSpace(normalized) => normalized,
            _ => "the requested language"
        };

    private static string ResolveVoiceName(string? voiceGender) =>
        string.Equals(voiceGender, "Male", StringComparison.OrdinalIgnoreCase)
            ? "Kore"
            : "Puck";
}

public sealed record GeminiSpeechResult(
    string TranslatedText,
    string TargetLanguage,
    byte[] AudioContent,
    string ContentType,
    string VoiceName);
