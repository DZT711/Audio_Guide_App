using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace MauiApp_Mobile.Services;

public sealed class UserLocationService : INotifyPropertyChanged
{
    public static UserLocationService Instance { get; } = new();

    private readonly SemaphoreSlim _headingTrackingLock = new(1, 1);
    private Location? _lastKnownLocation;
    private double? _headingDegrees;
    private bool _headingMonitoringStarted;

    private UserLocationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<Location?>? LocationUpdated;
    public event EventHandler<double?>? HeadingUpdated;

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
    public double? HeadingDegrees
    {
        get => _headingDegrees;
        private set
        {
            if (_headingDegrees == value)
            {
                return;
            }

            _headingDegrees = value;
            OnPropertyChanged();
            HeadingUpdated?.Invoke(this, value);
        }
    }

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
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15));
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

        if (location is null)
        {
            try
            {
                location = await Geolocation.Default.GetLastKnownLocationAsync();
            }
            catch (Exception ex) when (
                ex is FeatureNotEnabledException or
                FeatureNotSupportedException or
                PermissionException)
            {
            }
        }

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

    public async Task EnsureHeadingTrackingAsync(CancellationToken cancellationToken = default)
    {
        if (_headingMonitoringStarted)
        {
            return;
        }

        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permissionStatus != PermissionStatus.Granted)
        {
            Log("heading-tracking-skipped:foreground-location-not-granted");
            return;
        }

        if (!await _headingTrackingLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (_headingMonitoringStarted)
            {
                return;
            }

            Compass.Default.ReadingChanged -= OnCompassReadingChanged;
            Compass.Default.ReadingChanged += OnCompassReadingChanged;
            Compass.Default.Start(SensorSpeed.UI);
            _headingMonitoringStarted = true;
            Log("heading-tracking-started");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log($"heading-tracking-failed:{ex.Message}");
        }
        finally
        {
            _headingTrackingLock.Release();
        }
    }

    private static void Log(string message)
    {
        var payload = $"[UserLocationService] {message}";
        System.Diagnostics.Debug.WriteLine(payload);
#if ANDROID
        Android.Util.Log.Info("SmartTour.Location", payload);
#endif
    }

    private void OnCompassReadingChanged(object? sender, CompassChangedEventArgs e)
    {
        var normalized = e.Reading.HeadingMagneticNorth % 360d;
        HeadingDegrees = normalized < 0 ? normalized + 360d : normalized;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
