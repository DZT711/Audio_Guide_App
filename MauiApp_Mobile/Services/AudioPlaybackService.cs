using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Constants;
using System.Net.Http.Json;
using MauiTextToSpeech = Microsoft.Maui.Media.TextToSpeech;

#if ANDROID
using Android.Media;
using Android.OS;
using Android.Speech.Tts;
using Java.Util;
using AndroidTts = Android.Speech.Tts.TextToSpeech;
using AndroidLocale = Java.Util.Locale;
#endif

#if WINDOWS
using Windows.Media.Core;
using Windows.Media.Playback;
using WindowsMediaPlayer = Windows.Media.Playback.MediaPlayer;
#endif

namespace MauiApp_Mobile.Services;

public sealed class AudioPlaybackService
{
    public static AudioPlaybackService Instance { get; } = new();
    private const bool UseCloudTts = false;
#if ANDROID
    private const string GoogleTtsEnginePackage = "com.google.android.tts";
    private const string SamsungTtsEnginePackage = "com.samsung.SMT";
#endif
    private static readonly HttpClient SpeechHttpClient = new()
    {
        BaseAddress = new Uri(MobileApiOptions.BaseUrl),
        Timeout = TimeSpan.FromSeconds(45)
    };

    private CancellationTokenSource? _ttsCancellationTokenSource;

#if ANDROID
    private MediaPlayer? _androidPlayer;
    private AndroidTts? _androidTts;
#endif

#if WINDOWS
    private WindowsMediaPlayer? _windowsPlayer;
#endif

    private AudioPlaybackService()
    {
    }

    public event EventHandler<PublicAudioTrackDto?>? PlaybackStateChanged;

    public PublicAudioTrackDto? CurrentTrack { get; private set; }

    public bool IsPlaying { get; private set; }

