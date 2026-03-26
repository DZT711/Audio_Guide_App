using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
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

    public static LocationDto ToDto(this Location location) =>
        new()
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
            CoverImageUrl = location.Images
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.ImageId)
                .Select(item => item.ImageUrl)
                .FirstOrDefault(),
            ImageUrls = location.Images
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.ImageId)
                .Select(item => item.ImageUrl)
                .ToList(),
            WebURL = location.WebURL,
            Email = location.Email,
            Phone = location.PhoneContact,
            EstablishedYear = location.EstablishedYear ?? DateTime.UtcNow.Year,
            AudioCount = location.AudioContents.Count,
            Status = location.Status,
            CreatedAt = location.CreatedAt,
            UpdatedAt = location.UpdatedAt
        };

    public static AudioDto ToDto(this Audio audio, Language? language = null) =>
        new()
        {
            Id = audio.AudioId,
            LocationId = audio.LocationId,
            LocationName = audio.Location?.Name ?? "Unknown",
            Language = audio.LanguageCode,
            LanguageName = language?.LangName,
            NativeLanguageName = language?.NativeName,
            PreferNativeVoice = language?.PreferNativeVoice ?? true,
            Title = audio.Title,
            Description = audio.Description,
            SourceType = audio.SourceType,
            Script = audio.Script,
            AudioURL = audio.FilePath,
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
}
