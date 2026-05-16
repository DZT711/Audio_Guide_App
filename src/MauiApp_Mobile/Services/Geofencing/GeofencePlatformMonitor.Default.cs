#if !ANDROID && !IOS
using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofencePlatformMonitor
{
    private partial bool GetPlatformSupport() => false;

    private partial Task<NativeGeofenceRegistrationResult> RegisterPlatformAsync(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        CancellationToken cancellationToken) =>
        Task.FromResult(new NativeGeofenceRegistrationResult(
            IsSuccessful: false,
            RegisteredCount: 0,
            Mode: "unsupported",
            FailureReason: "platform-monitoring-unavailable"));

    private partial Task UnregisterPlatformAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
#endif
