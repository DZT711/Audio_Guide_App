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
public class UsageController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    AnalyticsDataFilterService analyticsDataFilter) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsageHistory([FromQuery] UsageHistoryQueryDto? query = null)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UsageHistoryView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var includeSynthetic = query?.IncludeSynthetic == true;
        var items = await analyticsDataFilter
            .ApplyPlaybackFilter(BuildPlaybackQuery(access.User!), includeSynthetic)
            .OrderByDescending(item => item.EventAt)
            .ToListAsync();

        var locationIds = items
            .Where(item => item.LocationId.HasValue)
            .Select(item => item.LocationId!.Value)
            .Distinct()
            .ToList();

        var tourLookup = await LoadTourLookupAsync(locationIds);
        var usageItems = items
            .Select(item =>
            {
                var tourNames = item.LocationId.HasValue && tourLookup.TryGetValue(item.LocationId.Value, out var names)
                    ? names
                    : [];

                return item.ToDto(tourNames);
            })
            .ToList();

        var guestKeys = usageItems
            .Select(item => !string.IsNullOrWhiteSpace(item.SessionId) ? item.SessionId : item.DeviceId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var listeningSamples = usageItems
            .Where(item => item.ListeningSeconds.HasValue)
            .Select(item => item.ListeningSeconds!.Value)
            .ToList();

        return Ok(new UsageHistoryOverviewDto
        {
            TotalEvents = usageItems.Count,
            UniqueGuests = guestKeys,
            DistinctLocations = usageItems
                .Where(item => item.LocationId.HasValue)
                .Select(item => item.LocationId!.Value)
                .Distinct()
                .Count(),
            AverageListeningSeconds = listeningSamples.Count == 0
                ? 0d
                : Math.Round(listeningSamples.Average(), 1, MidpointRounding.AwayFromZero),
            Items = usageItems
        });
    }

    private IQueryable<PlaybackEvent> BuildPlaybackQuery(DashboardUser currentUser)
    {
        var query = context.PlaybackEvents
            .Include(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .Include(item => item.Audio)
            .AsQueryable();

        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.Location != null && item.Location.OwnerId == currentUser.UserId)
            : query;
    }

    private async Task<Dictionary<int, IReadOnlyList<string>>> LoadTourLookupAsync(IReadOnlyCollection<int> locationIds)
    {
        if (locationIds.Count == 0)
        {
            return [];
        }

        var items = await context.TourLocations
            .Include(item => item.Tour)
            .Where(item => locationIds.Contains(item.LocationId) && item.Tour != null)
            .ToListAsync();

        return items
            .GroupBy(item => item.LocationId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(item => item.Tour!.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private static bool IsOwnerScoped(DashboardUser user) =>
        string.Equals(user.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);
}
