namespace MauiApp_Mobile.Services;

public sealed class PlaceNavigationService
{
    public static PlaceNavigationService Instance { get; } = new();

    private string? _pendingPlaceId;
    private string? _pendingMapPlaceId;

    private PlaceNavigationService()
    {
    }

    public void RequestPlaceDetail(string placeId)
    {
        _pendingPlaceId = string.IsNullOrWhiteSpace(placeId) ? null : placeId.Trim();
    }

    public string? ConsumePendingPlaceId()
    {
        var pendingPlaceId = _pendingPlaceId;
        _pendingPlaceId = null;
        return pendingPlaceId;
    }

    public void RequestMapFocus(string placeId)
    {
        _pendingMapPlaceId = string.IsNullOrWhiteSpace(placeId) ? null : placeId.Trim();
    }

    public string? ConsumePendingMapPlaceId()
    {
        var pendingPlaceId = _pendingMapPlaceId;
        _pendingMapPlaceId = null;
        return pendingPlaceId;
    }
}
