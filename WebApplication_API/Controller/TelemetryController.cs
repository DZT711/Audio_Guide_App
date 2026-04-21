using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_SharedClassLibrary.Contracts;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class TelemetryController(
    DBContext context,
    ILogger<TelemetryController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("v1/route-history")]
    public async Task<ActionResult<TelemetryIngestResultDto>> IngestRouteHistory(
        [FromBody] RouteHistoryBatchIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        var samples = request.Samples ?? [];
        if (samples.Count == 0)
        {
            return BadRequest("At least one route history sample is required.");
        }

        if (samples.Count > 500)
        {
            return BadRequest("Route history batch size exceeds the limit (500).");
        }

        var acceptedItems = new List<LocationTrackingEvent>(samples.Count);
        var rejected = 0;

        foreach (var sample in samples)
        {
            if (!IsValidHash(sample.DeviceHash)
                || !IsValidLatitude(sample.Latitude)
                || !IsValidLongitude(sample.Longitude))
            {
                rejected++;
                continue;
            }

            acceptedItems.Add(new LocationTrackingEvent
            {
                DeviceId = sample.DeviceHash.Trim(),
                SessionId = NormalizeHashOrNull(sample.SessionHash),
                Latitude = sample.Latitude,
                Longitude = sample.Longitude,
                AccuracyMeters = sample.AccuracyMeters,
                SpeedMetersPerSecond = sample.SpeedMetersPerSecond,
                BatteryPercent = sample.BatteryPercent,
                IsForeground = sample.IsForeground,
                TourId = sample.TourId,
                PoiId = sample.PoiId,
                Context = NormalizeContext(sample.Context),
                CapturedAt = EnsureUtc(sample.CapturedAtUtc)
            });

            AnalyticsOnlineGuestService.TouchGuest(
                sample.SessionHash,
                sample.DeviceHash,
                sample.PoiId,
                DateTime.UtcNow);
        }

        if (acceptedItems.Count > 0)
        {
            context.LocationTrackingEvents.AddRange(acceptedItems);
            await context.SaveChangesAsync(cancellationToken);
        }

        LogRejected("route-history", acceptedItems.Count, rejected);
        return Ok(new TelemetryIngestResultDto
        {
            AcceptedCount = acceptedItems.Count,
            RejectedCount = rejected
        });
    }

    [AllowAnonymous]
    [HttpPost("v1/audio-play-events")]
    public async Task<ActionResult<TelemetryIngestResultDto>> IngestAudioPlayEvents(
        [FromBody] AudioPlayEventBatchIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        var events = request.Events ?? [];
        if (events.Count == 0)
        {
            return BadRequest("At least one audio play event is required.");
        }

        if (events.Count > 500)
        {
            return BadRequest("Audio play event batch size exceeds the limit (500).");
        }

        var acceptedItems = new List<PlaybackEvent>(events.Count);
        var rejected = 0;

        foreach (var playEvent in events)
        {
            if (!IsValidHash(playEvent.DeviceHash)
                || string.IsNullOrWhiteSpace(playEvent.EventType))
            {
                rejected++;
                continue;
            }

            acceptedItems.Add(new PlaybackEvent
            {
                DeviceId = playEvent.DeviceHash.Trim(),
                SessionId = NormalizeHashOrNull(playEvent.SessionHash),
                AudioId = playEvent.AudioId,
                LocationId = playEvent.PoiId,
                PoiId = playEvent.PoiId,
                TourId = playEvent.TourId,
                EventType = NormalizeEventType(playEvent.EventType),
                TriggerSource = NormalizeTriggerSource(playEvent.TriggerSource),
                ListeningSeconds = playEvent.ListeningSeconds,
                BatteryPercent = playEvent.BatteryPercent,
                NetworkType = NormalizeNetworkType(playEvent.NetworkType),
                Context = NormalizeContext(playEvent.Context),
                EventAt = EnsureUtc(playEvent.PlayedAtUtc)
            });

            AnalyticsOnlineGuestService.TouchGuest(
                playEvent.SessionHash,
                playEvent.DeviceHash,
                playEvent.PoiId,
                DateTime.UtcNow);
        }

        if (acceptedItems.Count > 0)
        {
            context.PlaybackEvents.AddRange(acceptedItems);
            await context.SaveChangesAsync(cancellationToken);
        }

        LogRejected("audio-play-events", acceptedItems.Count, rejected);
        return Ok(new TelemetryIngestResultDto
        {
            AcceptedCount = acceptedItems.Count,
            RejectedCount = rejected
        });
    }

    [AllowAnonymous]
    [HttpPost("v1/audio-listening-sessions")]
    public async Task<ActionResult<TelemetryIngestResultDto>> IngestAudioListeningSessions(
        [FromBody] AudioListeningSessionBatchIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessions = request.Sessions ?? [];
        if (sessions.Count == 0)
        {
            return BadRequest("At least one audio listening session is required.");
        }

        if (sessions.Count > 500)
        {
            return BadRequest("Audio listening session batch size exceeds the limit (500).");
        }

        var acceptedItems = new List<AudioListeningSession>(sessions.Count);
        var rejected = 0;

        foreach (var session in sessions)
        {
            if (!IsValidHash(session.DeviceHash)
                || session.ListeningSeconds <= 0)
            {
                rejected++;
                continue;
            }

            var startedAtUtc = EnsureUtc(session.StartedAtUtc);
            var endedAtUtc = EnsureUtc(session.EndedAtUtc);
            if (endedAtUtc < startedAtUtc)
            {
                rejected++;
                continue;
            }

            var durationSeconds = (int)Math.Ceiling((endedAtUtc - startedAtUtc).TotalSeconds);
            if (durationSeconds <= 0)
            {
                rejected++;
                continue;
            }

            var normalizedListeningSeconds = Math.Clamp(session.ListeningSeconds, 1, Math.Max(1, durationSeconds));

            acceptedItems.Add(new AudioListeningSession
            {
                DeviceId = session.DeviceHash.Trim(),
                SessionId = NormalizeHashOrNull(session.SessionHash),
                AudioId = session.AudioId,
                LocationId = session.PoiId,
                PoiId = session.PoiId,
                TourId = session.TourId,
                StartedAt = startedAtUtc,
                EndedAt = endedAtUtc,
                ListeningSeconds = normalizedListeningSeconds,
                IsCompleted = session.IsCompleted,
                InterruptedReason = NormalizeReason(session.InterruptedReason),
                Context = NormalizeContext(session.Context),
                CreatedAt = DateTime.UtcNow
            });

            AnalyticsOnlineGuestService.TouchGuest(
                session.SessionHash,
                session.DeviceHash,
                session.PoiId,
                DateTime.UtcNow);
        }

        if (acceptedItems.Count > 0)
        {
            context.AudioListeningSessions.AddRange(acceptedItems);
            await context.SaveChangesAsync(cancellationToken);
        }

        LogRejected("audio-listening-sessions", acceptedItems.Count, rejected);
        return Ok(new TelemetryIngestResultDto
        {
            AcceptedCount = acceptedItems.Count,
            RejectedCount = rejected
        });
    }

    [AllowAnonymous]
    [HttpPost("v1/heartbeat")]
    public ActionResult<TelemetryIngestResultDto> IngestHeartbeat([FromBody] TelemetryHeartbeatRequest request)
    {
        if (request is null || !IsValidHash(request.DeviceHash))
        {
            return BadRequest("A valid hashed device identity is required.");
        }

        AnalyticsOnlineGuestService.TouchGuest(
            request.SessionHash,
            request.DeviceHash,
            request.PoiId,
            DateTime.UtcNow);

        return Ok(new TelemetryIngestResultDto
        {
            AcceptedCount = 1,
            RejectedCount = 0
        });
    }

    private void LogRejected(string feed, int accepted, int rejected)
    {
        if (rejected <= 0)
        {
            return;
        }

        logger.LogWarning(
            "Telemetry ingest feed '{Feed}' rejected {Rejected} record(s); accepted {Accepted}.",
            feed,
            rejected,
            accepted);
    }

    private static bool IsValidHash(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim().Length is >= 32 and <= 128;

    private static bool IsValidLatitude(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value is >= -90d and <= 90d;

    private static bool IsValidLongitude(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value is >= -180d and <= 180d;

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    private static string? NormalizeHashOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > 128 ? normalized[..128] : normalized;
    }

    private static string NormalizeEventType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var normalized = value.Trim();
        return normalized.Length > 32 ? normalized[..32] : normalized;
    }

    private static string NormalizeTriggerSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        var normalized = value.Trim();
        return normalized.Length > 64 ? normalized[..64] : normalized;
    }

    private static string? NormalizeNetworkType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > 32 ? normalized[..32] : normalized;
    }

    private static string? NormalizeReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > 64 ? normalized[..64] : normalized;
    }

    private static string? NormalizeContext(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > 128 ? normalized[..128] : normalized;
    }
}
