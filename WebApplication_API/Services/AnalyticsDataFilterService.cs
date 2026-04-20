using WebApplication_API.Model;

namespace WebApplication_API.Services;

public sealed class AnalyticsDataFilterService
{
    public IQueryable<PlaybackEvent> ApplyPlaybackFilter(IQueryable<PlaybackEvent> query, bool includeSynthetic)
    {
        if (includeSynthetic)
        {
            return query;
        }

        return query.Where(item =>
            (item.DeviceId == null
                || (!item.DeviceId.StartsWith("analytics-demo-")
                    && !item.DeviceId.StartsWith("android-demo-")
                    && !item.DeviceId.StartsWith("hash-device-")
                    && !item.DeviceId.StartsWith("test-")
                    && !item.DeviceId.StartsWith("seed-")
                    && !item.DeviceId.StartsWith("demo-")))
            && (item.SessionId == null
                || (!item.SessionId.StartsWith("analytics-session-")
                    && !item.SessionId.StartsWith("session-demo-")
                    && !item.SessionId.StartsWith("hash-session-")
                    && !item.SessionId.StartsWith("test-")
                    && !item.SessionId.StartsWith("seed-")
                    && !item.SessionId.StartsWith("demo-")))
            && (item.Context == null
                || (!item.Context.StartsWith("synthetic")
                    && !item.Context.StartsWith("test")
                    && !item.Context.StartsWith("seed")
                    && !item.Context.StartsWith("demo")
                    && !item.Context.StartsWith("harness")))
            && !item.TriggerSource.StartsWith("synthetic")
            && !item.TriggerSource.StartsWith("test")
            && !item.TriggerSource.StartsWith("seed")
            && !item.TriggerSource.StartsWith("demo")
            && !item.TriggerSource.StartsWith("harness"));
    }

    public IQueryable<LocationTrackingEvent> ApplyTrackingFilter(IQueryable<LocationTrackingEvent> query, bool includeSynthetic)
    {
        if (includeSynthetic)
        {
            return query;
        }

        return query.Where(item =>
            !item.DeviceId.StartsWith("analytics-demo-")
            && !item.DeviceId.StartsWith("android-demo-")
            && !item.DeviceId.StartsWith("hash-device-")
            && !item.DeviceId.StartsWith("test-")
            && !item.DeviceId.StartsWith("seed-")
            && !item.DeviceId.StartsWith("demo-")
            && (item.SessionId == null
                || (!item.SessionId.StartsWith("analytics-session-")
                    && !item.SessionId.StartsWith("session-demo-")
                    && !item.SessionId.StartsWith("hash-session-")
                    && !item.SessionId.StartsWith("test-")
                    && !item.SessionId.StartsWith("seed-")
                    && !item.SessionId.StartsWith("demo-")))
            && (item.Context == null
                || (!item.Context.StartsWith("synthetic")
                    && !item.Context.StartsWith("test")
                    && !item.Context.StartsWith("seed")
                    && !item.Context.StartsWith("demo")
                    && !item.Context.StartsWith("harness"))));
    }

    public IQueryable<AudioListeningSession> ApplyListeningFilter(IQueryable<AudioListeningSession> query, bool includeSynthetic)
    {
        if (includeSynthetic)
        {
            return query;
        }

        return query.Where(item =>
            !item.DeviceId.StartsWith("analytics-demo-")
            && !item.DeviceId.StartsWith("android-demo-")
            && !item.DeviceId.StartsWith("hash-device-")
            && !item.DeviceId.StartsWith("test-")
            && !item.DeviceId.StartsWith("seed-")
            && !item.DeviceId.StartsWith("demo-")
            && (item.SessionId == null
                || (!item.SessionId.StartsWith("analytics-session-")
                    && !item.SessionId.StartsWith("session-demo-")
                    && !item.SessionId.StartsWith("hash-session-")
                    && !item.SessionId.StartsWith("test-")
                    && !item.SessionId.StartsWith("seed-")
                    && !item.SessionId.StartsWith("demo-")))
            && (item.Context == null
                || (!item.Context.StartsWith("synthetic")
                    && !item.Context.StartsWith("test")
                    && !item.Context.StartsWith("seed")
                    && !item.Context.StartsWith("demo")
                    && !item.Context.StartsWith("harness"))));
    }
}
