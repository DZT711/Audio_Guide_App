namespace WebApplication_API.Services;

public sealed class GeminiSpeechOptions
{
    public const string SectionName = "GeminiSpeech";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = "";

    public string TranslationModel { get; set; } = "gemini-2.5-flash";

    public string TtsModel { get; set; } = "gemini-2.5-flash-preview-tts";

    public int TimeoutSeconds { get; set; } = 40;
}
