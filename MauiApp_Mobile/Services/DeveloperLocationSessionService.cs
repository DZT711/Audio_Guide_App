using Microsoft.Maui.Devices.Sensors;

namespace MauiApp_Mobile.Services;

public sealed class DeveloperLocationSessionService
{
    private readonly object _gate = new();
    private DeveloperLocationSessionState? _activeSession;

    public static DeveloperLocationSessionService Instance { get; } = new();

    private DeveloperLocationSessionService()
    {
    }

    public bool IsActive => TryGetActiveSession(out _);

    public DeveloperLocationSessionState Start(Location location, TimeSpan duration)
    {
        var normalizedDuration = duration <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(20)
            : duration;

        var session = new DeveloperLocationSessionState(
            location,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.Add(normalizedDuration));

        lock (_gate)
        {
            _activeSession = session;
        }

        return session;
    }

    public void Stop()
    {
        lock (_gate)
        {
            _activeSession = null;
        }
    }

    public bool TryGetActiveSession(out DeveloperLocationSessionState? session)
    {
        lock (_gate)
        {
            if (_activeSession is not null && _activeSession.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                session = _activeSession;
                return true;
            }

            _activeSession = null;
            session = null;
            return false;
        }
    }
}

public sealed record DeveloperLocationSessionState(
    Location Location,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc);
