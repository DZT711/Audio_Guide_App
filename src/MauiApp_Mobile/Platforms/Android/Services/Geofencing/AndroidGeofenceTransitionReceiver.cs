#if ANDROID
using Android.Content;
using Android.Locations;
using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AndroidGeofenceTransitionReceiver : BroadcastReceiver
{
    internal const string ActionGeofenceTransition = "smarttour.geofence.transition";
    internal const string ExtraPoiId = "smarttour.geofence.poi_id";

    public override void OnReceive(Context? context, Intent? intent)
    {
        var poiId = intent?.GetStringExtra(ExtraPoiId);
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return;
        }

        var isEntering = intent?.GetBooleanExtra(LocationManager.KeyProximityEntering, false) ?? false;
        GeofencePlatformMonitor.Instance.PublishTransition(new NativeGeofenceTransition(
            poiId,
            isEntering ? GeofenceTriggerEvent.EnteredRadius : GeofenceTriggerEvent.ExitedRadius,
            DateTimeOffset.UtcNow));
    }
}
#endif
