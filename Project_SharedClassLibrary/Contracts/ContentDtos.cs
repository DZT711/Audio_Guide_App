namespace Project_SharedClassLibrary.Contracts;

public sealed class CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class LanguageDto
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? NativeName { get; init; }
    public bool PreferNativeVoice { get; init; } = true;
    public bool IsDefault { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class LocationDto
{
    public int Id { get; init; }
    public int CategoryId { get; init; }
    public string Category { get; init; } = "";
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Radius { get; init; }
    public double StandbyRadius { get; init; }
    public int Priority { get; init; }
    public int DebounceSeconds { get; init; }
    public bool IsGpsTriggerEnabled { get; init; }
    public string? Address { get; init; }
    public string? PreferenceImageUrl { get; init; }
    public string? CoverImageUrl { get; init; }
    public IReadOnlyList<string> ImageUrls { get; init; } = [];
    public string? WebURL { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public int EstablishedYear { get; init; }
    public int AudioCount { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AudioDto
{
    public int Id { get; init; }
    public int LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string Language { get; init; } = "vi-VN";
    public string? LanguageName { get; init; }
    public string? NativeLanguageName { get; init; }
    public bool PreferNativeVoice { get; init; } = true;
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string SourceType { get; init; } = "TTS";
    public string? Script { get; init; }
    public string? AudioURL { get; init; }
    public int? FileSizeBytes { get; init; }
    public int Duration { get; init; }
    public string? VoiceName { get; init; }
    public string? VoiceGender { get; init; }
    public int Priority { get; init; }
    public string PlaybackMode { get; init; } = "Auto";
    public string InterruptPolicy { get; init; } = "NotificationFirst";
    public bool IsDownloadable { get; init; } = true;
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class TourStopDto
{
    public int LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string Category { get; init; } = "";
    public string? Address { get; init; }
    public string? PreferenceImageUrl { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int SequenceOrder { get; init; }
    public double SegmentDistanceKm { get; init; }
    public int Status { get; init; }
}

public sealed class TourDto
{
    public int Id { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public double TotalDistanceKm { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public double WalkingSpeedKph { get; init; }
    public string? StartTime { get; init; }
    public string? FinishTime { get; init; }
    public int StopCount { get; init; }
    public int Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<TourStopDto> Stops { get; init; } = [];
}

public sealed class DashboardUserDto
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public string? FullName { get; init; }
    public string Role { get; init; } = "";
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public int Status { get; init; }
    public int OwnedLocationCount { get; init; }
    public int OwnedAudioCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class DashboardMetricDto
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Trend { get; init; } = "";
    public string TrendTone { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Description { get; init; } = "";
    public string AccentStart { get; init; } = "";
    public string AccentEnd { get; init; } = "";
}

public sealed class DashboardActivityDto
{
    public string UserName { get; init; } = "";
    public string UserInitials { get; init; } = "";
    public string Action { get; init; } = "";
    public string TargetName { get; init; } = "";
    public DateTime OccurredAt { get; init; }
    public string TimeAgo { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class FocusItemDto
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public int Progress { get; init; }
    public string Icon { get; init; } = "";
    public string Tone { get; init; } = "";
}

public sealed class DashboardOverviewDto
{
    public IReadOnlyList<DashboardMetricDto> Metrics { get; init; } = [];
    public IReadOnlyList<DashboardActivityDto> Activities { get; init; } = [];
    public IReadOnlyList<FocusItemDto> FocusItems { get; init; } = [];
}

public sealed class AdminSessionUserDto
{
    public int UserId { get; init; }
    public string Username { get; init; } = "";
    public string? FullName { get; init; }
    public string Role { get; init; } = "";
    public int Status { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class AdminLoginResponse
{
    public string Token { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    public AdminSessionUserDto User { get; init; } = new();
}

public sealed class DashboardSnapshotDto
{
    public DateTime ExportedAt { get; init; }
    public AdminSessionUserDto RequestedBy { get; init; } = new();
    public DashboardOverviewDto Overview { get; init; } = new();
    public IReadOnlyList<CategoryDto> Categories { get; init; } = [];
    public IReadOnlyList<LocationDto> Locations { get; init; } = [];
    public IReadOnlyList<TourDto> Tours { get; init; } = [];
    public IReadOnlyList<AudioDto> AudioItems { get; init; } = [];
    public IReadOnlyList<DashboardUserDto> Users { get; init; } = [];
}

public sealed class UsageHistoryItemDto
{
    public int Id { get; init; }
    public int? LocationId { get; init; }
    public string LocationName { get; init; } = "";
    public string? PreferenceImageUrl { get; init; }
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public int? AudioId { get; init; }
    public string? AudioTitle { get; init; }
    public string TriggerSource { get; init; } = "";
    public string EventType { get; init; } = "";
    public DateTime EventAt { get; init; }
    public string TimeAgo { get; init; } = "";
    public string? DeviceId { get; init; }
    public string? SessionId { get; init; }
    public int? ListeningSeconds { get; init; }
    public int? QueuePosition { get; init; }
    public int? BatteryPercent { get; init; }
    public string? NetworkType { get; init; }
    public IReadOnlyList<string> TourNames { get; init; } = [];
}

public sealed class UsageHistoryOverviewDto
{
    public int TotalEvents { get; init; }
    public int UniqueGuests { get; init; }
    public int DistinctLocations { get; init; }
    public double AverageListeningSeconds { get; init; }
    public IReadOnlyList<UsageHistoryItemDto> Items { get; init; } = [];
}

public sealed class ModerationItemDto
{
    public string Type { get; init; } = "";
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string SubmittedBy { get; init; } = "";
    public DateTime SubmittedAt { get; init; }
    public string Status { get; init; } = "";
}

public sealed class ApiMessageResponse
{
    public string Message { get; set; } = "";
}
