using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;

namespace MauiApp_Mobile.Services;

public sealed class LocationTrackingService : INotifyPropertyChanged
{
    private readonly SemaphoreSlim _trackingLock = new(1, 1);
    private CancellationTokenSource? _trackingCts;
    private bool _isTracking;
    private Location? _lastKnownLocation;
    private string _deviceId = string.Empty;
    private string _sessionId = Guid.NewGuid().ToString("N");

    public static LocationTrackingService Instance { get; } = new();

    private LocationTrackingService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LocationSample>? LocationUpdated;

    public bool IsTracking
    {
        get => _isTracking;
        private set
        {
            if (_isTracking == value)
            {
                return;
            }

            _isTracking = value;
            OnPropertyChanged();
        }
    }

    public Location? LastKnownLocation
    {
        get => _lastKnownLocation;
        private set
        {
            _lastKnownLocation = value;
            OnPropertyChanged();
        }
    }

    public string DeviceId => _deviceId;
    public string SessionId => _sessionId;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await MobileDatabaseService.Instance.InitializeAsync(cancellationToken);

        _deviceId = await MobileDatabaseService.Instance.GetSettingAsync("device.id", cancellationToken) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_deviceId))
        {
            _deviceId = Guid.NewGuid().ToString("N");
            await MobileDatabaseService.Instance.SetSettingAsync("device.id", _deviceId, cancellationToken);
        }

        _sessionId = Guid.NewGuid().ToString("N");
    }

    public async Task<PermissionStatus> EnsureLocationPermissionsAsync(bool requestBackgroundIfEnabled, CancellationToken cancellationToken = default)
    {
        var whenInUseStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (whenInUseStatus != PermissionStatus.Granted)
        {
            whenInUseStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (whenInUseStatus != PermissionStatus.Granted || !requestBackgroundIfEnabled || !AppSettingsService.Instance.BackgroundTrackingEnabled)
        {
            return whenInUseStatus;
        }

        try
        {
            var alwaysStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (alwaysStatus != PermissionStatus.Granted)
            {
                alwaysStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
            }

            return alwaysStatus == PermissionStatus.Granted ? alwaysStatus : whenInUseStatus;
        }
        catch
        {
            return whenInUseStatus;
        }
    }

    public Task<PermissionStatus> GetForegroundPermissionStatusAsync() =>
        Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

    public Task<PermissionStatus> GetBackgroundPermissionStatusAsync() =>
        Permissions.CheckStatusAsync<Permissions.LocationAlways>();

    public async Task<Location?> GetCurrentLocationAsync(bool forForegroundMap = true, CancellationToken cancellationToken = default)
    {
        var foregroundStatus = await GetForegroundPermissionStatusAsync();
        if (foregroundStatus != PermissionStatus.Granted)
        {
            return LastKnownLocation;
        }

        var request = CreateRequest();

        try
        {
            var currentLocation = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
            if (currentLocation is not null)
            {
                await PublishLocationAsync(currentLocation, isForeground: forForegroundMap, cancellationToken);
                return currentLocation;
            }
        }
        catch (Exception ex) when (ex is TaskCanceledException or TimeoutException)
        {
        }

        Location? lastKnown = null;
        try
        {
            lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
        }
        catch (Exception ex) when (
            ex is FeatureNotEnabledException or
            FeatureNotSupportedException or
            PermissionException)
        {
        }

        if (lastKnown is not null)
        {
            await PublishLocationAsync(lastKnown, isForeground: forForegroundMap, cancellationToken);
        }

        return lastKnown;
    }

    public async Task StartForegroundTrackingAsync(CancellationToken cancellationToken = default)
    {
#if ANDROID
        AndroidLocationForegroundServiceManager.Stop();
#endif
        var foregroundStatus = await GetForegroundPermissionStatusAsync();
        if (foregroundStatus != PermissionStatus.Granted)
        {
            return;
        }

        await StartPollingAsync(isForeground: true, cancellationToken);
    }

    public async Task StartBackgroundTrackingAsync(CancellationToken cancellationToken = default)
    {
        var foregroundStatus = await GetForegroundPermissionStatusAsync();
        var backgroundStatus = await GetBackgroundPermissionStatusAsync();
        if (foregroundStatus != PermissionStatus.Granted || backgroundStatus != PermissionStatus.Granted)
        {
            return;
        }

#if ANDROID
        AndroidLocationForegroundServiceManager.Start();
#endif
        await StartPollingAsync(isForeground: false, cancellationToken);
    }

    public async Task StartTrackingFromSettingsAsync(bool requestBackgroundUpgrade = false, CancellationToken cancellationToken = default)
    {
        if (AppSettingsService.Instance.BatterySaverEnabled || !AppSettingsService.Instance.BackgroundTrackingEnabled)
        {
            await StartForegroundTrackingAsync(cancellationToken);
            return;
        }

        if (requestBackgroundUpgrade)
        {
            await EnsureLocationPermissionsAsync(requestBackgroundIfEnabled: true, cancellationToken);
        }

        var backgroundStatus = await GetBackgroundPermissionStatusAsync();
        if (backgroundStatus == PermissionStatus.Granted)
        {
            await StartBackgroundTrackingAsync(cancellationToken);
            return;
        }

        await StartForegroundTrackingAsync(cancellationToken);
    }

    public Task StopAsync()
    {
        _trackingCts?.Cancel();
        _trackingCts = null;
        IsTracking = false;
#if ANDROID
        AndroidLocationForegroundServiceManager.Stop();
#endif
        return Task.CompletedTask;
    }

    public async Task RunSingleBackgroundTickAsync(CancellationToken cancellationToken = default)
    {
        await GetCurrentLocationAsync(forForegroundMap: false, cancellationToken);
    }

    public TimeSpan GetRecommendedTrackingInterval() => ResolveTrackingInterval();

    private async Task StartPollingAsync(bool isForeground, CancellationToken cancellationToken)
    {
        if (!await _trackingLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            _trackingCts?.Cancel();
            _trackingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var localToken = _trackingCts.Token;
            IsTracking = true;

            _ = Task.Run(async () =>
            {
                while (!localToken.IsCancellationRequested)
                {
                    try
                    {
                        await GetCurrentLocationAsync(isForeground, localToken);
                    }
                    catch
                    {
                    }

                    var delay = ResolveTrackingInterval();
                    try
                    {
                        await Task.Delay(delay, localToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                IsTracking = false;
            }, localToken);
        }
        finally
        {
            _trackingLock.Release();
        }
    }

    private async Task PublishLocationAsync(Location location, bool isForeground, CancellationToken cancellationToken)
    {
        LastKnownLocation = location;
        UserLocationService.Instance.UpdateLocation(location);

        var battery = Battery.Default.ChargeLevel;
        var batteryPercent = battery > 0 ? (int)Math.Round(battery * 100d) : (int?)null;
        var sample = new LocationSample(
            location,
            isForeground,
            DateTimeOffset.UtcNow,
            ResolveTrackingInterval());

        await MobileDatabaseService.Instance.LogTrackingEventAsync(new LocationTrackingRecord(
            _deviceId,
            _sessionId,
            location.Latitude,
            location.Longitude,
            location.Accuracy,
            location.Speed,
            batteryPercent,
            isForeground,
            sample.CapturedAtUtc), cancellationToken);

        LocationUpdated?.Invoke(this, sample);
    }

    private GeolocationRequest CreateRequest()
    {
        var accuracy = AppSettingsService.Instance.BatterySaverEnabled
            ? GeolocationAccuracy.Low
            : AppSettingsService.Instance.GpsAccuracy switch
            {
                GpsAccuracyOption.VeryLow => GeolocationAccuracy.Low,
                GpsAccuracyOption.Low => GeolocationAccuracy.Medium,
                GpsAccuracyOption.Medium => GeolocationAccuracy.Default,
                GpsAccuracyOption.VeryHigh => GeolocationAccuracy.Best,
                _ => GeolocationAccuracy.High
            };

        return new GeolocationRequest(accuracy, TimeSpan.FromSeconds(15));
    }

    private TimeSpan ResolveTrackingInterval()
    {
        if (AppSettingsService.Instance.BatterySaverEnabled)
        {
            return TimeSpan.FromSeconds(60);
        }

        if (AppSettingsService.Instance.BackgroundTrackingEnabled)
        {
            return AppSettingsService.Instance.GpsAccuracy switch
            {
                GpsAccuracyOption.VeryHigh => TimeSpan.FromSeconds(8),
                GpsAccuracyOption.High => TimeSpan.FromSeconds(12),
                GpsAccuracyOption.Medium => TimeSpan.FromSeconds(18),
                GpsAccuracyOption.Low => TimeSpan.FromSeconds(26),
                _ => TimeSpan.FromSeconds(35)
            };
        }

        return AppSettingsService.Instance.GpsAccuracy switch
        {
            GpsAccuracyOption.VeryHigh => TimeSpan.FromSeconds(10),
            GpsAccuracyOption.High => TimeSpan.FromSeconds(15),
            GpsAccuracyOption.Medium => TimeSpan.FromSeconds(22),
            GpsAccuracyOption.Low => TimeSpan.FromSeconds(30),
            _ => TimeSpan.FromSeconds(40)
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record LocationSample(
    Location Location,
    bool IsForeground,
    DateTimeOffset CapturedAtUtc,
    TimeSpan RecommendedNextPollInterval);
