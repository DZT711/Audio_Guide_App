using System.ComponentModel.DataAnnotations;
using Project_SharedClassLibrary.Constants;

namespace Project_SharedClassLibrary.Contracts;

public sealed class TourRoutePreviewStopRequest
{
    [Range(1, int.MaxValue)]
    public int LocationId { get; set; }

    [Range(1, int.MaxValue)]
    public int SequenceOrder { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}

public sealed class TourRoutePreviewRequest : IValidatableObject
{
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Start time must use HH:mm.")]
    public string? StartTime { get; set; } = TourDefaults.DefaultStartTime;

    public List<TourRoutePreviewStopRequest> Stops { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Stops.Count == 0)
        {
            yield return new ValidationResult("Choose at least one POI for the tour.", [nameof(Stops)]);
        }

        var duplicateIds = Stops
            .GroupBy(item => item.LocationId)
            .Where(group => group.Key > 0 && group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            yield return new ValidationResult("Each POI can only appear once in the same tour.", [nameof(Stops)]);
        }
    }
}

public sealed class TourRoutePointDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

public sealed class TourRouteSegmentDto
{
    public int SequenceOrder { get; init; }
    public int LocationId { get; init; }
    public double DistanceKm { get; init; }
}

public sealed class TourRoutePreviewDto
{
    public double TotalDistanceKm { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public double WalkingSpeedKph { get; init; } = TourDefaults.DefaultWalkingSpeedKph;
    public string? StartTime { get; init; }
    public string? FinishTime { get; init; }
    public bool UsesRoadRouting { get; init; } = true;
    public IReadOnlyList<TourRouteSegmentDto> Segments { get; init; } = [];
    public IReadOnlyList<TourRoutePointDto> Path { get; init; } = [];
}
