namespace Project_SharedClassLibrary.Geofencing;

public sealed class GeofenceSpatialIndex
{
    private readonly Dictionary<GridCell, List<PoiGeofenceDefinition>> _cells;
    private readonly Dictionary<string, PoiGeofenceDefinition> _definitionsById;
    private readonly double _cellSizeDegrees;

    private GeofenceSpatialIndex(
        Dictionary<GridCell, List<PoiGeofenceDefinition>> cells,
        Dictionary<string, PoiGeofenceDefinition> definitionsById,
        double cellSizeDegrees)
    {
        _cells = cells;
        _definitionsById = definitionsById;
        _cellSizeDegrees = cellSizeDegrees;
    }

    public static GeofenceSpatialIndex Empty { get; } = new(
        new Dictionary<GridCell, List<PoiGeofenceDefinition>>(),
        new Dictionary<string, PoiGeofenceDefinition>(StringComparer.OrdinalIgnoreCase),
        0.01d);

    public static GeofenceSpatialIndex Build(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        GeofenceEngineOptions options)
    {
        const double cellSizeDegrees = 0.01d;
        var cells = new Dictionary<GridCell, List<PoiGeofenceDefinition>>();
        var definitionsById = new Dictionary<string, PoiGeofenceDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            definitionsById[definition.Id] = definition;

            var effectiveRadiusMeters =
                Math.Max(definition.ActivationRadiusMeters, definition.NearRadiusMeters) +
                Math.Max(0d, options.SpatialPaddingMeters);

            var latitudePadding = effectiveRadiusMeters / 111_320d;
            var longitudePadding = effectiveRadiusMeters /
                Math.Max(1d, 111_320d * Math.Cos(definition.Latitude * Math.PI / 180d));

            var minLatitude = definition.Latitude - latitudePadding;
            var maxLatitude = definition.Latitude + latitudePadding;
            var minLongitude = definition.Longitude - longitudePadding;
            var maxLongitude = definition.Longitude + longitudePadding;

            var minCellX = (int)Math.Floor(minLatitude / cellSizeDegrees);
            var maxCellX = (int)Math.Floor(maxLatitude / cellSizeDegrees);
            var minCellY = (int)Math.Floor(minLongitude / cellSizeDegrees);
            var maxCellY = (int)Math.Floor(maxLongitude / cellSizeDegrees);

            for (var cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (var cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    var cell = new GridCell(cellX, cellY);
                    if (!cells.TryGetValue(cell, out var bucket))
                    {
                        bucket = new List<PoiGeofenceDefinition>();
                        cells[cell] = bucket;
                    }

                    bucket.Add(definition);
                }
            }
        }

        return new GeofenceSpatialIndex(cells, definitionsById, cellSizeDegrees);
    }

    public bool TryGetDefinition(string poiId, out PoiGeofenceDefinition definition) =>
        _definitionsById.TryGetValue(poiId, out definition!);

    public IReadOnlyList<PoiGeofenceDefinition> GetCandidateDefinitions(
        double latitude,
        double longitude,
        int candidateLimit)
    {
        if (_definitionsById.Count == 0)
        {
            return Array.Empty<PoiGeofenceDefinition>();
        }

        var originX = (int)Math.Floor(latitude / _cellSizeDegrees);
        var originY = (int)Math.Floor(longitude / _cellSizeDegrees);
        var matches = new Dictionary<string, PoiGeofenceDefinition>(StringComparer.OrdinalIgnoreCase);

        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                var cell = new GridCell(originX + offsetX, originY + offsetY);
                if (!_cells.TryGetValue(cell, out var bucket))
                {
                    continue;
                }

                foreach (var definition in bucket)
                {
                    matches[definition.Id] = definition;
                }
            }
        }

        if (matches.Count == 0)
        {
            return _definitionsById.Values
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => ComputeDegreeDistance(latitude, longitude, item.Latitude, item.Longitude))
                .Take(Math.Max(1, candidateLimit))
                .ToList();
        }

        return matches.Values
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => ComputeDegreeDistance(latitude, longitude, item.Latitude, item.Longitude))
            .Take(Math.Max(1, candidateLimit))
            .ToList();
    }

    private static double ComputeDegreeDistance(
        double latitudeA,
        double longitudeA,
        double latitudeB,
        double longitudeB)
    {
        var latitudeDelta = latitudeA - latitudeB;
        var longitudeDelta = longitudeA - longitudeB;
        return (latitudeDelta * latitudeDelta) + (longitudeDelta * longitudeDelta);
    }

    private readonly record struct GridCell(int X, int Y);
}
