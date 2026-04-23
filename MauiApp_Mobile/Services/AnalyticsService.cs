using System.Net.Http.Json;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;

namespace MauiApp_Mobile.Services;

public interface IAnalyticsService
{
    Task TrackEventAsync(
        UsageEventType eventType,
        string? referenceId = null,
        int durationSeconds = 0,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task SyncEventsToServerAsync(CancellationToken cancellationToken = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private const int SyncBatchSize = 100;
    private const string DeviceIdPreferenceKey = "analytics.device.id";
    private static readonly HttpClient HttpClient = MobileApiHttpClientFactory.Create(TimeSpan.FromSeconds(20), 4);
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public static AnalyticsService Instance { get; } = new();

    private AnalyticsService()
    {
    }

    public async Task TrackEventAsync(
        UsageEventType eventType,
        string? referenceId = null,
        int durationSeconds = 0,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        if (eventType == UsageEventType.Unknown)
        {
            return;
        }

        try
        {
            var deviceId = GetOrCreateAnonymousDeviceId();
            var queuedEvent = new UsageEventQueueRecord(
                EventId: Guid.NewGuid(),
                DeviceId: deviceId,
                EventType: eventType,
                ReferenceId: TrimToLengthOrNull(referenceId, 128),
                Details: TrimToLengthOrNull(details, 4000),
                DurationSeconds: Math.Max(0, durationSeconds),
                TimestampUtc: DateTimeOffset.UtcNow);

            await MobileDatabaseService.Instance.EnqueueUsageEventsAsync([queuedEvent], cancellationToken);

            if (AppDataModeService.Instance.IsApiEnabled &&
                Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                _ = SyncEventsToServerAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsService] Failed to track event '{eventType}': {ex.Message}");
        }
    }

    public async Task SyncEventsToServerAsync(CancellationToken cancellationToken = default)
    {
        if (!AppDataModeService.Instance.IsApiEnabled || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            for (var pass = 0; pass < 10; pass++)
            {
                var pending = await MobileDatabaseService.Instance.GetPendingUsageEventsAsync(SyncBatchSize, cancellationToken);
                if (pending.Count == 0)
                {
                    return;
                }

                var queueIds = pending.Select(item => item.QueueId).ToList();
                var payload = pending
                    .Where(item => item.EventType != UsageEventType.Unknown)
                    .Select(item => new UsageEvent
                    {
                        Id = item.EventId,
                        DeviceId = item.DeviceId,
                        EventType = item.EventType,
                        ReferenceId = item.ReferenceId,
                        Details = item.Details,
                        DurationSeconds = item.DurationSeconds,
                        Timestamp = item.TimestampUtc.UtcDateTime
                    })
                    .ToList();

                if (payload.Count == 0)
                {
                    await MobileDatabaseService.Instance.DeleteUsageEventsAsync(queueIds, cancellationToken);
                    continue;
                }

                if (await TryPostUsageEventsAsync(payload, cancellationToken))
                {
                    await MobileDatabaseService.Instance.DeleteUsageEventsAsync(queueIds, cancellationToken);
                }
                else
                {
                    await MobileDatabaseService.Instance.MarkUsageEventsAttemptFailedAsync(queueIds, cancellationToken);
                    return;
                }
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static async Task<bool> TryPostUsageEventsAsync(
        IReadOnlyList<UsageEvent> events,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.PostAsJsonAsync(ApiRoutes.AnalyticsIngestEventsV1, events, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[AnalyticsService] Failed to sync usage events: {(int)response.StatusCode} {response.ReasonPhrase}");
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnalyticsService] Failed to sync usage events: {ex.Message}");
            return false;
        }
    }

    private static string GetOrCreateAnonymousDeviceId()
    {
        try
        {
            var existing = Preferences.Default.Get(DeviceIdPreferenceKey, string.Empty)?.Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var generated = $"guest-{Guid.NewGuid():N}";
            Preferences.Default.Set(DeviceIdPreferenceKey, generated);
            return generated;
        }
        catch
        {
            return $"guest-{Guid.NewGuid():N}";
        }
    }

    private static string? TrimToLengthOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > maxLength
            ? normalized[..maxLength]
            : normalized;
    }
}
