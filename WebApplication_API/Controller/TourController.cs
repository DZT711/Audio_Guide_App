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
public class TourController(
    DBContext context,
    AdminRequestAuthorizationService authService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTours()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.TourRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var tours = await BuildTourQuery(access.User!)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Name)
            .ToListAsync();

        return Ok(tours.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetTourById(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.TourRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var tour = await BuildTourQuery(access.User!)
            .FirstOrDefaultAsync(item => item.TourId == id);

        if (tour is null)
        {
            return NotFound(new { message = "Tour not found." });
        }

        return Ok(tour.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> CreateTour([FromBody] TourUpsertRequest request, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.TourManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedStops = NormalizeStops(request.Stops);
        var stopValidationMessage = ValidateStops(normalizedStops);
        if (!string.IsNullOrWhiteSpace(stopValidationMessage))
        {
            return BadRequest(new { message = stopValidationMessage });
        }

        var locations = await LoadLocationsAsync(normalizedStops.Select(item => item.LocationId), cancellationToken);
        var locationValidationMessage = ValidateRequestedLocations(locations, normalizedStops.Select(item => item.LocationId));
        if (!string.IsNullOrWhiteSpace(locationValidationMessage))
        {
            return BadRequest(new { message = locationValidationMessage });
        }

        var inactiveLocation = locations.FirstOrDefault(item => item.Status != 1);
        if (inactiveLocation is not null)
        {
            return BadRequest(new { message = "Only active POIs can be added to a tour." });
        }

        var orderedLocations = OrderLocations(normalizedStops, locations);
        var metrics = TourPlanningService.CalculateMetrics(orderedLocations, request.WalkingSpeedKph, request.StartTime);
        var tour = new Tour
        {
            OwnerId = access.User!.UserId,
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            TotalDistanceKm = metrics.TotalDistanceKm,
            EstimatedDurationMinutes = metrics.EstimatedDurationMinutes,
            WalkingSpeedKph = request.WalkingSpeedKph,
            StartTime = metrics.StartTime,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var stop in normalizedStops)
        {
            tour.Stops.Add(new TourLocation
            {
                LocationId = stop.LocationId,
                SequenceOrder = stop.SequenceOrder
            });
        }

        context.Tours.Add(tour);
        await context.SaveChangesAsync(cancellationToken);

        var savedTour = await BuildTourQuery(access.User!)
            .FirstAsync(item => item.TourId == tour.TourId, cancellationToken);

        return CreatedAtAction(nameof(GetTourById), new { id = tour.TourId }, savedTour.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTour(int id, [FromBody] TourUpsertRequest request, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.TourManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tour = await context.Tours
            .Include(item => item.Stops)
            .FirstOrDefaultAsync(item => item.TourId == id, cancellationToken);

        if (tour is null)
        {
            return NotFound(new { message = "Tour not found." });
        }

        var normalizedStops = NormalizeStops(request.Stops);
        var stopValidationMessage = ValidateStops(normalizedStops);
        if (!string.IsNullOrWhiteSpace(stopValidationMessage))
        {
            return BadRequest(new { message = stopValidationMessage });
        }

        var locations = await LoadLocationsAsync(normalizedStops.Select(item => item.LocationId), cancellationToken);
        var locationValidationMessage = ValidateRequestedLocations(locations, normalizedStops.Select(item => item.LocationId));
        if (!string.IsNullOrWhiteSpace(locationValidationMessage))
        {
            return BadRequest(new { message = locationValidationMessage });
        }

        var existingStopLocationIds = tour.Stops
            .Select(item => item.LocationId)
            .ToHashSet();

        var invalidNewStop = locations.FirstOrDefault(item =>
            item.Status != 1 && !existingStopLocationIds.Contains(item.LocationId));

        if (invalidNewStop is not null)
        {
            return BadRequest(new { message = "Only active POIs can be added to a tour." });
        }

        var orderedLocations = OrderLocations(normalizedStops, locations);
        var metrics = TourPlanningService.CalculateMetrics(orderedLocations, request.WalkingSpeedKph, request.StartTime);

        tour.Name = request.Name.Trim();
        tour.Description = Normalize(request.Description);
        tour.TotalDistanceKm = metrics.TotalDistanceKm;
        tour.EstimatedDurationMinutes = metrics.EstimatedDurationMinutes;
        tour.WalkingSpeedKph = request.WalkingSpeedKph;
        tour.StartTime = metrics.StartTime;
        tour.Status = request.Status;
        tour.UpdatedAt = DateTime.UtcNow;

        var requestedLocationIds = normalizedStops
            .Select(item => item.LocationId)
            .ToHashSet();

        var removedStops = tour.Stops
            .Where(item => !requestedLocationIds.Contains(item.LocationId))
            .ToList();

        if (removedStops.Count > 0)
        {
            context.TourLocations.RemoveRange(removedStops);
        }

        var existingStops = tour.Stops.ToDictionary(item => item.LocationId);
        foreach (var stop in normalizedStops)
        {
            if (existingStops.TryGetValue(stop.LocationId, out var existingStop))
            {
                existingStop.SequenceOrder = stop.SequenceOrder;
            }
            else
            {
                tour.Stops.Add(new TourLocation
                {
                    TourId = tour.TourId,
                    LocationId = stop.LocationId,
                    SequenceOrder = stop.SequenceOrder
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return Ok(new ApiMessageResponse { Message = "Tour updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> ArchiveTour(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.TourManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var tour = await context.Tours.FirstOrDefaultAsync(item => item.TourId == id);
        if (tour is null)
        {
            return NotFound(new { message = "Tour not found." });
        }

        tour.Status = 0;
        tour.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new ApiMessageResponse { Message = "Tour archived successfully." });
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

    private async Task<List<Location>> LoadLocationsAsync(IEnumerable<int> locationIds, CancellationToken cancellationToken)
    {
        var requestedIds = locationIds
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0)
        {
            return [];
        }

        return await context.Locations
            .Include(item => item.Owner)
            .Include(item => item.Category)
            .Where(item => requestedIds.Contains(item.LocationId))
            .ToListAsync(cancellationToken);
    }

    private static List<TourStopUpsertRequest> NormalizeStops(IEnumerable<TourStopUpsertRequest> stops) =>
        stops
            .OrderBy(item => item.SequenceOrder)
            .ThenBy(item => item.LocationId)
            .Select((item, index) => new TourStopUpsertRequest
            {
                LocationId = item.LocationId,
                SequenceOrder = index + 1
            })
            .ToList();

    private static string? ValidateStops(IReadOnlyCollection<TourStopUpsertRequest> stops)
    {
        if (stops.Count == 0)
        {
            return "Choose at least one POI for the tour.";
        }

        return stops.GroupBy(item => item.LocationId).Any(group => group.Count() > 1)
            ? "Each POI can only appear once in the same tour."
            : null;
    }

    private static string? ValidateRequestedLocations(IEnumerable<Location> locations, IEnumerable<int> requestedLocationIds)
    {
        var availableIds = locations
            .Select(item => item.LocationId)
            .ToHashSet();

        return requestedLocationIds.All(availableIds.Contains)
            ? null
            : "One or more selected POIs could not be found.";
    }

    private static IReadOnlyList<Location> OrderLocations(
        IEnumerable<TourStopUpsertRequest> normalizedStops,
        IEnumerable<Location> locations)
    {
        var locationLookup = locations.ToDictionary(item => item.LocationId);
        return normalizedStops
            .OrderBy(item => item.SequenceOrder)
            .Select(item => locationLookup[item.LocationId])
            .ToList();
    }

    private static bool IsOwnerScoped(DashboardUser user) =>
        string.Equals(user.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
