namespace MauiApp_Mobile.Services;

public sealed class PlaceNavigationService
{
    public static PlaceNavigationService Instance { get; } = new();

    private readonly object _syncRoot = new();
    private PendingPlaceNavigationRequest? _pendingPlaceRequest;
    private string? _pendingMapPlaceId;

    private PlaceNavigationService()
    {
    }

    public void RequestPlaceDetail(string placeId) =>
        RequestPlaceDetail(placeId, autoplay: false, audioTrackId: null);

    public void RequestPlaceDetail(string placeId, bool autoplay, int? audioTrackId)
    {
        var normalizedPlaceId = NormalizePlaceId(placeId);
        lock (_syncRoot)
        {
            _pendingPlaceRequest = string.IsNullOrWhiteSpace(normalizedPlaceId)
                ? null
                : new PendingPlaceNavigationRequest(normalizedPlaceId, autoplay, audioTrackId);
        }
    }

    public PendingPlaceNavigationRequest? ConsumePendingPlaceRequest()
    {
        lock (_syncRoot)
        {
            var pendingRequest = _pendingPlaceRequest;
            _pendingPlaceRequest = null;
            return pendingRequest;
        }
    }

    public string? ConsumePendingPlaceId() =>
        ConsumePendingPlaceRequest()?.PlaceId;

    public void RequestMapFocus(string placeId)
    {
        lock (_syncRoot)
        {
            _pendingMapPlaceId = NormalizePlaceId(placeId);
        }
    }

    public string? ConsumePendingMapPlaceId()
    {
        lock (_syncRoot)
        {
            var pendingPlaceId = _pendingMapPlaceId;
            _pendingMapPlaceId = null;
            return pendingPlaceId;
        }
    }

    private static string? NormalizePlaceId(string? placeId) =>
        string.IsNullOrWhiteSpace(placeId)
            ? null
            : placeId.Trim();
}

public sealed record PendingPlaceNavigationRequest(
    string PlaceId,
    bool Autoplay,
    int? AudioTrackId);
