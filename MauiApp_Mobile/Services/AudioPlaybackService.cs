using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MauiTextToSpeech = Microsoft.Maui.Media.TextToSpeech;

#if ANDROID
using Android.Media;
using Android.Speech.Tts;
using AndroidTts = Android.Speech.Tts.TextToSpeech;
#endif

#if WINDOWS
using Windows.Media.Core;
using Windows.Media.Playback;
using WindowsMediaPlayer = Windows.Media.Playback.MediaPlayer;
#endif

namespace MauiApp_Mobile.Services;

public sealed partial class AudioPlaybackService
{
    public static AudioPlaybackService Instance { get; } = new();
    private const bool UseCloudTts = false;

    private static readonly HttpClient SpeechHttpClient = MobileApiHttpClientFactory.Create(
        TimeSpan.FromSeconds(45),
        6);

    private CancellationTokenSource? _ttsCancellationTokenSource;
    private TaskCompletionSource<bool>? _activePlaybackCompletionSource;
    private CancellationTokenSource? _progressLoopCts;
    private int _playbackSessionVersion;

#if ANDROID
    private MediaPlayer? _androidPlayer;
    private AndroidTts? _androidTts;
    private AudioManager? _androidAudioManager;
    private PlaybackAudioFocusChangeListener? _androidAudioFocusChangeListener;
    private AudioFocusRequestClass? _androidAudioFocusRequest;
    private bool _pauseRequestedByAudioFocus;
    private bool _androidPlaybackDucked;
#endif

#if WINDOWS
    private WindowsMediaPlayer? _windowsPlayer;
#endif

    private AudioPlaybackService()
    {
    }

    public event EventHandler<PublicAudioTrackDto?>? PlaybackStateChanged;
    public event EventHandler<AudioPlaybackProgressSnapshot>? PlaybackProgressChanged;

