using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace MauiApp_Mobile.Services;

public sealed class UserLocationService : INotifyPropertyChanged
{
    public static UserLocationService Instance { get; } = new();

    private Location? _lastKnownLocation;

    private UserLocationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<Location?>? LocationUpdated;

    public Location? LastKnownLocation
    {
        get => _lastKnownLocation;
        private set
        {
            _lastKnownLocation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLocation));
        }
    }

    public bool HasLocation => LastKnownLocation is not null;

    public async Task<Location?> RefreshLocationAsync(
        bool requestPermission = false,
        CancellationToken cancellationToken = default)
    {
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permissionStatus != PermissionStatus.Granted)
        {
            if (!requestPermission)
            {
                return LastKnownLocation;
            }

            permissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (permissionStatus != PermissionStatus.Granted)
        {
            return LastKnownLocation;
        }

        Location? location = null;

        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(12));
            location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
        }
        catch (Exception ex) when (
            ex is TaskCanceledException or
            TimeoutException or
            FeatureNotEnabledException or
            FeatureNotSupportedException or
            PermissionException)
        {
        }

        location ??= await Geolocation.Default.GetLastKnownLocationAsync();
        if (location is not null)
        {
            UpdateLocation(location);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return LastKnownLocation;
    }

    public void UpdateLocation(Location location)
    {
        LastKnownLocation = location;
        LocationUpdated?.Invoke(this, location);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
