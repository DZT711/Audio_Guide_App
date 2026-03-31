using System.ComponentModel.DataAnnotations;

namespace Project_SharedClassLibrary.Contracts;

public sealed class ChangeRequestQueryDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;

    public string? Type { get; init; }

    public string? Action { get; init; }

    public string? Status { get; init; }

    public string? Search { get; init; }
}

public sealed class InboxQueryDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;

    public bool UnreadOnly { get; init; }
}

public sealed class InboxAnnouncementRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(4000)]
    public string Body { get; set; } = "";
}

public sealed class ChangeRequestDto
{
    public int Id { get; init; }
    public string Type { get; init; } = "";
    public string ActionType { get; init; } = "";
    public int? TargetId { get; init; }
    public string Name { get; init; } = "";
    public int OwnerId { get; init; }
    public string OwnerName { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Reason { get; init; }
    public string? AdminNote { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public LocationDto? Location { get; init; }
    public AudioDto? Audio { get; init; }
}

public sealed class ChangeRequestListDto
{
    public IReadOnlyList<ChangeRequestDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ReviewChangeRequestRequest
{
    [StringLength(4000)]
    public string? AdminNote { get; set; }
}

public sealed class InboxMessageDto
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string MessageType { get; init; } = "";
    public int? RelatedRequestId { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public sealed class InboxOverviewDto
{
    public IReadOnlyList<InboxMessageDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public int UnreadCount { get; init; }
}

public sealed class LocationChangeRequestSubmission : LocationUpsertRequest
{
    [Required]
    [StringLength(16)]
    public string RequestType { get; set; } = "CREATE";

    [Range(1, int.MaxValue)]
    public int? TargetId { get; set; }

    [StringLength(2000)]
    public string? Reason { get; set; }
}

public sealed class AudioChangeRequestSubmission : AudioUpsertRequest
{
    [Required]
    [StringLength(16)]
    public string RequestType { get; set; } = "CREATE";

    [Range(1, int.MaxValue)]
    public int? TargetId { get; set; }

    [StringLength(2000)]
    public string? Reason { get; set; }
}
