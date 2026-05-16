#if IOS
using CoreLocation;
using Foundation;
using Microsoft.Maui.ApplicationModel;
using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofencePlatformMonitor
{
    private readonly Dictionary<string, CLCircularRegion> _iosRegions = new(StringComparer.OrdinalIgnoreCase);
    private CLLocationManager? _iosLocationManager;
    private GeofenceLocationManagerDelegate? _iosDelegate;

    private partial bool GetPlatformSupport() => CLLocationManager.IsMonitoringAvailable(typeof(CLCircularRegion));

    private partial Task<NativeGeofenceRegistrationResult> RegisterPlatformAsync(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        CancellationToken cancellationToken)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!GetPlatformSupport())
            {
                return Task.FromResult(new NativeGeofenceRegistrationResult(false, 0, "ios-region", "region-monitoring-unavailable"));
            }

            _iosLocationManager ??= new CLLocationManager();
            _iosDelegate ??= new GeofenceLocationManagerDelegate(this);
            _iosLocationManager.Delegate = _iosDelegate;
            UnregisterIosRegions();

            var registeredCount = 0;
            foreach (var definition in definitions)
            {
                var region = new CLCircularRegion(
                    new CLLocationCoordinate2D(definition.Latitude, definition.Longitude),
                    Math.Max(10d, definition.ActivationRadiusMeters),
                    definition.Id)
                {
                    NotifyOnEntry = true,
                    NotifyOnExit = true
                };

                _iosLocationManager.StartMonitoring(region);
                _iosRegions[definition.Id] = region;
                registeredCount++;
            }

            return Task.FromResult(new NativeGeofenceRegistrationResult(true, registeredCount, "ios-region"));
        }).Unwrap();
    }

    private partial Task UnregisterPlatformAsync(CancellationToken cancellationToken)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            UnregisterIosRegions();
            return Task.CompletedTask;
        }).Unwrap();
    }

    private void UnregisterIosRegions()
    {
        if (_iosLocationManager is null)
        {
            return;
        }

        foreach (var region in _iosRegions.Values)
        {
            try
            {
                _iosLocationManager.StopMonitoring(region);
            }
            catch
            {
            }
        }

        _iosRegions.Clear();
    }

    private sealed class GeofenceLocationManagerDelegate : CLLocationManagerDelegate
    {
        private readonly GeofencePlatformMonitor _owner;

        public GeofenceLocationManagerDelegate(GeofencePlatformMonitor owner)
        {
            _owner = owner;
        }

        public override void DidEnterRegion(CLLocationManager manager, CLRegion region)
        {
            if (string.IsNullOrWhiteSpace(region.Identifier))
            {
                return;
            }

            _owner.PublishTransition(new NativeGeofenceTransition(
                region.Identifier,
                GeofenceTriggerEvent.EnteredRadius,
                DateTimeOffset.UtcNow));
        }

        public override void DidExitRegion(CLLocationManager manager, CLRegion region)
        {
            if (string.IsNullOrWhiteSpace(region.Identifier))
            {
                return;
            }

            _owner.PublishTransition(new NativeGeofenceTransition(
                region.Identifier,
                GeofenceTriggerEvent.ExitedRadius,
                DateTimeOffset.UtcNow));
        }
    }
}
#endif
