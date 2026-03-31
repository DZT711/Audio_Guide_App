using System.Globalization;
using Project_SharedClassLibrary.Constants;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public static class TourPlanningService
{
    public static TourMetrics CalculateMetrics(IEnumerable<Location> orderedLocations, string? startTime) =>
        CalculateMetrics(orderedLocations, TourDefaults.DefaultWalkingSpeedKph, startTime);

    public static TourMetrics CalculateMetrics(IEnumerable<Location> orderedLocations, double walkingSpeedKph, string? startTime)
    {
        var locations = orderedLocations.ToList();
        if (locations.Count <= 1)
        {
            return new TourMetrics(0d, 0, NormalizeTime(startTime), NormalizeTime(startTime));
        }

        var totalDistanceKm = 0d;
        for (var index = 1; index < locations.Count; index++)
        {
            totalDistanceKm += CalculateDistanceKm(locations[index - 1], locations[index]);
        }

        totalDistanceKm = Math.Round(totalDistanceKm, 2, MidpointRounding.AwayFromZero);
        var estimatedDurationMinutes = CalculateDurationMinutes(totalDistanceKm, walkingSpeedKph);

        var normalizedStartTime = NormalizeTime(startTime);
        return new TourMetrics(
            totalDistanceKm,
            estimatedDurationMinutes,
            normalizedStartTime,
            CalculateFinishTime(normalizedStartTime, estimatedDurationMinutes));
    }

    public static double CalculateDistanceKm(Location from, Location to) =>
        CalculateDistanceKm(from.Latitude, from.Longitude, to.Latitude, to.Longitude);

    public static double CalculateDistanceKm(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        const double EarthRadiusKm = 6371d;
        var latDelta = DegreesToRadians(toLatitude - fromLatitude);
        var lonDelta = DegreesToRadians(toLongitude - fromLongitude);
        var originLatitude = DegreesToRadians(fromLatitude);
        var destinationLatitude = DegreesToRadians(toLatitude);

        var haversine =
            Math.Pow(Math.Sin(latDelta / 2d), 2d)
            + Math.Cos(originLatitude) * Math.Cos(destinationLatitude) * Math.Pow(Math.Sin(lonDelta / 2d), 2d);

        var arc = 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine));
        return EarthRadiusKm * arc;
    }

    public static string? CalculateFinishTime(string? startTime, int durationMinutes)
    {
        if (!TryParseTime(startTime, out var parsedStartTime))
        {
            return null;
        }

        return parsedStartTime.Add(TimeSpan.FromMinutes(Math.Max(0, durationMinutes))).ToString(@"hh\:mm");
    }

    public static string? NormalizeTime(string? value) =>
        TryParseTime(value, out var parsedTime)
            ? parsedTime.ToString(@"hh\:mm")
            : null;

    public static int CalculateDurationMinutes(double totalDistanceKm, double walkingSpeedKph) =>
        walkingSpeedKph <= 0
            ? 0
            : (int)Math.Ceiling(Math.Max(0d, totalDistanceKm) / walkingSpeedKph * 60d);

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            time = default;
            return false;
        }

        var normalizedValue = value.Trim();
        if (TimeSpan.TryParseExact(
                normalizedValue,
                ["hh\\:mm", "h\\:mm", "hh\\:mm\\:ss", "h\\:mm\\:ss"],
                CultureInfo.InvariantCulture,
                out time))
        {
            return true;
        }

        if (DateTime.TryParseExact(
                normalizedValue,
                ["h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime)
            || DateTime.TryParse(
                normalizedValue,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out parsedDateTime)
            || DateTime.TryParse(
                normalizedValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out parsedDateTime))
        {
            time = parsedDateTime.TimeOfDay;
            return true;
        }

        time = default;
        return false;
    }

    private static double DegreesToRadians(double value) => value * Math.PI / 180d;
}

public sealed record TourMetrics(
    double TotalDistanceKm,
    int EstimatedDurationMinutes,
    string? StartTime,
    string? FinishTime);
