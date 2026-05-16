namespace Project_SharedClassLibrary.Contracts;

public sealed class ActivityLogQueryDto
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public string? Search { get; init; }
    public string? Action { get; init; }
    public string? Entity { get; init; }
}

public sealed class ActivityLogEntryDto
{
    public int Id { get; init; }
    public int? UserId { get; init; }
    public string UserName { get; init; } = "";
    public string? FullName { get; init; }
    public string Role { get; init; } = "";
    public string ActionType { get; init; } = "";
    public string EntityType { get; init; } = "";
    public int? EntityId { get; init; }
    public string? EntityName { get; init; }
    public string Summary { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

public sealed class ActivityLogListDto
{
    public IReadOnlyList<ActivityLogEntryDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
