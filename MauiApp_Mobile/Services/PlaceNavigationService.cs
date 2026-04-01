namespace MauiApp_Mobile.Services;

public sealed class PlaceNavigationService
{
    public static PlaceNavigationService Instance { get; } = new();

    private string? _pendingPlaceId;

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
}
