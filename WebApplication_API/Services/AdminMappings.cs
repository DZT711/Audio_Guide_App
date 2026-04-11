using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public static class AdminMappings
{
    public static CategoryDto ToDto(this Category category) =>
        new()
        {
            Id = category.CategoryId,
            Name = category.Name,
            Description = category.Description,
            Status = category.Status,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };

    public static LanguageDto ToDto(this Language language) =>
        new()
        {
            Id = language.LanguageId,
            Code = language.LangCode,
            Name = language.LangName,
            NativeName = language.NativeName,
            PreferNativeVoice = language.PreferNativeVoice,
            IsDefault = language.IsDefault,
            Status = language.Status,
            CreatedAt = language.CreatedAt
        };

    public static DashboardUserDto ToDto(this DashboardUser user, int ownedLocationCount, int ownedAudioCount) =>
        new()
        {
            Id = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            Email = user.Email,
            Phone = user.Phone,
            Status = user.Status,
            OwnedLocationCount = ownedLocationCount,
            OwnedAudioCount = ownedAudioCount,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

    public static AdminSessionUserDto ToSessionDto(this DashboardUser user) =>
        new()
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            Status = user.Status,
            Email = user.Email,
            Phone = user.Phone,
            Permissions = AdminRolePolicies.GetPermissions(user.Role)
        };

    public static ActivityLogEntryDto ToDto(this ActivityLog item) =>
        new()
        {
            Id = item.ActivityLogId,
            UserId = item.UserId,
            UserName = item.UserName,
            FullName = item.FullName,
            Role = item.Role,
            ActionType = item.ActionType,
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            EntityName = item.EntityName,
            Summary = item.Summary,
            CreatedAt = item.CreatedAt
        };

    public static LocationDto ToDto(this Location location)
    {
        var orderedImageUrls = location.Images
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ImageId)
            .Select(item => NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl)
            .ToList();

        var preferenceImageUrl = NormalizeImagePath(location.PreferenceImageUrl)
            ?? orderedImageUrls.FirstOrDefault();

        return new LocationDto
        {
            Id = location.LocationId,
            CategoryId = location.CategoryId ?? 0,
            Category = location.Category?.Name ?? "Unassigned",
            OwnerId = location.OwnerId,
            OwnerName = location.Owner?.FullName ?? location.Owner?.Username,
            Name = location.Name,
            Description = location.Description,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Radius = location.Radius,
            StandbyRadius = location.StandbyRadius,
            Priority = location.Priority,
            DebounceSeconds = location.DebounceSeconds,
            IsGpsTriggerEnabled = location.IsGpsTriggerEnabled,
            Address = location.Address,
            PreferenceImageUrl = preferenceImageUrl,
            CoverImageUrl = preferenceImageUrl,
            ImageUrls = orderedImageUrls,
            WebURL = location.WebURL,
            Email = location.Email,
            Phone = location.PhoneContact,
            EstablishedYear = location.EstablishedYear ?? DateTime.UtcNow.Year,
            AudioCount = location.AudioContents.Count(item => item.Status == 1),
            AvailableVoiceGenders = location.AudioContents
                .Where(item => item.Status == 1 && !string.IsNullOrWhiteSpace(item.VoiceGender))
                .Select(item => item.VoiceGender!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Status = location.Status,
            CreatedAt = location.CreatedAt,
            UpdatedAt = location.UpdatedAt
        };
    }

    public static AudioDto ToDto(this Audio audio, Language? language = null) =>
        new()
        {
            Id = audio.AudioId,
            LocationId = audio.LocationId,
            LocationName = audio.Location?.Name ?? "Unknown",
            OwnerId = audio.Location?.OwnerId,
            OwnerName = audio.Location?.Owner?.FullName ?? audio.Location?.Owner?.Username,
            Language = audio.LanguageCode,
            LanguageName = language?.LangName,
            NativeLanguageName = language?.NativeName,
            PreferNativeVoice = language?.PreferNativeVoice ?? true,
            Title = audio.Title,
            Description = audio.Description,
            SourceType = audio.SourceType,
            Script = audio.Script,
            AudioURL = NormalizeAudioPath(audio.FilePath),
            FileSizeBytes = audio.FileSizeBytes,
            Duration = audio.DurationSeconds ?? 0,
            VoiceName = audio.VoiceName,
            VoiceGender = audio.VoiceGender,
            Priority = audio.Priority,
            PlaybackMode = audio.PlaybackMode,
            InterruptPolicy = audio.InterruptPolicy,
            IsDownloadable = audio.IsDownloadable,
            Status = audio.Status,
            CreatedAt = audio.CreatedAt,
            UpdatedAt = audio.UpdatedAt
        };

    public static TourDto ToDto(this Tour tour)
    {
        var orderedStops = tour.Stops
            .Where(item => item.Location is not null)
            .OrderBy(item => item.SequenceOrder)
            .ToList();

        var stopDtos = new List<TourStopDto>(orderedStops.Count);
        Location? previousLocation = null;
        foreach (var stop in orderedStops)
        {
            var location = stop.Location!;
            var segmentDistanceKm = previousLocation is null
                ? 0d
                : Math.Round(stop.SegmentDistanceKm, 2, MidpointRounding.AwayFromZero);

            stopDtos.Add(new TourStopDto
            {
                LocationId = location.LocationId,
                LocationName = location.Name,
                OwnerId = location.OwnerId,
                OwnerName = location.Owner?.FullName ?? location.Owner?.Username,
                Category = location.Category?.Name ?? "Unassigned",
                Address = location.Address,
                PreferenceImageUrl = NormalizeImagePath(location.PreferenceImageUrl)
                    ?? location.Images
                        .OrderBy(item => item.SortOrder)
                        .ThenBy(item => item.ImageId)
                        .Select(item => NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl)
                        .FirstOrDefault(),
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                SequenceOrder = stop.SequenceOrder,
                SegmentDistanceKm = segmentDistanceKm,
                Status = location.Status
            });

            previousLocation = location;
        }

        var finishTime = TourPlanningService.CalculateFinishTime(tour.StartTime, tour.EstimatedDurationMinutes);

        return new TourDto
        {
            Id = tour.TourId,
            OwnerId = tour.OwnerId,
            OwnerName = tour.Owner?.FullName ?? tour.Owner?.Username,
            Name = tour.Name,
            Description = tour.Description,
            TotalDistanceKm = Math.Round(tour.TotalDistanceKm, 2, MidpointRounding.AwayFromZero),
            EstimatedDurationMinutes = tour.EstimatedDurationMinutes,
            WalkingSpeedKph = tour.WalkingSpeedKph,
            StartTime = TourPlanningService.NormalizeTime(tour.StartTime),
            FinishTime = finishTime,
            StopCount = stopDtos.Count,
            Status = tour.Status,
            CreatedAt = tour.CreatedAt,
            UpdatedAt = tour.UpdatedAt,
            Stops = stopDtos
        };
    }

    public static UsageHistoryItemDto ToDto(this PlaybackEvent playbackEvent, IReadOnlyList<string>? tourNames = null) =>
        new()
        {
            Id = playbackEvent.PlaybackEventId,
            LocationId = playbackEvent.LocationId,
            LocationName = playbackEvent.Location?.Name ?? "Unknown location",
            PreferenceImageUrl = NormalizeImagePath(playbackEvent.Location?.PreferenceImageUrl),
            OwnerId = playbackEvent.Location?.OwnerId,
            OwnerName = playbackEvent.Location?.Owner?.FullName ?? playbackEvent.Location?.Owner?.Username,
            AudioId = playbackEvent.AudioId,
            AudioTitle = playbackEvent.Audio?.Title,
            TriggerSource = playbackEvent.TriggerSource,
            EventType = playbackEvent.EventType,
            EventAt = playbackEvent.EventAt,
            TimeAgo = playbackEvent.EventAt.ToRelativeTime(),
            DeviceId = playbackEvent.DeviceId,
            SessionId = playbackEvent.SessionId,
            ListeningSeconds = playbackEvent.ListeningSeconds,
            QueuePosition = playbackEvent.QueuePosition,
            BatteryPercent = playbackEvent.BatteryPercent,
            NetworkType = playbackEvent.NetworkType,
            TourNames = tourNames ?? []
        };

    public static string ToRelativeTime(this DateTime value)
    {
        var span = DateTime.UtcNow - value;
        if (span.TotalMinutes < 1)
        {
            return "just now";
        }

        if (span.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)span.TotalMinutes)} mins ago";
        }

        if (span.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)span.TotalHours)} hours ago";
        }

        return $"{Math.Max(1, (int)span.TotalDays)} days ago";
    }

    public static string ToInitials(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "ST";
        }

        var initials = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => char.ToUpperInvariant(item[0]))
            .Take(2)
            .ToArray();

        return initials.Length == 0 ? "ST" : new string(initials);
    }

    private static string? NormalizeAudioPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : SharedStoragePaths.NormalizePublicAudioPath(value);

    private static string? NormalizeImagePath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : SharedStoragePaths.NormalizePublicImagePath(value);
}
