using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Project_SharedClassLibrary.Constants;

namespace MauiApp_Mobile;

public partial class MainPage : ContentPage
{
    // private IDispatcherTimer? _trackingTimer;
    // private bool _isRefreshing;

    // public MainPage()
    // {
    //     InitializeComponent();
    // }

    // protected override async void OnAppearing()
    // {
    //     base.OnAppearing();
    //     StartTracking();
    //     await RefreshLocationAsync(showFallbackMessage: false);
    // }

    // protected override void OnDisappearing()
    // {
    //     base.OnDisappearing();
    //     StopTracking();
    // }

    // private void StartTracking()
    // {
    //     if (_trackingTimer is not null)
    //     {
    //         return;
    //     }

    //     _trackingTimer = Dispatcher.CreateTimer();
    //     _trackingTimer.Interval = TimeSpan.FromSeconds(15);
    //     _trackingTimer.Tick += OnTrackingTimerTick;
    //     _trackingTimer.Start();
    // }

    // private void StopTracking()
    // {
    //     if (_trackingTimer is null)
    //     {
    //         return;
    //     }

    //     _trackingTimer.Stop();
    //     _trackingTimer.Tick -= OnTrackingTimerTick;
    //     _trackingTimer = null;
    // }

    // private async void OnTrackingTimerTick(object? sender, EventArgs e)
    // {
    //     await RefreshLocationAsync(showFallbackMessage: false);
    // }

    // private async void OnRefreshLocationClicked(object? sender, EventArgs e)
    // {
    //     await RefreshLocationAsync(showFallbackMessage: true);
    // }

    // private async Task RefreshLocationAsync(bool showFallbackMessage)
    // {
    //     if (_isRefreshing)
    //     {
    //         return;
    //     }

    //     _isRefreshing = true;
    //     TrackingIndicator.IsVisible = true;
    //     TrackingIndicator.IsRunning = true;
    //     RefreshLocationButton.IsEnabled = false;

    //     try
    //     {
    //         var currentLocation = await TryGetCurrentLocationAsync();
    //         if (currentLocation is null)
    //         {
    //             ApplyFallbackLocation(showFallbackMessage);
    //             return;
    //         }

    //         ApplyDeviceLocation(currentLocation);
    //     }
    //     finally
    //     {
    //         TrackingIndicator.IsRunning = false;
    //         TrackingIndicator.IsVisible = false;
    //         RefreshLocationButton.IsEnabled = true;
    //         _isRefreshing = false;
    //     }
    // }

    // private static async Task<Location?> TryGetCurrentLocationAsync()
    // {
    //     try
    //     {
    //         var permissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
    //         if (permissionStatus != PermissionStatus.Granted)
    //         {
    //             permissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    //         }

    //         if (permissionStatus != PermissionStatus.Granted)
    //         {
    //             return null;
    //         }

    //         var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
    //         return await Geolocation.Default.GetLocationAsync(request);
    //     }
    //     catch (Exception)
    //     {
    //         return null;
    //     }
    // }

    // private void ApplyDeviceLocation(Location location)
    // {
    //     StatusCard.BackgroundColor = Color.FromArgb("#EEF9F0");
    //     StatusCard.Stroke = new SolidColorBrush(Color.FromArgb("#B7E0C0"));
    //     PositionStatusLabel.Text = "Tracking your live device position";
    //     PositionDetailLabel.Text = "The current coordinates were pulled from the device and will refresh automatically while this page is open.";
    //     PositionSourceValue.Text = "Device location";
    //     PositionAreaValue.Text = "Current position";
    //     LatitudeValue.Text = location.Latitude.ToString("F6");
    //     LongitudeValue.Text = location.Longitude.ToString("F6");
    //     LastUpdatedValue.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    // }

    // private void ApplyFallbackLocation(bool showFallbackMessage)
    // {
    //     StatusCard.BackgroundColor = Color.FromArgb("#FFF6E8");
    //     StatusCard.Stroke = new SolidColorBrush(Color.FromArgb("#E6C98E"));
    //     PositionStatusLabel.Text = "Using the default District 4 pin";
    //     PositionDetailLabel.Text = showFallbackMessage
    //         ? "Location access is unavailable right now, so the app is staying on the shared District 4 fallback coordinates."
    //         : "The app will move to your live device location when permission is available. Until then it stays on the shared District 4 fallback coordinates.";
    //     PositionSourceValue.Text = "Shared fallback";
    //     PositionAreaValue.Text = LocationDefaults.District4Label;
    //     LatitudeValue.Text = LocationDefaults.District4Latitude.ToString("F6");
    //     LongitudeValue.Text = LocationDefaults.District4Longitude.ToString("F6");
    //     LastUpdatedValue.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    // }
}
