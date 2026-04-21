using System.Text.Json.Serialization;

namespace Project_SharedClassLibrary.Contracts;

public enum UsageEventType
{
    Unknown = 0,
    ViewMap = 1,
    PlayAudio = 2,
    ViewPoi = 3,
    AppOpen = 4
}

public sealed class UsageEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string DeviceId { get; init; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UsageEventType EventType { get; init; } = UsageEventType.Unknown;

    public string? ReferenceId { get; init; }

    public string? Details { get; init; }

    public int DurationSeconds { get; init; }

    public DateTime Timestamp { get; init; }
}

public sealed class TopPoiInteractionDto
{
    public string ReferenceId { get; init; } = "";

    public int InteractionCount { get; init; }

    public int PlayAudioCount { get; init; }

    public int ViewPoiCount { get; init; }
}

public sealed class UsageStatisticsDto
{
    public int TotalAudioPlays { get; init; }

    public int TotalMapViews { get; init; }

    public int UniqueUsers { get; init; }

    public int OnlineUsers { get; init; }

    public IReadOnlyList<TopPoiInteractionDto> TopPoiInteractions { get; init; } = [];
}

public sealed class UsageEventIngestResultDto
{
    public int AcceptedCount { get; init; }

    public int RejectedCount { get; init; }
}

public sealed class UsageEventHistoryPageDto
{
    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<UsageEvent> Items { get; init; } = [];
}
