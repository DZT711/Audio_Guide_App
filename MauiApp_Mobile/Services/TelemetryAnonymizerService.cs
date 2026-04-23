using System.Security.Cryptography;
using System.Text;

namespace MauiApp_Mobile.Services;

public sealed class TelemetryAnonymizerService
{
    private const string SaltSettingKey = "telemetry.hash.salt";
    private string _salt = string.Empty;

    public static TelemetryAnonymizerService Instance { get; } = new();

    private TelemetryAnonymizerService()
    {
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await MobileDatabaseService.Instance.InitializeAsync(cancellationToken);

        var persistedSalt = await MobileDatabaseService.Instance.GetSettingAsync(SaltSettingKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(persistedSalt))
        {
            _salt = persistedSalt;
            return;
        }

        var saltBytes = RandomNumberGenerator.GetBytes(32);
        _salt = Convert.ToHexString(saltBytes).ToLowerInvariant();
        await MobileDatabaseService.Instance.SetSettingAsync(SaltSettingKey, _salt, cancellationToken);
    }

    public string HashIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        var payload = $"{_salt}|{normalized}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public TelemetryIdentitySnapshot CreateIdentitySnapshot()
    {
        var deviceHash = HashIdentifier(LocationTrackingService.Instance.DeviceId);
        var sessionHash = HashIdentifier(LocationTrackingService.Instance.SessionId);
        return new TelemetryIdentitySnapshot(deviceHash, sessionHash);
    }
}

public sealed record TelemetryIdentitySnapshot(
    string DeviceHash,
    string SessionHash);
