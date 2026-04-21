using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using WebApplication_API.Data;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public interface IUsageAnalyticsService
{
    Task<UsageEventIngestResultDto> IngestEventsAsync(
        IReadOnlyList<UsageEvent> events,
        CancellationToken cancellationToken = default);

    Task<UsageStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

    Task<UsageEventHistoryPageDto> GetHistoryAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public sealed class UsageAnalyticsService(
    DBContext context,
    ILogger<UsageAnalyticsService> logger) : IUsageAnalyticsService
{
    public async Task<UsageEventIngestResultDto> IngestEventsAsync(
        IReadOnlyList<UsageEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return new UsageEventIngestResultDto();
        }

        var acceptedItems = new List<UsageEventEntity>(events.Count);
        var rejected = 0;

        foreach (var item in events)
        {
            if (!TryNormalize(item, out var normalized))
            {
                rejected++;
                continue;
            }

            acceptedItems.Add(normalized);
        }

        if (acceptedItems.Count > 0)
        {
            context.UsageEvents.AddRange(acceptedItems);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var acceptedItem in acceptedItems)
            {
                AnalyticsOnlineGuestService.TouchGuest(
                    sessionId: null,
                    deviceId: acceptedItem.DeviceId,
                    locationId: TryParsePositiveInt(acceptedItem.ReferenceId),
                    seenAtUtc: DateTime.UtcNow);
            }
        }

        if (rejected > 0)
        {
            logger.LogWarning(
                "Usage events ingest rejected {RejectedCount} events and accepted {AcceptedCount}.",
                rejected,
                acceptedItems.Count);
        }

        return new UsageEventIngestResultDto
        {
            AcceptedCount = acceptedItems.Count,
            RejectedCount = rejected
        };
    }

    public async Task<UsageStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var query = context.UsageEvents.AsNoTracking();

        var totalAudioPlaysTask = query.CountAsync(
            item => item.EventType == UsageEventType.PlayAudio,
            cancellationToken);
        var totalMapViewsTask = query.CountAsync(
            item => item.EventType == UsageEventType.ViewMap,
            cancellationToken);
        var uniqueUsersTask = query
            .Select(item => item.DeviceId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct()
            .CountAsync(cancellationToken);
        var onlineUsersTask = AnalyticsOnlineGuestService.CountOnlineUsageUsersAsync(
            context,
            AnalyticsOnlineGuestService.ResolveDefaultThresholdUtc(),
            cancellationToken);
        var topPoiTask = query
            .Where(item => !string.IsNullOrWhiteSpace(item.ReferenceId)
                           && (item.EventType == UsageEventType.PlayAudio
                               || item.EventType == UsageEventType.ViewPoi))
            .GroupBy(item => item.ReferenceId!)
            .Select(group => new TopPoiInteractionDto
            {
                ReferenceId = group.Key,
                InteractionCount = group.Count(),
                PlayAudioCount = group.Count(item => item.EventType == UsageEventType.PlayAudio),
                ViewPoiCount = group.Count(item => item.EventType == UsageEventType.ViewPoi)
            })
            .OrderByDescending(item => item.InteractionCount)
            .ThenBy(item => item.ReferenceId)
            .Take(10)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalAudioPlaysTask, totalMapViewsTask, uniqueUsersTask, onlineUsersTask, topPoiTask);

        return new UsageStatisticsDto
        {
            TotalAudioPlays = totalAudioPlaysTask.Result,
            TotalMapViews = totalMapViewsTask.Result,
            UniqueUsers = uniqueUsersTask.Result,
            OnlineUsers = onlineUsersTask.Result,
            TopPoiInteractions = topPoiTask.Result
        };
    }

    public async Task<UsageEventHistoryPageDto> GetHistoryAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageNumber = Math.Max(1, pageNumber);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.UsageEvents
            .AsNoTracking()
            .OrderByDescending(item => item.Timestamp)
            .ThenByDescending(item => item.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(item => new UsageEvent
            {
                Id = item.Id,
                DeviceId = item.DeviceId,
                EventType = item.EventType,
                ReferenceId = item.ReferenceId,
                Details = item.Details,
                DurationSeconds = item.DurationSeconds,
                Timestamp = item.Timestamp
            })
            .ToListAsync(cancellationToken);

        return new UsageEventHistoryPageDto
        {
            PageNumber = normalizedPageNumber,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    private static bool TryNormalize(UsageEvent input, out UsageEventEntity normalized)
    {
        normalized = default!;

        if (input.EventType == UsageEventType.Unknown)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.DeviceId))
        {
            return false;
        }

        var trimmedDeviceId = input.DeviceId.Trim();
        if (trimmedDeviceId.Length > 128)
        {
            trimmedDeviceId = trimmedDeviceId[..128];
        }

        if (trimmedDeviceId.Length == 0)
        {
            return false;
        }

        normalized = new UsageEventEntity
        {
            Id = input.Id == Guid.Empty ? Guid.NewGuid() : input.Id,
            DeviceId = trimmedDeviceId,
            EventType = input.EventType,
            ReferenceId = TrimToLengthOrNull(input.ReferenceId, 128),
            Details = TrimToLengthOrNull(input.Details, 4000),
            DurationSeconds = Math.Max(0, input.DurationSeconds),
            Timestamp = EnsureUtc(input.Timestamp)
        };

        return true;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value == default)
        {
            return DateTime.UtcNow;
        }

        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    private static string? TrimToLengthOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength
            ? trimmed[..maxLength]
            : trimmed;
    }

    private static int? TryParsePositiveInt(string? value) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
}
