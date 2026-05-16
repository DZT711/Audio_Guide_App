using System;

namespace Project_SharedClassLibrary.Contracts;

public sealed class ServerInfoDto
{
    public string Status { get; init; } = "Online";
    public string ApiVersion { get; init; } = "";
    public string FrameworkDescription { get; init; } = "";
    public string EnvironmentName { get; init; } = "";
    public string TimeZoneDisplayName { get; init; } = "";
    public int ActiveAdminUserCount { get; init; }
    public DateTime ServerTimeUtc { get; init; }
    public DateTime ServerTimeLocal { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public long UptimeSeconds { get; init; }
}

public sealed class PublicServerInfoDto
{
    public string Status { get; init; } = "Online";
    public string ApiVersion { get; init; } = "";
    public string FrameworkDescription { get; init; } = "";
    public string EnvironmentName { get; init; } = "";
    public string TimeZoneDisplayName { get; init; } = "";
    public DateTime ServerTimeUtc { get; init; }
    public DateTime ServerTimeLocal { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public long UptimeSeconds { get; init; }
}