    public async Task PlayAsync(PublicAudioTrackDto track, CancellationToken cancellationToken = default)
    {
        await StopAsync();

        CurrentTrack = track;
        IsPlaying = true;
        RaisePlaybackStateChanged();

        try
        {
            if (UseCloudTts && ShouldUseTranslatedCloudTts(track))
            {
                try
                {
                    await PlayTranslatedCloudTtsAsync(track, cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    // Fall back to the device voice or stored audio if cloud TTS is unavailable.
                }
            }

            if (ShouldUseTts(track))
            {
                await PlayTtsAsync(track, cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(track.AudioURL))
            {
                await PlatformPlayAudioAsync(ResolveAudioUrl(track.AudioURL), cancellationToken);
                return;
            }

            throw new InvalidOperationException("This audio track has no playable source.");
        }
        catch
        {
            CurrentTrack = null;
            IsPlaying = false;
            RaisePlaybackStateChanged();
            throw;
        }
    }

    public async Task TestCurrentVoiceAsync(CancellationToken cancellationToken = default)
    {
        var preferredLanguage = GetPreferredPlaybackLanguageCode();
        var sampleTrack = new PublicAudioTrackDto
        {
            Id = -1,
            Title = "Voice test",
            SourceType = "TTS",
            Language = preferredLanguage,
            Script = GetVoiceTestScript(preferredLanguage),
            VoiceGender = "Female"
        };

        await PlayAsync(sampleTrack, cancellationToken);
    }

    public async Task StopAsync()
    {
        _ttsCancellationTokenSource?.Cancel();
        _ttsCancellationTokenSource?.Dispose();
        _ttsCancellationTokenSource = null;

        await PlatformStopAudioAsync();

        CurrentTrack = null;
        IsPlaying = false;
        RaisePlaybackStateChanged();
    }

    private async Task PlayTtsAsync(PublicAudioTrackDto track, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(track.Script))
        {
            throw new InvalidOperationException("The selected TTS track has no script.");
        }

#if ANDROID
        await PlatformSpeakTtsAsync(track.Script, track.Language, cancellationToken);
#else
        _ttsCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var options = new SpeechOptions();
        var locale = await ResolveLocaleAsync(track.Language);
        if (locale is not null)
        {
            options.Locale = locale;
        }

        await MauiTextToSpeech.Default.SpeakAsync(track.Script, options, _ttsCancellationTokenSource.Token);
#endif
        await OnPlaybackCompletedAsync();
    }

    private async Task PlayTranslatedCloudTtsAsync(PublicAudioTrackDto track, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(track.Script))
        {
            throw new InvalidOperationException("The selected TTS track has no script.");
        }

        var request = new PublicAudioTranslateTtsRequest
        {
            Text = track.Script,
            SourceLanguage = NormalizeLanguageCode(track.Language),
            TargetLanguage = GetPreferredPlaybackLanguageCode(),
            VoiceGender = string.IsNullOrWhiteSpace(track.VoiceGender) ? "Female" : track.VoiceGender
        };

        using var response = await SpeechHttpClient.PostAsJsonAsync(ApiRoutes.PublicTranslateTts, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Cloud translation TTS is unavailable."
                : message);
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (audioBytes.Length == 0)
        {
            throw new InvalidOperationException("Cloud translation TTS returned an empty audio file.");
        }

        var tempFilePath = Path.Combine(FileSystem.Current.CacheDirectory, $"smarttour-tts-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(tempFilePath, audioBytes, cancellationToken);

        try
        {
            await PlatformPlayAudioAsync(tempFilePath, cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch
            {
            }
        }
    }

    private static bool ShouldUseTts(PublicAudioTrackDto track) =>
        string.Equals(track.SourceType, "TTS", StringComparison.OrdinalIgnoreCase)
        || (string.IsNullOrWhiteSpace(track.AudioURL) && !string.IsNullOrWhiteSpace(track.Script));

    private static bool ShouldUseTranslatedCloudTts(PublicAudioTrackDto track)
    {
        if (string.IsNullOrWhiteSpace(track.Script))
        {
            return false;
        }

        var targetLanguage = GetPreferredPlaybackLanguageCode();
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            return false;
        }

        if (!LanguagePrefixesMatch(track.Language, targetLanguage))
        {
            return true;
        }

        return string.Equals(GetLanguagePrefix(targetLanguage), "vi", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Microsoft.Maui.Media.Locale?> ResolveLocaleAsync(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var locales = await MauiTextToSpeech.Default.GetLocalesAsync();
        return locales.FirstOrDefault(item =>
                   string.Equals(item.Language, languageCode, StringComparison.OrdinalIgnoreCase))
               ?? locales.FirstOrDefault(item =>
                   item.Language.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase))
               ?? locales.FirstOrDefault(item =>
                   languageCode.StartsWith(item.Language, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveAudioUrl(string audioUrl)
    {
        if (Uri.TryCreate(audioUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return new Uri(new Uri(MobileApiOptions.BaseUrl), audioUrl.TrimStart('/')).ToString();
    }

    private static string GetPreferredPlaybackLanguageCode() =>
        LocalizationService.Instance.Language switch
        {
            "vi" => "vi-VN",
            "en" => "en-US",
            "cn" => "zh-CN",
            "jp" => "ja-JP",
            "kr" => "ko-KR",
            "fr" => "fr-FR",
            _ => "vi-VN"
        };

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "";
        }

        var trimmed = languageCode.Trim().Replace('_', '-');
        var parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    private static bool LanguagePrefixesMatch(string? left, string? right)
    {
        var normalizedLeft = NormalizeLanguageCode(left);
        var normalizedRight = NormalizeLanguageCode(right);
        if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
        {
            return false;
        }

        var leftPrefix = normalizedLeft.Split('-')[0];
        var rightPrefix = normalizedRight.Split('-')[0];
        return string.Equals(leftPrefix, rightPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLanguagePrefix(string? languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "";
        }

        return normalized.Split('-')[0];
    }

    private static string GetVoiceTestScript(string preferredLanguage) =>
        NormalizeLanguageCode(preferredLanguage) switch
        {
            "vi-VN" => "Xin chao, day la ban thu giong doc tieng Viet.",
            "en-US" => "Hello, this is your English voice test.",
            "fr-FR" => "Bonjour, ceci est votre test vocal en francais.",
            "ja-JP" => "Konnichiwa, kore wa nihongo no boisu tesuto desu.",
            "ko-KR" => "Annyeonghaseyo, ileoseo hangugeo moksori teseuteu-imnida.",
            "zh-CN" => "Ni hao, zhe shi zhongwen yuyin ceshi.",
            _ => "Xin chao, day la ban thu giong doc."
        };

    private async Task PlatformPlayAudioAsync(string source, CancellationToken cancellationToken)
    {
#if ANDROID
        _androidPlayer?.Release();
        _androidPlayer?.Dispose();
        _androidPlayer = new MediaPlayer();
        _androidPlayer.SetAudioAttributes(new AudioAttributes.Builder()
            .SetContentType(AudioContentType.Music)
            .SetUsage(AudioUsageKind.Media)
            .Build());
        _androidPlayer.SetDataSource(source);
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _androidPlayer.Completion += (_, _) =>
        {
            completionSource.TrySetResult();
            MainThread.BeginInvokeOnMainThread(async () => await OnPlaybackCompletedAsync());
        };
        _androidPlayer.Prepared += (_, _) => _androidPlayer?.Start();
        _androidPlayer.Error += (_, args) =>
        {
            completionSource.TrySetException(new InvalidOperationException("Android audio playback failed."));
            MainThread.BeginInvokeOnMainThread(async () => await OnPlaybackCompletedAsync());
            args.Handled = true;
        };
        _androidPlayer.PrepareAsync();
        using var registration = cancellationToken.Register(() =>
            MainThread.BeginInvokeOnMainThread(async () => await StopAsync()));
        await completionSource.Task;
        return;
#elif WINDOWS
        _windowsPlayer?.Dispose();
        _windowsPlayer = new WindowsMediaPlayer();
        _windowsPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
        _windowsPlayer.IsMuted = false;
        _windowsPlayer.Volume = 1d;
        _windowsPlayer.AutoPlay = false;
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _windowsPlayer.MediaEnded += (_, _) =>
        {
            completionSource.TrySetResult();
            MainThread.BeginInvokeOnMainThread(async () => await OnPlaybackCompletedAsync());
        };
        _windowsPlayer.MediaFailed += (_, args) =>
        {
            completionSource.TrySetException(new InvalidOperationException(args.ErrorMessage));
            MainThread.BeginInvokeOnMainThread(async () => await OnPlaybackCompletedAsync());
        };
        _windowsPlayer.Source = MediaSource.CreateFromUri(new Uri(source));
        try
        {
            _windowsPlayer.Play();
        }
        catch
        {
            await Launcher.Default.OpenAsync(new Uri(source));
            await OnPlaybackCompletedAsync();
            return;
        }
        using var registration = cancellationToken.Register(() =>
            MainThread.BeginInvokeOnMainThread(async () => await StopAsync()));
        await completionSource.Task;
        return;
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Audio playback is not configured for this platform.");
#endif
    }

    private Task PlatformStopAudioAsync()
    {
#if ANDROID
        _androidTts?.Stop();
        _androidTts?.Shutdown();
        _androidTts?.Dispose();
        _androidTts = null;

        if (_androidPlayer is not null)
        {
            if (_androidPlayer.IsPlaying)
            {
                _androidPlayer.Stop();
            }

            _androidPlayer.Release();
            _androidPlayer.Dispose();
            _androidPlayer = null;
        }

        return Task.CompletedTask;
#elif WINDOWS
        _windowsPlayer?.Pause();
        _windowsPlayer?.Dispose();
        _windowsPlayer = null;
        return Task.CompletedTask;
#else
        return Task.CompletedTask;
#endif
    }

    private Task OnPlaybackCompletedAsync()
    {
        CurrentTrack = null;
        IsPlaying = false;
        RaisePlaybackStateChanged();
        return Task.CompletedTask;
    }

    private void RaisePlaybackStateChanged() =>
        PlaybackStateChanged?.Invoke(this, CurrentTrack);

#if ANDROID
    private async Task PlatformSpeakTtsAsync(string script, string? languageCode, CancellationToken cancellationToken)
    {
        var locale = ResolveAndroidLocale(languageCode);
        var engineCandidates = new[]
        {
            GoogleTtsEnginePackage,
            SamsungTtsEnginePackage,
            string.Empty
        };

        Exception? lastException = null;

        foreach (var enginePackage in engineCandidates)
        {
            try
            {
                var spoke = await TrySpeakWithAndroidEngineAsync(script, locale, enginePackage, cancellationToken);
                if (spoke)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        _ttsCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var options = new SpeechOptions();
        var mauiLocale = await ResolveLocaleAsync(languageCode);
        if (mauiLocale is not null)
        {
            options.Locale = mauiLocale;
        }

        await MauiTextToSpeech.Default.SpeakAsync(script, options, _ttsCancellationTokenSource.Token);

        if (lastException is not null)
        {
            System.Diagnostics.Debug.WriteLine($"Android TTS engine fallback ended on MAUI default: {lastException.Message}");
        }
    }

    private async Task<bool> TrySpeakWithAndroidEngineAsync(
        string script,
        AndroidLocale locale,
        string? enginePackage,
        CancellationToken cancellationToken)
    {
        _androidTts?.Stop();
        _androidTts?.Shutdown();
        _androidTts?.Dispose();
        _androidTts = null;

        var initCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var initListener = new AndroidTtsInitListener(status => initCompletion.TrySetResult((int)status));

        _androidTts = string.IsNullOrWhiteSpace(enginePackage)
            ? new AndroidTts(Android.App.Application.Context, initListener)
            : new AndroidTts(Android.App.Application.Context, initListener, enginePackage);

        using var registration = cancellationToken.Register(() => initCompletion.TrySetCanceled(cancellationToken));
        var initStatus = await initCompletion.Task;
        if (initStatus != (int)OperationResult.Success || _androidTts is null)
        {
            return false;
        }

        var languageResult = _androidTts.SetLanguage(locale);
        if (languageResult == LanguageAvailableResult.MissingData || languageResult == LanguageAvailableResult.NotSupported)
        {
            return false;
        }

        var speakCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var utteranceId = Guid.NewGuid().ToString("N");
        _androidTts.SetOnUtteranceProgressListener(new AndroidTtsProgressListener(
            utteranceId,
            () => speakCompletion.TrySetResult(),
            message => speakCompletion.TrySetException(new InvalidOperationException(message))));

        var parameters = new Bundle();
        var speakStatus = _androidTts.Speak(script, QueueMode.Flush, parameters, utteranceId);
        if (speakStatus != OperationResult.Success)
        {
            return false;
        }

        using var speakRegistration = cancellationToken.Register(() =>
        {
            _androidTts?.Stop();
            speakCompletion.TrySetCanceled(cancellationToken);
        });

        await speakCompletion.Task;
        return true;
    }

    private static AndroidLocale ResolveAndroidLocale(string? languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return AndroidLocale.Default;
        }

        var parts = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? new AndroidLocale(parts[0], parts[1])
            : new AndroidLocale(parts[0]);
    }

    private sealed class AndroidTtsInitListener(Action<OperationResult> onInitialized) : Java.Lang.Object, AndroidTts.IOnInitListener
    {
        public void OnInit(OperationResult status) => onInitialized(status);
    }

    private sealed class AndroidTtsProgressListener(
        string utteranceId,
        Action onDone,
        Action<string> onError) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceIdValue)
        {
        }

        public override void OnDone(string? utteranceIdValue)
        {
            if (string.Equals(utteranceIdValue, utteranceId, StringComparison.Ordinal))
            {
                onDone();
            }
        }

        public override void OnError(string? utteranceIdValue)
        {
            if (string.Equals(utteranceIdValue, utteranceId, StringComparison.Ordinal))
            {
                onError("Android TTS engine failed while speaking.");
            }
        }
    }
#endif
}
