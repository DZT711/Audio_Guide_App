using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class DashboardController(
    DBContext context,
    AdminRequestAuthorizationService authService) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.DashboardView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        return Ok(await BuildOverviewAsync(access.User!));
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.DashboardExport);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var currentUser = access.User!;
        var ownerScoped = IsOwnerScoped(currentUser);
        var overview = await BuildOverviewAsync(currentUser);

        var categories = await context.Categories
            .OrderBy(item => item.Name)
            .ToListAsync();

        var locations = await context.Locations
            .Include(item => item.Category)
            .Include(item => item.Owner)
            .Include(item => item.Images)
            .Include(item => item.AudioContents)
            .Where(item => !ownerScoped || item.OwnerId == currentUser.UserId)
            .OrderBy(item => item.Name)
            .ToListAsync();

        var tours = await BuildTourQuery(currentUser)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Name)
            .ToListAsync();

        var audioItems = await context.AudioContents
            .Include(item => item.Location)
            .Where(item => !ownerScoped || item.Location!.OwnerId == currentUser.UserId)
            .OrderBy(item => item.Title)
            .ToListAsync();

        var languageLookup = await context.Languages
            .Where(item => audioItems.Select(audio => audio.LanguageCode).Contains(item.LangCode))
            .ToDictionaryAsync(item => item.LangCode, StringComparer.OrdinalIgnoreCase);

        var users = AdminRolePolicies.HasPermission(currentUser.Role, AdminPermissions.UserRead)
            ? await context.DashboardUsers
                .OrderBy(item => item.Username)
                .Select(item => new
                {
                    User = item,
                    OwnedLocationCount = item.OwnedLocations.Count,
                    OwnedAudioCount = item.OwnedLocations.SelectMany(location => location.AudioContents).Count()
                })
                .ToListAsync()
            : [];

        return Ok(new DashboardSnapshotDto
        {
            ExportedAt = DateTime.UtcNow,
            RequestedBy = currentUser.ToSessionDto(),
            Overview = overview,
            Categories = categories.Select(item => item.ToDto()).ToList(),
            Locations = locations.Select(item => item.ToDto()).ToList(),
            Tours = tours.Select(item => item.ToDto()).ToList(),
            AudioItems = audioItems
                .Select(item => item.ToDto(languageLookup.TryGetValue(item.LanguageCode, out var language) ? language : null))
                .ToList(),
            Users = users.Select(item => item.User.ToDto(item.OwnedLocationCount, item.OwnedAudioCount)).ToList()
        });
    }

    private async Task<DashboardOverviewDto> BuildOverviewAsync(DashboardUser currentUser)
    {
        var ownerScoped = IsOwnerScoped(currentUser);

        var locationsQuery = ownerScoped
            ? context.Locations.Where(item => item.OwnerId == currentUser.UserId)
            : context.Locations.AsQueryable();

        var audioQuery = ownerScoped
            ? context.AudioContents.Where(item => item.Location!.OwnerId == currentUser.UserId)
            : context.AudioContents.AsQueryable();
        var tourQuery = BuildTourScopeQuery(currentUser);
        var playbackQuery = BuildPlaybackQuery(currentUser);

        var totalLocations = await locationsQuery.CountAsync();
        var activeLocations = await locationsQuery.CountAsync(item => item.Status == 1);
        var totalAudio = await audioQuery.CountAsync();
        var activeAudio = await audioQuery.CountAsync(item => item.Status == 1);
        var totalTours = await tourQuery.CountAsync();
        var activeTours = await tourQuery.CountAsync(item => item.Status == 1);
        var canViewUsers = AdminRolePolicies.HasPermission(currentUser.Role, AdminPermissions.UserRead);
        var totalUsers = canViewUsers
            ? await context.DashboardUsers.CountAsync()
            : 1;
        var activeUsers = canViewUsers
            ? await context.DashboardUsers.CountAsync(item => item.Status == 1)
            : currentUser.Status == 1 ? 1 : 0;
        var totalPlaybackEvents = await playbackQuery.CountAsync();
        var totalTrackingEvents = ownerScoped
            ? 0
            : await context.LocationTrackingEvents.CountAsync();

        var metrics = new List<DashboardMetricDto>
        {
            new()
            {
                Title = "Total POIs",
                Value = totalLocations.ToString(),
                Trend = $"{activeLocations} active",
                TrendTone = "positive",
                Icon = "bi-geo-alt",
                Description = ownerScoped ? "Locations assigned to your account." : "Published and draft points of interest in the POC database.",
                AccentStart = "#0f766e",
                AccentEnd = "#14b8a6"
            },
            new()
            {
                Title = "Total Audios",
                Value = totalAudio.ToString(),
                Trend = $"{activeAudio} active",
                TrendTone = "positive",
                Icon = "bi-headphones",
                Description = ownerScoped ? "Audio content linked to your locations." : "Narration items supporting GPS, QR, and manual playback.",
                AccentStart = "#1d4ed8",
                AccentEnd = "#38bdf8"
            },
            new()
            {
                Title = "Curated Tours",
                Value = totalTours.ToString(),
                Trend = $"{activeTours} active",
                TrendTone = "positive",
                Icon = "bi-signpost-split",
                Description = ownerScoped
                    ? "Tours that include at least one of your owned POIs."
                    : "Route collections connecting active POIs into guided journeys.",
                AccentStart = "#ca8a04",
                AccentEnd = "#facc15"
            },
            new()
            {
                Title = canViewUsers ? "Dashboard Users" : "Your Access",
                Value = canViewUsers ? totalUsers.ToString() : currentUser.Role,
                Trend = canViewUsers ? $"{activeUsers} active" : currentUser.Status == 1 ? "Account active" : "Account inactive",
                TrendTone = "warning",
                Icon = canViewUsers ? "bi-people" : "bi-person-badge",
                Description = canViewUsers
                    ? "Accounts divided by role and status for the admin web."
                    : "Your current role and account status in the admin web.",
                AccentStart = "#7c3aed",
                AccentEnd = "#c084fc"
            },
            new()
            {
                Title = "Telemetry Events",
                Value = (totalPlaybackEvents + totalTrackingEvents).ToString(),
                Trend = $"{totalPlaybackEvents} playback logs",
                TrendTone = "negative",
                Icon = "bi-activity",
                Description = "Playback and GPS tracking entries collected for the POC analytics layer.",
                AccentStart = "#dc2626",
                AccentEnd = "#fb7185"
            }
        };

        return new DashboardOverviewDto
        {
            Metrics = metrics,
            Activities = await BuildActivitiesAsync(currentUser),
            FocusItems = await BuildFocusItemsAsync(currentUser)
        };
    }

    private async Task<List<DashboardActivityDto>> BuildActivitiesAsync(DashboardUser currentUser)
    {
        var ownerScoped = IsOwnerScoped(currentUser);
        var items = new List<DashboardActivityDto>();

        var recentLocations = await context.Locations
            .Include(item => item.Owner)
            .Where(item => !ownerScoped || item.OwnerId == currentUser.UserId)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(4)
            .ToListAsync();

        items.AddRange(recentLocations.Select(item => new DashboardActivityDto
        {
            UserName = item.Owner?.FullName ?? item.Owner?.Username ?? "Admin workspace",
            UserInitials = (item.Owner?.FullName ?? item.Owner?.Username).ToInitials(),
            Action = item.UpdatedAt is null ? "Created POI" : "Updated POI",
            TargetName = item.Name,
            OccurredAt = item.UpdatedAt ?? item.CreatedAt,
            TimeAgo = (item.UpdatedAt ?? item.CreatedAt).ToRelativeTime(),
            Status = item.Status == 1 ? "Completed" : "Pending"
        }));

        var recentAudio = await context.AudioContents
            .Include(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .Where(item => !ownerScoped || item.Location!.OwnerId == currentUser.UserId)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(4)
            .ToListAsync();

        items.AddRange(recentAudio.Select(item => new DashboardActivityDto
        {
            UserName = item.Location?.Owner?.FullName ?? item.Location?.Owner?.Username ?? "Admin workspace",
            UserInitials = (item.Location?.Owner?.FullName ?? item.Location?.Owner?.Username).ToInitials(),
            Action = item.UpdatedAt is null ? "Created Audio" : "Updated Audio",
            TargetName = item.Title,
            OccurredAt = item.UpdatedAt ?? item.CreatedAt,
            TimeAgo = (item.UpdatedAt ?? item.CreatedAt).ToRelativeTime(),
            Status = item.Status == 1 ? "Completed" : "Pending"
        }));

        if (AdminRolePolicies.HasPermission(currentUser.Role, AdminPermissions.TourRead))
        {
            var recentTours = await BuildTourQuery(currentUser)
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .Take(4)
                .ToListAsync();

            items.AddRange(recentTours.Select(item => new DashboardActivityDto
            {
                UserName = item.Owner?.FullName ?? item.Owner?.Username ?? "Routing desk",
                UserInitials = (item.Owner?.FullName ?? item.Owner?.Username).ToInitials(),
                Action = item.UpdatedAt is null ? "Created Tour" : "Updated Tour",
                TargetName = item.Name,
                OccurredAt = item.UpdatedAt ?? item.CreatedAt,
                TimeAgo = (item.UpdatedAt ?? item.CreatedAt).ToRelativeTime(),
                Status = item.Status == 1 ? "Completed" : "Pending"
            }));
        }

        if (AdminRolePolicies.HasPermission(currentUser.Role, AdminPermissions.UserRead))
        {
            var recentUsers = await context.DashboardUsers
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .Take(4)
                .ToListAsync();

            items.AddRange(recentUsers.Select(item => new DashboardActivityDto
            {
                UserName = item.FullName ?? item.Username,
                UserInitials = (item.FullName ?? item.Username).ToInitials(),
                Action = item.UpdatedAt is null ? "Created User" : "Updated User",
                TargetName = item.Username,
                OccurredAt = item.UpdatedAt ?? item.CreatedAt,
                TimeAgo = (item.UpdatedAt ?? item.CreatedAt).ToRelativeTime(),
                Status = item.Status == 1 ? "Completed" : "Failed"
            }));
        }

        return items
            .OrderByDescending(item => item.OccurredAt)
            .Take(8)
            .ToList();
    }

    private async Task<List<FocusItemDto>> BuildFocusItemsAsync(DashboardUser currentUser)
    {
        var ownerScoped = IsOwnerScoped(currentUser);
        var totalLocations = await context.Locations.CountAsync(item => !ownerScoped || item.OwnerId == currentUser.UserId);
        var activeLocations = await context.Locations.CountAsync(item =>
            (!ownerScoped || item.OwnerId == currentUser.UserId) && item.Status == 1);
        var totalAudio = await context.AudioContents.CountAsync(item =>
            !ownerScoped || item.Location!.OwnerId == currentUser.UserId);
        var scriptAudio = await context.AudioContents.CountAsync(item =>
            (!ownerScoped || item.Location!.OwnerId == currentUser.UserId) && item.Script != null && item.Script != "");
        var totalTours = await BuildTourScopeQuery(currentUser).CountAsync();
        var activeTours = await BuildTourScopeQuery(currentUser).CountAsync(item => item.Status == 1);
        var playbackEvents = await BuildPlaybackQuery(currentUser).CountAsync();

        return
        [
            new FocusItemDto
            {
                Title = "POI Publication",
                Description = "Track how many locations are active under the current scope.",
                Progress = totalLocations == 0 ? 0 : (int)Math.Round((double)activeLocations / totalLocations * 100),
                Icon = "bi-geo-alt",
                Tone = "teal"
            },
            new FocusItemDto
            {
                Title = "Audio Completeness",
                Description = "Measures how many audio records still include script support for TTS fallback.",
                Progress = totalAudio == 0 ? 0 : (int)Math.Round((double)scriptAudio / totalAudio * 100),
                Icon = "bi-mic",
                Tone = "amber"
            },
            new FocusItemDto
            {
                Title = "Tour Readiness",
                Description = "Shows how many visible tours are still active and available for guest walkthroughs.",
                Progress = totalTours == 0 ? 0 : (int)Math.Round((double)activeTours / totalTours * 100),
                Icon = "bi-signpost-2",
                Tone = "blue"
            },
            new FocusItemDto
            {
                Title = "Telemetry Coverage",
                Description = ownerScoped
                    ? "Playback logs from your owned POIs help confirm guest usage is being recorded."
                    : "Playback and GPS events confirm whether the POC trigger loop is healthy.",
                Progress = Math.Min(100, playbackEvents * 10),
                Icon = "bi-activity",
                Tone = "blue"
            }
        ];
    }

    private IQueryable<Tour> BuildTourQuery(DashboardUser currentUser)
    {
        var query = context.Tours
            .Include(item => item.Owner)
            .Include(item => item.Stops)
            .ThenInclude(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .Include(item => item.Stops)
            .ThenInclude(item => item.Location)
            .ThenInclude(item => item!.Category)
            .AsQueryable();

        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.Stops.Any(stop => stop.Location != null && stop.Location.OwnerId == currentUser.UserId))
            : query;
    }

    private IQueryable<Tour> BuildTourScopeQuery(DashboardUser currentUser)
    {
        var query = context.Tours.AsQueryable();
        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.Stops.Any(stop => stop.Location != null && stop.Location.OwnerId == currentUser.UserId))
            : query;
    }

    private IQueryable<PlaybackEvent> BuildPlaybackQuery(DashboardUser currentUser)
    {
        var query = context.PlaybackEvents.AsQueryable();
        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.Location != null && item.Location.OwnerId == currentUser.UserId)
            : query;
    }

    private static bool IsOwnerScoped(DashboardUser user) =>
        string.Equals(user.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);
}
