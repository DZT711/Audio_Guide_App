using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofencePlatformMonitor : IGeofencePlatformMonitor
{
    public static GeofencePlatformMonitor Instance { get; } = new();

    private GeofencePlatformMonitor()
    {
    }

    public event EventHandler<NativeGeofenceTransition>? TransitionReceived;

    public bool IsSupported => GetPlatformSupport();

    public Task<NativeGeofenceRegistrationResult> RegisterAsync(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        CancellationToken cancellationToken = default) =>
        RegisterPlatformAsync(definitions, cancellationToken);

    public Task UnregisterAllAsync(CancellationToken cancellationToken = default) =>
        UnregisterPlatformAsync(cancellationToken);

    internal void PublishTransition(NativeGeofenceTransition transition) =>
        TransitionReceived?.Invoke(this, transition);

    private partial bool GetPlatformSupport();
    private partial Task<NativeGeofenceRegistrationResult> RegisterPlatformAsync(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        CancellationToken cancellationToken);
    private partial Task UnregisterPlatformAsync(CancellationToken cancellationToken);
}
