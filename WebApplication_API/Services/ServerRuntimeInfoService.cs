namespace WebApplication_API.Services;

public sealed class ServerRuntimeInfoService
{
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
}
