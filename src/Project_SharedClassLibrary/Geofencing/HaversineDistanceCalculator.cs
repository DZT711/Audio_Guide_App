namespace Project_SharedClassLibrary.Geofencing;

public static class HaversineDistanceCalculator
{
    private const double EarthRadiusMeters = 6_371_000d;

    public static double CalculateMeters(
        double latitudeA,
        double longitudeA,
        double latitudeB,
        double longitudeB)
    {
        var latitudeDeltaRadians = DegreesToRadians(latitudeB - latitudeA);
        var longitudeDeltaRadians = DegreesToRadians(longitudeB - longitudeA);
        var latitudeARadians = DegreesToRadians(latitudeA);
        var latitudeBRadians = DegreesToRadians(latitudeB);

        var haversine =
            Math.Pow(Math.Sin(latitudeDeltaRadians / 2d), 2d) +
            Math.Cos(latitudeARadians) *
            Math.Cos(latitudeBRadians) *
            Math.Pow(Math.Sin(longitudeDeltaRadians / 2d), 2d);

        var angularDistance = 2d * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1d - haversine));
        return EarthRadiusMeters * angularDistance;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
