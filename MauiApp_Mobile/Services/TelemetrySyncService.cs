using System.Net.Http.Json;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using Microsoft.Maui.Networking;

namespace MauiApp_Mobile.Services;

public sealed class TelemetrySyncService
{
    private const int BatchSize = 120;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);
    private static readonly HttpClient HttpClient = MobileApiHttpClientFactory.Create(TimeSpan.FromSeconds(20), 4);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private bool _started;

    public static TelemetrySyncService Instance { get; } = new();

    private TelemetrySyncService()
    {
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = RunHeartbeatLoopAsync(_heartbeatCts.Token);
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        if (_heartbeatCts is not null)
        {
            try
            {
                _heartbeatCts.Cancel();
            }
            catch
            {
            }

            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }

        _heartbeatTask = null;
        _started = false;
    }

    public async Task TriggerSyncAsync(CancellationToken cancellationToken = default)
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
            await SyncRouteHistoryAsync(cancellationToken);
            await SyncHeatmapEventsAsync(cancellationToken);
            await SyncAudioPlayEventsAsync(cancellationToken);
            await SyncAudioListeningSessionsAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private static async Task SyncRouteHistoryAsync(CancellationToken cancellationToken)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var pending = await MobileDatabaseService.Instance.GetPendingRouteTelemetryAsync(BatchSize, cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            var payload = new RouteHistoryBatchIngestRequest
            {
                Samples = pending.Select(item => new RouteHistorySampleIngestDto
                {
                    DeviceHash = item.DeviceHash,
                    SessionHash = item.SessionHash,
                    CapturedAtUtc = item.CapturedAtUtc.UtcDateTime,
                    Latitude = item.Latitude,
                    Longitude = item.Longitude,
                    AccuracyMeters = item.AccuracyMeters,
                    SpeedMetersPerSecond = item.SpeedMetersPerSecond,
                    BatteryPercent = item.BatteryPercent,
                    IsForeground = item.IsForeground,
                    TourId = item.TourId,
                    PoiId = item.PoiId,
                    Context = item.Context
                }).ToList()
            };

            var queueIds = pending.Select(item => item.QueueId).ToList();
            if (await TryPostAsync(ApiRoutes.TelemetryIngestRouteHistoryV1, payload, cancellationToken))
            {
                await MobileDatabaseService.Instance.DeleteRouteTelemetryAsync(queueIds, cancellationToken);
            }
            else
            {
                await MobileDatabaseService.Instance.MarkRouteTelemetryAttemptFailedAsync(queueIds, cancellationToken);
                return;
            }
        }
    }

    private static async Task SyncAudioPlayEventsAsync(CancellationToken cancellationToken)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var pending = await MobileDatabaseService.Instance.GetPendingAudioPlayEventsAsync(BatchSize, cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            var payload = new AudioPlayEventBatchIngestRequest
            {
                Events = pending.Select(item => new AudioPlayEventIngestDto
                {
                    DeviceHash = item.DeviceHash,
                    SessionHash = item.SessionHash,
                    PlayedAtUtc = item.PlayedAtUtc.UtcDateTime,
                    AudioId = item.AudioId,
                    PoiId = item.PoiId,
                    TourId = item.TourId,
                    EventType = item.EventType,
                    TriggerSource = item.TriggerSource,
                    ListeningSeconds = item.ListeningSeconds,
                    PositionSeconds = item.PositionSeconds,
                    BatteryPercent = item.BatteryPercent,
                    NetworkType = item.NetworkType,
                    Context = item.Context
                }).ToList()
            };

            var queueIds = pending.Select(item => item.QueueId).ToList();
            if (await TryPostAsync(ApiRoutes.TelemetryIngestAudioPlayEventsV1, payload, cancellationToken))
            {
                await MobileDatabaseService.Instance.DeleteAudioPlayEventsAsync(queueIds, cancellationToken);
            }
            else
            {
                await MobileDatabaseService.Instance.MarkAudioPlayEventsAttemptFailedAsync(queueIds, cancellationToken);
                return;
            }
        }
    }

    private static async Task SyncHeatmapEventsAsync(CancellationToken cancellationToken)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var pending = await MobileDatabaseService.Instance.GetPendingHeatmapEventsAsync(BatchSize, cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            var payload = new HeatmapEventBatchIngestRequest
            {
                Events = pending.Select(item => new HeatmapEventIngestDto
                {
                    DeviceHash = item.DeviceHash,
                    SessionHash = item.SessionHash,
                    CapturedAtUtc = item.CapturedAtUtc.UtcDateTime,
                    Latitude = item.Latitude,
                    Longitude = item.Longitude,
                    AccuracyMeters = item.AccuracyMeters,
                    SpeedMetersPerSecond = item.SpeedMetersPerSecond,
                    BatteryPercent = item.BatteryPercent,
                    IsForeground = item.IsForeground,
                    PoiId = item.PoiId,
                    TourId = item.TourId,
                    EventType = item.EventType,
                    Weight = item.Weight,
                    TriggerSource = item.TriggerSource,
                    Context = item.Context
                }).ToList()
            };

            var queueIds = pending.Select(item => item.QueueId).ToList();
            if (await TryPostAsync(ApiRoutes.TelemetryIngestHeatmapEventsV1, payload, cancellationToken))
            {
                await MobileDatabaseService.Instance.DeleteHeatmapEventsAsync(queueIds, cancellationToken);
            }
            else
            {
                await MobileDatabaseService.Instance.MarkHeatmapEventsAttemptFailedAsync(queueIds, cancellationToken);
                return;
            }
        }
    }

    private static async Task SyncAudioListeningSessionsAsync(CancellationToken cancellationToken)
    {
        for (var pass = 0; pass < 6; pass++)
        {
            var pending = await MobileDatabaseService.Instance.GetPendingAudioListeningSessionsAsync(BatchSize, cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            var payload = new AudioListeningSessionBatchIngestRequest
            {
                Sessions = pending.Select(item => new AudioListeningSessionIngestDto
                {
                    DeviceHash = item.DeviceHash,
                    SessionHash = item.SessionHash,
                    AudioId = item.AudioId,
                    PoiId = item.PoiId,
                    TourId = item.TourId,
                    StartedAtUtc = item.StartedAtUtc.UtcDateTime,
                    EndedAtUtc = item.EndedAtUtc.UtcDateTime,
                    ListeningSeconds = item.ListeningSeconds,
                    IsCompleted = item.IsCompleted,
                    InterruptedReason = item.InterruptedReason,
                    Context = item.Context
                }).ToList()
            };

            var queueIds = pending.Select(item => item.QueueId).ToList();
            if (await TryPostAsync(ApiRoutes.TelemetryIngestAudioListeningSessionsV1, payload, cancellationToken))
            {
                await MobileDatabaseService.Instance.DeleteAudioListeningSessionsAsync(queueIds, cancellationToken);
            }
            else
            {
                await MobileDatabaseService.Instance.MarkAudioListeningSessionsAttemptFailedAsync(queueIds, cancellationToken);
                return;
            }
        }
    }

    private static async Task<bool> TryPostAsync<TPayload>(
        string route,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.PostAsJsonAsync(route, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[TelemetrySync] Failed to sync {route}: {(int)response.StatusCode} {response.ReasonPhrase}");
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TelemetrySync] Failed to sync {route}: {ex.Message}");
            return false;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        _ = TriggerSyncAsync();
        _ = SendHeartbeatAsync();
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SendHeartbeatAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        if (!AppDataModeService.Instance.IsApiEnabled || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        try
        {
            var identity = TelemetryAnonymizerService.Instance.CreateIdentitySnapshot();
            var locationId = PlaybackCoordinatorService.Instance.CurrentTrack?.LocationId;
            var payload = new TelemetryHeartbeatRequest
            {
                DeviceHash = identity.DeviceHash,
                SessionHash = identity.SessionHash,
                PoiId = locationId is > 0 ? locationId : null,
                TourId = null,
                Context = "mobile-heartbeat"
            };

            await TryPostAsync(ApiRoutes.TelemetryHeartbeatV1, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }
    }
}
