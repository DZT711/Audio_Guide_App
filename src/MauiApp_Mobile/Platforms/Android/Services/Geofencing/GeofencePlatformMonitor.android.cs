#if ANDROID
using Android.App;
using Android.Content;
using Android.Locations;
using AndroidAppApplication = Android.App.Application;
using AndroidUri = Android.Net.Uri;
using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;
using Project_SharedClassLibrary.Geofencing;
using SystemUri = System.Uri;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofencePlatformMonitor
{
    private readonly Dictionary<string, PendingIntent> _androidPendingIntents = new(StringComparer.OrdinalIgnoreCase);

    private partial bool GetPlatformSupport()
    {
        try
        {
            var context = (Context?)Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? AndroidAppApplication.Context;
            return context?.GetSystemService(Context.LocationService) is LocationManager;
        }
        catch (Exception ex)
        {
            LogMonitor($"support-check-failed:{ex.Message}");
            return false;
        }
    }

    private partial async Task<NativeGeofenceRegistrationResult> RegisterPlatformAsync(
        IReadOnlyList<PoiGeofenceDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var foregroundPermission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (foregroundPermission != PermissionStatus.Granted)
        {
            LogMonitor("register-skipped:foreground-location-not-granted");
            return new NativeGeofenceRegistrationResult(false, 0, "android-proximity", "foreground-location-not-granted");
        }

        if (AppSettingsService.Instance.BackgroundTrackingEnabled)
        {
            var backgroundPermission = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (backgroundPermission != PermissionStatus.Granted)
            {
                LogMonitor("register-skipped:background-location-not-granted");
                return new NativeGeofenceRegistrationResult(false, 0, "android-proximity", "background-location-not-granted");
            }
        }

        return await Task.Run(() =>
        {
            var context = (Context?)Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? AndroidAppApplication.Context;
            var locationManager = (LocationManager?)context.GetSystemService(Context.LocationService);
            if (locationManager is null)
            {
                LogMonitor("register-failed:location-manager-unavailable");
                return new NativeGeofenceRegistrationResult(false, 0, "android-proximity", "location-manager-unavailable");
            }

            UnregisterAndroidIntents(locationManager);

#pragma warning disable CS0618
            var registeredCount = 0;
            foreach (var definition in definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var intent = new Intent(context, typeof(AndroidGeofenceTransitionReceiver));
                intent.SetAction(AndroidGeofenceTransitionReceiver.ActionGeofenceTransition);
                intent.SetData(SystemUri.TryCreate(
                    $"smarttour://geofence/{definition.Id}",
                    UriKind.Absolute,
                    out var geofenceUri)
                    ? AndroidUri.Parse(geofenceUri.AbsoluteUri)
                    : null);
                intent.PutExtra(AndroidGeofenceTransitionReceiver.ExtraPoiId, definition.Id);

                var pendingIntent = PendingIntent.GetBroadcast(
                    context,
                    ComputeStableRequestCode(definition.Id),
                    intent,
                    PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

                locationManager.AddProximityAlert(
                    definition.Latitude,
                    definition.Longitude,
                    (float)Math.Max(10d, definition.ActivationRadiusMeters),
                    -1,
                    pendingIntent);

                _androidPendingIntents[definition.Id] = pendingIntent;
                registeredCount++;
            }
#pragma warning restore CS0618

            LogMonitor($"register-complete:count={registeredCount}");
            return new NativeGeofenceRegistrationResult(true, registeredCount, "android-proximity");
        }, cancellationToken);
    }

    private partial Task UnregisterPlatformAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var context = (Context?)Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? AndroidAppApplication.Context;
            var locationManager = (LocationManager?)context.GetSystemService(Context.LocationService);
            if (locationManager is not null)
            {
                UnregisterAndroidIntents(locationManager);
            }
        }, cancellationToken);
    }

    private void UnregisterAndroidIntents(LocationManager locationManager)
    {
        foreach (var pendingIntent in _androidPendingIntents.Values)
        {
            try
            {
                locationManager.RemoveProximityAlert(pendingIntent);
                pendingIntent.Cancel();
            }
            catch
            {
            }
        }

        _androidPendingIntents.Clear();
    }

    private static int ComputeStableRequestCode(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }

    private static void LogMonitor(string message)
    {
        var payload = $"[GeofencePlatformMonitor] {message}";
        System.Diagnostics.Debug.WriteLine(payload);
        Android.Util.Log.Info("SmartTour.Geofence", payload);
    }
}
#endif
