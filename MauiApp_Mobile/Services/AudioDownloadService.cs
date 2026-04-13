using System.Text.Json;
using System.Net;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Constants;

namespace MauiApp_Mobile.Services;

public sealed class AudioDownloadService
{
    private static readonly HttpClient DownloadHttpClient = MobileApiHttpClientFactory.Create(
        TimeSpan.FromMinutes(4),
        6);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string DownloadDirectoryPath = Path.Combine(FileSystem.Current.AppDataDirectory, "downloaded-audio");
    private static readonly string ManifestFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "downloaded-audio-manifest.json");

    public static AudioDownloadService Instance { get; } = new();

    private readonly SemaphoreSlim _manifestLock = new(1, 1);
    private readonly Dictionary<int, DownloadedAudioEntry> _entries = new();
    private bool _isManifestLoaded;

    private AudioDownloadService()
    {
    }

    public async Task<AudioDownloadSnapshot> GetSnapshotAsync(PublicAudioTrackDto track, CancellationToken cancellationToken = default)
    {
        await EnsureManifestLoadedAsync(cancellationToken);

        if (!_entries.TryGetValue(track.Id, out var entry) || !File.Exists(entry.LocalFilePath))
        {
            return AudioDownloadSnapshot.NotDownloaded(track.Id);
        }

        var fileInfo = new FileInfo(entry.LocalFilePath);
        return new AudioDownloadSnapshot(
            track.Id,
            true,
            entry.LocalFilePath,
            fileInfo.Exists ? fileInfo.Length : 0,
            entry.TotalBytes > 0 ? entry.TotalBytes : fileInfo.Length,
            entry.DownloadedAt);
    }

    public async Task<IReadOnlyList<AudioDownloadSnapshot>> GetSnapshotsForLocationAsync(int locationId, CancellationToken cancellationToken = default)
    {
        await EnsureManifestLoadedAsync(cancellationToken);

        return _entries.Values
            .Where(item => item.LocationId == locationId && File.Exists(item.LocalFilePath))
            .Select(item =>
            {
                var fileInfo = new FileInfo(item.LocalFilePath);
                return new AudioDownloadSnapshot(
                    item.TrackId,
                    true,
                    item.LocalFilePath,
                    fileInfo.Exists ? fileInfo.Length : 0,
                    item.TotalBytes > 0 ? item.TotalBytes : fileInfo.Length,
                    item.DownloadedAt);
            })
            .ToList();
    }

    public async Task<PublicAudioTrackDto> ResolvePlayableTrackAsync(
        PublicAudioTrackDto track,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(track, cancellationToken);
        if (!snapshot.IsDownloaded || string.IsNullOrWhiteSpace(snapshot.LocalFilePath))
        {
            return track;
        }

        return CloneTrack(track, new Uri(snapshot.LocalFilePath).AbsoluteUri);
    }

    public async Task<AudioDownloadSnapshot> DownloadAsync(
        PublicAudioTrackDto track,
        IProgress<AudioDownloadProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(track.AudioURL))
        {
            throw new InvalidOperationException("Audio này chỉ có TTS script, chưa có file ghi âm để tải về.");
        }

        await EnsureManifestLoadedAsync(cancellationToken);

        var sourceUri = ResolveAudioUri(track.AudioURL);
        var existingSnapshot = await GetSnapshotAsync(track, cancellationToken);
        if (existingSnapshot.IsDownloaded)
        {
            progress?.Report(new AudioDownloadProgressUpdate(existingSnapshot.DownloadedBytes, existingSnapshot.TotalBytes, 1d));
            return existingSnapshot;
        }

        Directory.CreateDirectory(DownloadDirectoryPath);

        using var response = await DownloadHttpClient.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseText = await SafeReadResponseAsync(response, cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(responseText)
                ? $"Không thể tải audio. HTTP {(int)response.StatusCode} {response.ReasonPhrase}."
                : responseText);
        }

        var totalBytes = response.Content.Headers.ContentLength;
        var fileExtension = ResolveFileExtension(sourceUri, response.Content.Headers.ContentType?.MediaType);
        var finalFilePath = Path.Combine(DownloadDirectoryPath, $"track-{track.Id}{fileExtension}");
        var tempFilePath = $"{finalFilePath}.download";

        try
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var localStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await localStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;

                var progressRatio = totalBytes.HasValue && totalBytes.Value > 0
                    ? Math.Clamp(downloadedBytes / (double)totalBytes.Value, 0d, 1d)
                    : 0d;
                var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.25d);
                var speedBytesPerSecond = downloadedBytes / elapsedSeconds;
                var update = new AudioDownloadProgressUpdate(downloadedBytes, totalBytes, progressRatio, speedBytesPerSecond);
                progress?.Report(update);
                DownloadNotificationService.ShowProgress(track.Title ?? $"Track {track.Id}", update);
            }

            await localStream.FlushAsync(cancellationToken);

            if (File.Exists(finalFilePath))
            {
                File.Delete(finalFilePath);
            }

            File.Move(tempFilePath, finalFilePath);

            var fileInfo = new FileInfo(finalFilePath);
            var entry = new DownloadedAudioEntry
            {
                TrackId = track.Id,
                LocationId = track.LocationId,
                SourceUrl = sourceUri.AbsoluteUri,
                LocalFilePath = finalFilePath,
                TotalBytes = totalBytes ?? fileInfo.Length,
                DownloadedAt = DateTimeOffset.UtcNow
            };

            _entries[track.Id] = entry;
            await SaveManifestAsync(cancellationToken);

            var snapshot = new AudioDownloadSnapshot(
                track.Id,
                true,
                finalFilePath,
                fileInfo.Length,
                totalBytes ?? fileInfo.Length,
                entry.DownloadedAt);
            progress?.Report(new AudioDownloadProgressUpdate(snapshot.DownloadedBytes, snapshot.TotalBytes, 1d));
            DownloadNotificationService.ShowSuccess(track.Title ?? $"Track {track.Id}");
            return snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            CleanupPartialDownload(tempFilePath);
            DownloadNotificationService.ShowFailure(track.Title ?? $"Track {track.Id}", BuildFailureMessage(ex));
            throw new InvalidOperationException(BuildFailureMessage(ex), ex);
        }
    }

    public async Task DeleteTrackAsync(int trackId, CancellationToken cancellationToken = default)
    {
        await EnsureManifestLoadedAsync(cancellationToken);

        if (!_entries.TryGetValue(trackId, out var entry))
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(entry.LocalFilePath) && File.Exists(entry.LocalFilePath))
            {
                File.Delete(entry.LocalFilePath);
            }
        }
        catch
        {
        }

        _entries.Remove(trackId);
        await SaveManifestAsync(cancellationToken);
    }

    public async Task DeleteLocationTracksAsync(int locationId, CancellationToken cancellationToken = default)
    {
        await EnsureManifestLoadedAsync(cancellationToken);

        var trackIds = _entries.Values
            .Where(item => item.LocationId == locationId)
            .Select(item => item.TrackId)
            .ToList();

        foreach (var trackId in trackIds)
        {
            await DeleteTrackAsync(trackId, cancellationToken);
        }
    }

    private async Task EnsureManifestLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isManifestLoaded)
        {
            return;
        }

        await _manifestLock.WaitAsync(cancellationToken);
        try
        {
            if (_isManifestLoaded)
            {
                return;
            }

            _entries.Clear();

            if (File.Exists(ManifestFilePath))
            {
                await using var stream = File.OpenRead(ManifestFilePath);
                var manifest = await JsonSerializer.DeserializeAsync<List<DownloadedAudioEntry>>(stream, JsonOptions, cancellationToken) ?? [];
                foreach (var entry in manifest.Where(item => !string.IsNullOrWhiteSpace(item.LocalFilePath) && File.Exists(item.LocalFilePath)))
                {
                    _entries[entry.TrackId] = entry;
                }
            }

            _isManifestLoaded = true;
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private async Task SaveManifestAsync(CancellationToken cancellationToken)
    {
        await _manifestLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestFilePath)!);
            await using var stream = File.Create(ManifestFilePath);
            await JsonSerializer.SerializeAsync(
                stream,
                _entries.Values.OrderBy(item => item.TrackId).ToList(),
                JsonOptions,
                cancellationToken);
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private static Uri ResolveAudioUri(string audioUrl)
    {
        return new Uri(MobileApiOptions.ResolveAudioUrl(audioUrl), UriKind.Absolute);
    }

    private static string ResolveFileExtension(Uri sourceUri, string? mediaType)
    {
        var extension = Path.GetExtension(sourceUri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return mediaType?.ToLowerInvariant() switch
        {
            "audio/wav" or "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/aac" => ".aac",
            _ => ".mp3"
        };
    }

    private static async Task<string> SafeReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void CleanupPartialDownload(string tempFilePath)
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

    private static string BuildFailureMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx when httpEx.StatusCode.HasValue =>
                $"Không thể tải audio. HTTP {(int)httpEx.StatusCode.Value} {httpEx.StatusCode.Value}.",
            HttpRequestException httpEx when !string.IsNullOrWhiteSpace(httpEx.Message) =>
                $"Không thể kết nối để tải audio: {httpEx.Message}",
            IOException ioEx when !string.IsNullOrWhiteSpace(ioEx.Message) =>
                $"Lỗi ghi file audio xuống máy: {ioEx.Message}",
            _ when !string.IsNullOrWhiteSpace(exception.Message) => exception.Message,
            _ => "Tải audio thất bại do lỗi không xác định."
        };
    }

    private static PublicAudioTrackDto CloneTrack(PublicAudioTrackDto track, string audioUrl) => new()
    {
        Id = track.Id,
        LocationId = track.LocationId,
        LocationName = track.LocationName,
        Language = track.Language,
        LanguageName = track.LanguageName,
        Title = track.Title,
        Description = track.Description,
        SourceType = track.SourceType,
        Script = track.Script,
        AudioURL = audioUrl,
        Duration = track.Duration,
        VoiceName = track.VoiceName,
        VoiceGender = track.VoiceGender,
        Priority = track.Priority,
        IsDefault = track.IsDefault
    };

    private sealed class DownloadedAudioEntry
    {
        public int TrackId { get; set; }

        public int LocationId { get; set; }

        public string SourceUrl { get; set; } = string.Empty;

        public string LocalFilePath { get; set; } = string.Empty;

        public long TotalBytes { get; set; }

        public DateTimeOffset DownloadedAt { get; set; }
    }
}

public readonly record struct AudioDownloadSnapshot(
    int TrackId,
    bool IsDownloaded,
    string? LocalFilePath,
    long DownloadedBytes,
    long? TotalBytes,
    DateTimeOffset? DownloadedAt)
{
    public static AudioDownloadSnapshot NotDownloaded(int trackId) => new(trackId, false, null, 0, null, null);
}

public readonly record struct AudioDownloadProgressUpdate(
    long DownloadedBytes,
    long? TotalBytes,
    double ProgressRatio,
    double SpeedBytesPerSecond = 0d);
