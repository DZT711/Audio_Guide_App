using System.Collections.Concurrent;
using WebApplication_API.Data;

namespace WebApplication_API.Services;

public static class AnalyticsOnlineGuestService
{
    private static readonly TimeSpan DefaultOnlineWindow = TimeSpan.FromSeconds(10);
    private static readonly ConcurrentDictionary<string, PresenceEntry> PresenceByGuestKey =
        new(StringComparer.OrdinalIgnoreCase);

    public static DateTime ResolveDefaultThresholdUtc() => DateTime.UtcNow - DefaultOnlineWindow;

    public static void ResetPresenceForTesting() => PresenceByGuestKey.Clear();

    public static void TouchGuest(
        string? sessionId,
        string? deviceId,
        int? locationId = null,
        DateTime? seenAtUtc = null)
    {
        var guestKey = GetGuestKey(sessionId, deviceId);
        if (string.IsNullOrWhiteSpace(guestKey))
        {
            return;
        }

        var normalizedSeenAtUtc = EnsureUtc(seenAtUtc ?? DateTime.UtcNow);
        var entry = PresenceByGuestKey.GetOrAdd(guestKey, _ => new PresenceEntry(normalizedSeenAtUtc));
        lock (entry.Gate)
        {
            if (normalizedSeenAtUtc > entry.LastSeenUtc)
            {
                entry.LastSeenUtc = normalizedSeenAtUtc;
            }

            if (locationId is > 0)
            {
                entry.LocationIds.Add(locationId.Value);
            }
        }
    }

    public static Task<int> CountOnlineUsageUsersAsync(
        DBContext context,
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default)
    {
        _ = context;
        _ = cancellationToken;
        return Task.FromResult(CollectActivePresenceGuestKeys(thresholdUtc).Count);
    }

    public static Task<int> CountScopedOnlineGuestsAsync(
        DBContext context,
        AnalyticsDataFilterService analyticsDataFilter,
        IReadOnlyCollection<int> scopedLocationIds,
        bool includeSynthetic,
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default)
    {
        if (scopedLocationIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        var locationIds = scopedLocationIds.Distinct().ToArray();
        _ = context;
        _ = analyticsDataFilter;
        _ = includeSynthetic;
        _ = cancellationToken;
        return Task.FromResult(CollectActivePresenceGuestKeys(thresholdUtc, locationIds).Count);
    }

    private static HashSet<string> CollectActivePresenceGuestKeys(
        DateTime thresholdUtc,
        IReadOnlyCollection<int>? scopedLocationIds = null)
    {
        HashSet<int>? scopedLocationSet = null;
        if (scopedLocationIds is { Count: > 0 })
        {
            scopedLocationSet = scopedLocationIds.ToHashSet();
        }

        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in PresenceByGuestKey)
        {
            var guestKey = pair.Key;
            var entry = pair.Value;

            DateTime lastSeenUtc;
            bool hasLocationContext;
            bool hasScopeMatch;
            lock (entry.Gate)
            {
                lastSeenUtc = entry.LastSeenUtc;
                hasLocationContext = entry.LocationIds.Count > 0;
                hasScopeMatch = scopedLocationSet is not null
                    && entry.LocationIds.Any(scopedLocationSet.Contains);
            }

            if (lastSeenUtc < thresholdUtc)
            {
                PresenceByGuestKey.TryRemove(guestKey, out _);
                continue;
            }

            if (scopedLocationSet is not null && (!hasLocationContext || !hasScopeMatch))
            {
                continue;
            }

            activeKeys.Add(guestKey);
        }

        return activeKeys;
    }

    private static string? GetGuestKey(string? sessionId, string? deviceId)
    {
        // Device hash is the stable anonymous identity across telemetry feeds.
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            return deviceId.Trim();
        }

        return string.IsNullOrWhiteSpace(sessionId)
            ? null
            : sessionId.Trim();
    }

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

    private sealed class PresenceEntry
    {
        public PresenceEntry(DateTime seenAtUtc)
        {
            LastSeenUtc = seenAtUtc;
        }

        public object Gate { get; } = new();
        public DateTime LastSeenUtc { get; set; }
        public HashSet<int> LocationIds { get; } = [];
    }
}