    public PublicAudioTrackDto? CurrentTrack { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool CanSeek { get; private set; }
    public TimeSpan CurrentPosition { get; private set; }
    public TimeSpan CurrentDuration { get; private set; }

    public async Task PlayAsync(PublicAudioTrackDto track, CancellationToken cancellationToken = default)
    {
        await StopAsync();
        var playbackSession = Interlocked.Increment(ref _playbackSessionVersion);

        CurrentTrack = track;
        IsLoading = true;
        IsPlaying = false;
        IsPaused = false;
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = ResolveTrackDuration(track);
        CanSeek = false;
        RaisePlaybackStateChanged();
        RaisePlaybackProgressChanged();

        try
        {
            if (UseCloudTts && ShouldUseTranslatedCloudTts(track))
            {
                try
                {
                    await PlayTranslatedCloudTtsAsync(track, cancellationToken);
                    return;
                }
                catch
                {
                }
            }

            if (ShouldUseTts(track))
            {
                try
                {
                    await PlayTtsAsync(track, cancellationToken, playbackSession);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (!string.IsNullOrWhiteSpace(track.AudioURL))
                {
                    System.Diagnostics.Debug.WriteLine($"Primary TTS playback failed for track {track.Id}. Falling back to file playback. {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(track.AudioURL))
            {
                try
                {
                    await PlatformPlayAudioAsync(ResolveAudioUrl(track.AudioURL), cancellationToken, playbackSession);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (!string.IsNullOrWhiteSpace(track.Script))
                {
                    System.Diagnostics.Debug.WriteLine($"Recorded playback failed for track {track.Id}. Falling back to TTS. {ex.Message}");
                    await PlayTtsAsync(track, cancellationToken, playbackSession);
                    return;
                }
            }

            throw new InvalidOperationException("This audio track has no playable source.");
        }
        catch
        {
            if (IsPlaybackSessionCurrent(playbackSession))
            {
                await PlatformStopAudioAsync();
                StopProgressLoop();
                CurrentTrack = null;
                IsLoading = false;
                IsPlaying = false;
                IsPaused = false;
                ResetProgressState();
                RaisePlaybackStateChanged();
                RaisePlaybackProgressChanged();
            }

            throw;
        }
    }

    public async Task TestCurrentVoiceAsync(CancellationToken cancellationToken = default)
    {
        var preferredLanguage = GetPreferredPlaybackLanguageCode();
        await PlayAsync(new PublicAudioTrackDto
        {
            Id = -1,
            Title = "Voice test",
            SourceType = "TTS",
            Language = preferredLanguage,
            Script = GetVoiceTestScript(preferredLanguage),
            VoiceGender = "Female"
        }, cancellationToken);
    }

    public async Task StopAsync()
    {
        Interlocked.Increment(ref _playbackSessionVersion);
        _ttsCancellationTokenSource?.Cancel();
        _ttsCancellationTokenSource?.Dispose();
        _ttsCancellationTokenSource = null;

        var activePlaybackCompletionSource = Interlocked.Exchange(ref _activePlaybackCompletionSource, null);
        activePlaybackCompletionSource?.TrySetCanceled();

        StopProgressLoop();

        CurrentTrack = null;
        IsLoading = false;
        IsPlaying = false;
        IsPaused = false;
        ResetProgressState();
        RaisePlaybackStateChanged();
        RaisePlaybackProgressChanged();

        await PlatformStopAudioAsync();
    }

    public async Task ShutdownForAppTerminationAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback termination cleanup failed: {ex.Message}");
        }

#if ANDROID
        try
        {
            AndroidAudioPlaybackNotificationManager.Instance.Cancel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback notification termination cleanup failed: {ex.Message}");
        }
#endif
    }

    public async Task PauseAsync(bool requestedByAudioFocus = false)
    {
        if (CurrentTrack is null || IsLoading || IsPaused)
        {
            return;
        }

        _pauseRequestedByAudioFocus = requestedByAudioFocus;

        if (_ttsCancellationTokenSource is not null && !CanSeek)
        {
            _ttsCancellationTokenSource.Cancel();
            StopProgressLoop();
            IsPlaying = false;
            IsPaused = true;
            RaisePlaybackStateChanged();
            RaisePlaybackProgressChanged();
            return;
        }

#if ANDROID
        if (_androidPlayer is not null)
        {
            if (_androidPlayer.IsPlaying)
            {
                _androidPlayer.Pause();
            }

            UpdateAndroidProgressSnapshot();
            StopProgressLoop();
            IsPlaying = false;
            IsPaused = true;
            RaisePlaybackStateChanged();
            RaisePlaybackProgressChanged();
            return;
        }

        if (_androidTts is not null)
        {
            _androidTts.Stop();
            StopProgressLoop();
            IsPlaying = false;
            IsPaused = true;
            RaisePlaybackStateChanged();
            RaisePlaybackProgressChanged();
            return;
        }
#elif WINDOWS
        if (_windowsPlayer is not null)
        {
            _windowsPlayer.Pause();
            StopProgressLoop();
            IsPlaying = false;
            IsPaused = true;
            RaisePlaybackStateChanged();
            RaisePlaybackProgressChanged();
            return;
        }
#endif

        await Task.CompletedTask;
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentTrack is null || IsLoading || IsPlaying)
        {
            return;
        }

        _pauseRequestedByAudioFocus = false;

#if ANDROID
        if (_androidPlayer is not null)
        {
            _androidPlayer.Start();
            UpdateAndroidProgressSnapshot();
            IsPaused = false;
            IsPlaying = true;
            StartProgressLoop();
            RaisePlaybackStateChanged();
            RaisePlaybackProgressChanged();
            return;
        }
#elif WINDOWS
        if (_windowsPlayer is not null)
        {
            _windowsPlayer.Play();
            IsPaused = false;
            IsPlaying = true;
            StartProgressLoop();
            RaisePlaybackStateChanged();
            RaisePlaybackProgressChanged();
            return;
        }
#endif

        if (CurrentTrack is not null)
        {
            await PlayAsync(CurrentTrack, cancellationToken);
        }
    }

    public Task TogglePauseResumeAsync(CancellationToken cancellationToken = default) =>
        IsPlaying ? PauseAsync() : ResumeAsync(cancellationToken);

    public Task ApplyRuntimeVolumeAsync()
    {
#if ANDROID
        ApplyAndroidPlayerVolume();
#elif WINDOWS
        if (_windowsPlayer is not null)
        {
            _windowsPlayer.Volume = AppSettingsService.Instance.PlaybackVolumeRatio;
        }
#endif
        return Task.CompletedTask;
    }

    public Task SeekByAsync(TimeSpan offset)
    {
        if (!CanSeek)
        {
            return Task.CompletedTask;
        }

#if ANDROID
        if (_androidPlayer is null)
        {
            return Task.CompletedTask;
        }

        var durationMs = _androidPlayer.Duration > 0 ? _androidPlayer.Duration : (int)Math.Max(CurrentDuration.TotalMilliseconds, 0);
        var targetPositionMs = Math.Clamp(_androidPlayer.CurrentPosition + (int)offset.TotalMilliseconds, 0, Math.Max(durationMs, 0));
        _androidPlayer.SeekTo(targetPositionMs);
        UpdateAndroidProgressSnapshot(targetPositionMs);
        RaisePlaybackProgressChanged();
#elif WINDOWS
        if (_windowsPlayer?.PlaybackSession is not null)
        {
            return SeekToAsync(_windowsPlayer.PlaybackSession.Position + offset);
        }
#endif

        return Task.CompletedTask;
    }

    public Task SeekToAsync(TimeSpan targetPosition)
    {
        if (!CanSeek)
        {
            return Task.CompletedTask;
        }

#if ANDROID
        if (_androidPlayer is null)
        {
            return Task.CompletedTask;
        }

        var durationMs = _androidPlayer.Duration > 0 ? _androidPlayer.Duration : (int)Math.Max(CurrentDuration.TotalMilliseconds, 0);
        var targetPositionMs = Math.Clamp((int)Math.Max(targetPosition.TotalMilliseconds, 0), 0, Math.Max(durationMs, 0));
        _androidPlayer.SeekTo(targetPositionMs);
        UpdateAndroidProgressSnapshot(targetPositionMs);
        RaisePlaybackProgressChanged();
#elif WINDOWS
        if (_windowsPlayer?.PlaybackSession is not null)
        {
            var session = _windowsPlayer.PlaybackSession;
            var safeTarget = targetPosition < TimeSpan.Zero ? TimeSpan.Zero : targetPosition;
            session.Position = session.NaturalDuration > TimeSpan.Zero && safeTarget > session.NaturalDuration
                ? session.NaturalDuration
                : safeTarget;
            CurrentPosition = session.Position;
            CurrentDuration = session.NaturalDuration;
            CanSeek = session.NaturalDuration > TimeSpan.Zero;
            RaisePlaybackProgressChanged();
        }
#endif

        return Task.CompletedTask;
    }

    private async Task PlayTtsAsync(PublicAudioTrackDto track, CancellationToken cancellationToken, int playbackSession)
    {
        if (string.IsNullOrWhiteSpace(track.Script))
        {
            throw new InvalidOperationException("The selected TTS track has no script.");
        }

        _ttsCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var options = new SpeechOptions();
        var locale = await ResolveLocaleAsync(track.Language);
        if (locale is not null)
        {
            options.Locale = locale;
        }

#if ANDROID
        RequestAndroidAudioFocus();
#endif

        MarkPlaybackStarted();
        await MauiTextToSpeech.Default.SpeakAsync(track.Script, options, _ttsCancellationTokenSource.Token);
        await OnPlaybackCompletedAsync(playbackSession);
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
            await PlatformPlayAudioAsync(tempFilePath, cancellationToken, Interlocked.CompareExchange(ref _playbackSessionVersion, 0, 0));
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

        return !LanguagePrefixesMatch(track.Language, targetLanguage)
            || string.Equals(GetLanguagePrefix(targetLanguage), "vi", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Microsoft.Maui.Media.Locale?> ResolveLocaleAsync(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var locales = await MauiTextToSpeech.Default.GetLocalesAsync();
        return locales.FirstOrDefault(item => string.Equals(item.Language, languageCode, StringComparison.OrdinalIgnoreCase))
            ?? locales.FirstOrDefault(item => item.Language.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase))
            ?? locales.FirstOrDefault(item => languageCode.StartsWith(item.Language, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveAudioUrl(string audioUrl) => MobileApiOptions.ResolveAudioUrl(audioUrl);

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

        return string.Equals(normalizedLeft.Split('-')[0], normalizedRight.Split('-')[0], StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLanguagePrefix(string? languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return string.IsNullOrWhiteSpace(normalized) ? "" : normalized.Split('-')[0];
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

    private async Task PlatformPlayAudioAsync(string source, CancellationToken cancellationToken, int playbackSession)
    {
#if ANDROID
        var playbackSource = await PrepareAndroidPlaybackSourceAsync(source, cancellationToken);
        RequestAndroidAudioFocus();
        SafeStopAndDisposeAndroidPlayer(Interlocked.Exchange(ref _androidPlayer, null));
        var player = new MediaPlayer();
        _androidPlayer = player;
        player.SetAudioAttributes(new AudioAttributes.Builder()
            .SetContentType(AudioContentType.Music)
            .SetUsage(AudioUsageKind.Media)
            .Build());

        ApplyAndroidPlayerVolume();
        player.SetDataSource(playbackSource);

        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _activePlaybackCompletionSource, completionSource);

        player.Completion += (_, _) =>
        {
            if (playbackSession != Interlocked.CompareExchange(ref _playbackSessionVersion, 0, 0))
            {
                completionSource.TrySetCanceled();
                return;
            }

            completionSource.TrySetResult(true);
            MainThread.BeginInvokeOnMainThread(async () => await OnPlaybackCompletedAsync(playbackSession));
        };
        player.Prepared += (_, _) =>
        {
            if (playbackSession != Interlocked.CompareExchange(ref _playbackSessionVersion, 0, 0))
            {
                SafeStopAndDisposeAndroidPlayer(player);
                return;
            }

            player.Start();
            UpdateAndroidProgressSnapshot();
            MarkPlaybackStarted();
        };
        player.Error += (_, args) =>
        {
            if (playbackSession != Interlocked.CompareExchange(ref _playbackSessionVersion, 0, 0))
            {
                args.Handled = true;
                completionSource.TrySetCanceled();
                return;
            }

            completionSource.TrySetException(new InvalidOperationException("Android audio playback failed."));
            args.Handled = true;
        };

        player.PrepareAsync();
        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(async () => await StopAsync()));
        try
        {
            await completionSource.Task;
        }
        finally
        {
            Interlocked.CompareExchange(ref _activePlaybackCompletionSource, null, completionSource);
        }

        return;
#elif WINDOWS
        _windowsPlayer?.Dispose();
        _windowsPlayer = new WindowsMediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
            IsMuted = false,
            Volume = AppSettingsService.Instance.PlaybackVolumeRatio,
            AutoPlay = false,
            Source = MediaSource.CreateFromUri(new Uri(source))
        };

        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _windowsPlayer.MediaEnded += (_, _) =>
        {
            completionSource.TrySetResult();
            MainThread.BeginInvokeOnMainThread(async () => await OnPlaybackCompletedAsync());
        };
        _windowsPlayer.MediaFailed += (_, args) =>
        {
            completionSource.TrySetException(new InvalidOperationException(args.ErrorMessage));
        };

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

        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(async () => await StopAsync()));
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
        try
        {
            _androidTts?.Stop();
            _androidTts?.Shutdown();
            _androidTts?.Dispose();
        }
        catch
        {
        }
        _androidTts = null;

        SafeStopAndDisposeAndroidPlayer(Interlocked.Exchange(ref _androidPlayer, null));

        ReleaseAndroidAudioFocus();

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

    private async Task OnPlaybackCompletedAsync(int playbackSession)
    {
        if (playbackSession != Interlocked.CompareExchange(ref _playbackSessionVersion, 0, 0))
        {
            return;
        }

        StopProgressLoop();
        await PlatformStopAudioAsync();
        CurrentTrack = null;
        IsLoading = false;
        IsPlaying = false;
        IsPaused = false;
        ResetProgressState();
        RaisePlaybackStateChanged();
        RaisePlaybackProgressChanged();
    }

    private void RaisePlaybackStateChanged()
    {
        PlaybackStateChanged?.Invoke(this, CurrentTrack);
#if ANDROID
        AndroidAudioPlaybackNotificationManager.Instance.Refresh(this);
#endif
    }

    private void RaisePlaybackProgressChanged()
    {
        PlaybackProgressChanged?.Invoke(this, new AudioPlaybackProgressSnapshot(
            CurrentTrack,
            CurrentPosition,
            CurrentDuration,
            CanSeek,
            IsPlaying,
            IsPaused,
            IsLoading));
#if ANDROID
        AndroidAudioPlaybackNotificationManager.Instance.Refresh(this);
#endif
    }

    private bool IsPlaybackSessionCurrent(int playbackSession) =>
        playbackSession == Interlocked.CompareExchange(ref _playbackSessionVersion, 0, 0);

    private void MarkPlaybackStarted()
    {
        if (CurrentTrack is null)
        {
            return;
        }

        IsLoading = false;
        IsPlaying = true;
        IsPaused = false;
        StartProgressLoop();
        RaisePlaybackStateChanged();
        RaisePlaybackProgressChanged();
    }

    private void ResetProgressState()
    {
        CurrentPosition = TimeSpan.Zero;
        CurrentDuration = TimeSpan.Zero;
        CanSeek = false;
    }

    private static TimeSpan ResolveTrackDuration(PublicAudioTrackDto track) =>
        track.Duration > 0 ? TimeSpan.FromSeconds(track.Duration) : TimeSpan.Zero;

    private void StartProgressLoop()
    {
        StopProgressLoop();
        if (CurrentTrack is null)
        {
            return;
        }

        _progressLoopCts = new CancellationTokenSource();
        var cancellationToken = _progressLoopCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    RefreshProgressSnapshot();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);
    }

    private void StopProgressLoop()
    {
        _progressLoopCts?.Cancel();
        _progressLoopCts?.Dispose();
        _progressLoopCts = null;
    }

    private void RefreshProgressSnapshot()
    {
#if ANDROID
        UpdateAndroidProgressSnapshot();
#elif WINDOWS
        if (_windowsPlayer?.PlaybackSession is not null)
        {
            CurrentPosition = _windowsPlayer.PlaybackSession.Position;
            CurrentDuration = _windowsPlayer.PlaybackSession.NaturalDuration;
            CanSeek = _windowsPlayer.PlaybackSession.NaturalDuration > TimeSpan.Zero;
        }
#endif
        RaisePlaybackProgressChanged();
    }

#if ANDROID
    private async Task<string> PrepareAndroidPlaybackSourceAsync(string source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
        {
            return source;
        }

        if (sourceUri.IsFile)
        {
            return sourceUri.LocalPath;
        }

        var isHttpSource = string.Equals(sourceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttpSource)
        {
            return source;
        }

        var cachedFilePath = ResolveCachedPlaybackFilePath(sourceUri);
        if (File.Exists(cachedFilePath) && new FileInfo(cachedFilePath).Length > 0)
        {
            return cachedFilePath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachedFilePath)!);
        var partialFilePath = $"{cachedFilePath}.download";

        try
        {
            using var response = await SpeechHttpClient.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var tempStream = File.Create(partialFilePath);
            await responseStream.CopyToAsync(tempStream, cancellationToken);

            if (File.Exists(cachedFilePath))
            {
                File.Delete(cachedFilePath);
            }

            File.Move(partialFilePath, cachedFilePath);
            return cachedFilePath;
        }
        catch
        {
            try
            {
                if (File.Exists(partialFilePath))
                {
                    File.Delete(partialFilePath);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private static string ResolveCachedPlaybackFilePath(Uri sourceUri)
    {
        var extension = Path.GetExtension(sourceUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mp3";
        }

        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceUri.AbsoluteUri));
        var fileName = $"{Convert.ToHexString(hashBytes).ToLowerInvariant()}{extension}";
        return Path.Combine(FileSystem.Current.CacheDirectory, "audio-cache", fileName);
    }

    private void UpdateAndroidProgressSnapshot(int? overridePositionMs = null)
    {
        if (_androidPlayer is null)
        {
            return;
        }

        try
        {
            CurrentPosition = TimeSpan.FromMilliseconds(Math.Max(overridePositionMs ?? _androidPlayer.CurrentPosition, 0));
            CurrentDuration = _androidPlayer.Duration > 0
                ? TimeSpan.FromMilliseconds(_androidPlayer.Duration)
                : CurrentDuration;
            CanSeek = _androidPlayer.Duration > 0;
        }
        catch (Java.Lang.IllegalStateException)
        {
            CurrentPosition = TimeSpan.Zero;
            CanSeek = false;
        }
    }

    private void ApplyAndroidPlayerVolume(float? overrideVolume = null)
    {
        if (_androidPlayer is null)
        {
            return;
        }

        var baseVolume = overrideVolume ?? AppSettingsService.Instance.PlaybackVolumeRatio;
        var effectiveVolume = _androidPlaybackDucked
            ? Math.Clamp(baseVolume * 0.3f, 0f, 1f)
            : Math.Clamp(baseVolume, 0f, 1f);

        try
        {
            _androidPlayer.SetVolume(effectiveVolume, effectiveVolume);
        }
        catch (Java.Lang.IllegalStateException)
        {
        }
    }

    private static void SafeStopAndDisposeAndroidPlayer(MediaPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        try
        {
            try
            {
                if (player.IsPlaying)
                {
                    player.Stop();
                }
                else
                {
                    player.Reset();
                }
            }
            catch (Java.Lang.IllegalStateException)
            {
            }

            try
            {
                player.Release();
            }
            catch
            {
            }
        }
        finally
        {
            try
            {
                player.Dispose();
            }
            catch
            {
            }
        }
    }

    private void RequestAndroidAudioFocus()
    {
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            _androidAudioManager ??= activity?.GetSystemService(Android.Content.Context.AudioService) as AudioManager
                ?? Android.App.Application.Context.GetSystemService(Android.Content.Context.AudioService) as AudioManager;

            if (_androidAudioManager is null)
            {
                return;
            }

            _androidAudioFocusChangeListener ??= new PlaybackAudioFocusChangeListener(this);
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                _androidAudioFocusRequest ??= new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                    .SetAudioAttributes(new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Media)
                        .SetContentType(AudioContentType.Speech)
                        .Build())
                    .SetWillPauseWhenDucked(false)
                    .SetOnAudioFocusChangeListener(_androidAudioFocusChangeListener)
                    .Build();

                _androidAudioManager.RequestAudioFocus(_androidAudioFocusRequest);
                return;
            }

            _androidAudioManager.RequestAudioFocus(_androidAudioFocusChangeListener, Android.Media.Stream.Music, AudioFocus.Gain);
        }
        catch
        {
        }
    }

    private void ReleaseAndroidAudioFocus()
    {
        try
        {
            if (_androidAudioManager is null || _androidAudioFocusChangeListener is null)
            {
                _pauseRequestedByAudioFocus = false;
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(26) && _androidAudioFocusRequest is not null)
            {
                _androidAudioManager.AbandonAudioFocusRequest(_androidAudioFocusRequest);
            }
            else
            {
                _androidAudioManager.AbandonAudioFocus(_androidAudioFocusChangeListener);
            }
        }
        catch
        {
        }
        finally
        {
            _pauseRequestedByAudioFocus = false;
            _androidPlaybackDucked = false;
        }
    }

    private void HandleAndroidAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Loss:
            case AudioFocus.LossTransient:
                _androidPlaybackDucked = false;
                _ = MainThread.InvokeOnMainThreadAsync(async () => await PauseAsync(requestedByAudioFocus: true));
                break;
            case AudioFocus.LossTransientCanDuck:
                if (_androidPlayer is not null)
                {
                    _androidPlaybackDucked = true;
                    ApplyAndroidPlayerVolume();
                    RaisePlaybackProgressChanged();
                }
                else
                {
                    _ = MainThread.InvokeOnMainThreadAsync(async () => await PauseAsync(requestedByAudioFocus: true));
                }
                break;
            case AudioFocus.Gain:
                if (_androidPlaybackDucked)
                {
                    _androidPlaybackDucked = false;
                    ApplyAndroidPlayerVolume();
                    RaisePlaybackProgressChanged();
                }
                break;
        }
    }

    private sealed class PlaybackAudioFocusChangeListener(AudioPlaybackService owner)
        : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        private readonly WeakReference<AudioPlaybackService> _owner = new(owner);

        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (_owner.TryGetTarget(out var target))
            {
                target.HandleAndroidAudioFocusChange(focusChange);
            }
        }
    }
#endif
}

public readonly record struct AudioPlaybackProgressSnapshot(
    PublicAudioTrackDto? Track,
    TimeSpan Position,
    TimeSpan Duration,
    bool CanSeek,
    bool IsPlaying,
    bool IsPaused,
    bool IsLoading);
