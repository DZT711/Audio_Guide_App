using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;
using Project_SharedClassLibrary.Contracts;

#if ANDROID
using Android.Media;
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

    private CancellationTokenSource? _ttsCancellationTokenSource;

#if ANDROID
    private MediaPlayer? _androidPlayer;
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

        _ttsCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var options = new SpeechOptions();
        var locale = await ResolveLocaleAsync(track.Language);
        if (locale is not null)
        {
            options.Locale = locale;
        }

        await TextToSpeech.Default.SpeakAsync(track.Script, options, _ttsCancellationTokenSource.Token);
        await OnPlaybackCompletedAsync();
    }

    private static bool ShouldUseTts(PublicAudioTrackDto track) =>
        string.Equals(track.SourceType, "TTS", StringComparison.OrdinalIgnoreCase)
        || (string.IsNullOrWhiteSpace(track.AudioURL) && !string.IsNullOrWhiteSpace(track.Script));

    private static async Task<Locale?> ResolveLocaleAsync(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var locales = await TextToSpeech.Default.GetLocalesAsync();
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
}
