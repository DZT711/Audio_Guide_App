using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace WebApplication_API.Services;

public sealed class AdminSessionTokenService
{
    private readonly ConcurrentDictionary<string, AdminSessionTicket> _tickets = new(StringComparer.Ordinal);

    public AdminSessionTicket Create(int userId, TimeSpan lifetime)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var ticket = new AdminSessionTicket(token, userId, DateTime.UtcNow.Add(lifetime));
        _tickets[token] = ticket;
        return ticket;
    }

    public bool TryGet(string token, out AdminSessionTicket ticket)
    {
        CleanupExpired();
        return _tickets.TryGetValue(token, out ticket!);
    }

    public void Remove(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _tickets.TryRemove(token, out _);
        }
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _tickets)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _tickets.TryRemove(pair.Key, out _);
            }
        }
    }
}

public sealed record AdminSessionTicket(string Token, int UserId, DateTime ExpiresAt);
