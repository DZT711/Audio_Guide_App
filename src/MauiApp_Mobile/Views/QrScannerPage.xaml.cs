using System.ComponentModel;
using System.Threading;
using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace MauiApp_Mobile.Views;

public partial class QrScannerPage : ContentPage
{
    private int _isProcessingScan;
    private bool _hasCameraAccess;
    private bool _subscriptionsAttached;

    public QrScannerPage()
    {
        InitializeComponent();
        CameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
        CameraView.CameraLocation = CameraLocation.Rear;
        ApplyTexts();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AttachSubscriptions();
        ApplyTexts();

#if ANDROID
        await EnsureCameraReadyAsync();
#else
        SetCameraUnavailable();
#endif
    }

    protected override void OnDisappearing()
    {
        CameraView.IsDetecting = false;
        DetachSubscriptions();
        base.OnDisappearing();
    }

    private void AttachSubscriptions()
    {
        if (_subscriptionsAttached)
        {
            return;
        }

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        _subscriptionsAttached = true;
    }

    private void DetachSubscriptions()
    {
        if (!_subscriptionsAttached)
        {
            return;
        }

        LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;
        _subscriptionsAttached = false;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(ApplyTexts);

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("QrScanner.Title");
        SubtitleLabel.Text = LocalizationService.Instance.T("QrScanner.Subtitle");
        FallbackTitleLabel.Text = LocalizationService.Instance.T("QrScanner.CameraUnavailable");
        FallbackMessageLabel.Text = LocalizationService.Instance.T("QrScanner.CameraUnavailableMessage");
        HintLabel.Text = LocalizationService.Instance.T("QrScanner.Hint");

        if (FallbackPanel.IsVisible)
        {
            StatusLabel.Text = LocalizationService.Instance.T("QrScanner.CameraUnavailable");
            return;
        }

        if (Interlocked.CompareExchange(ref _isProcessingScan, 0, 0) == 1)
        {
            StatusLabel.Text = LocalizationService.Instance.T("QrScanner.Processing");
            return;
        }

        StatusLabel.Text = LocalizationService.Instance.T("QrScanner.Ready");
    }

    private async Task EnsureCameraReadyAsync()
    {
        try
        {
            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permissionStatus != PermissionStatus.Granted)
            {
                permissionStatus = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (permissionStatus != PermissionStatus.Granted)
            {
                _hasCameraAccess = false;
                SetCameraUnavailable();

                var openSettings = await DisplayAlertAsync(
                    LocalizationService.Instance.T("QrScanner.PermissionTitle"),
                    LocalizationService.Instance.T("QrScanner.PermissionMessage"),
                    LocalizationService.Instance.T("QrScanner.OpenSettings"),
                    LocalizationService.Instance.T("QrScanner.Cancel"));

                if (openSettings)
                {
                    AppInfo.ShowSettingsUI();
                }

                return;
            }

            _hasCameraAccess = true;
            CameraView.IsVisible = true;
            CameraView.IsDetecting = true;
            CameraOverlay.IsVisible = true;
            FallbackPanel.IsVisible = false;
            StatusLabel.Text = LocalizationService.Instance.T("QrScanner.Ready");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrScanner] Camera initialization failed: {ex}");
            _hasCameraAccess = false;
            SetCameraUnavailable();
        }
    }

    private void SetCameraUnavailable()
    {
        CameraView.IsDetecting = false;
        CameraView.IsVisible = false;
        CameraOverlay.IsVisible = false;
        FallbackPanel.IsVisible = true;
        StatusLabel.Text = LocalizationService.Instance.T("QrScanner.CameraUnavailable");
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (!_hasCameraAccess || Interlocked.CompareExchange(ref _isProcessingScan, 1, 0) == 1)
        {
            return;
        }

        var shouldResumeDetection = true;

        try
        {
            var rawValue = e.Results?.FirstOrDefault()?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CameraView.IsDetecting = false;
                StatusLabel.Text = LocalizationService.Instance.T("QrScanner.Processing");
            });

            var deepLink = QrDeepLinkService.Instance.ParseDeepLink(rawValue);
            if (deepLink is null)
            {
                return;
            }

            var handleStatus = await QrDeepLinkService.Instance.HandleDeepLinkAsync(deepLink);
            if (handleStatus == DeepLinkHandleStatus.Handled)
            {
                shouldResumeDetection = false;
                await CloseScannerAsync();
                return;
            }

            var failureText = deepLink.IsValid
                ? LocalizationService.Instance.T("QrScanner.OpenFailed")
                : LocalizationService.Instance.T("QrScanner.InvalidCode");

            await MainThread.InvokeOnMainThreadAsync(() => StatusLabel.Text = failureText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrScanner] Scan handling failed: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
                StatusLabel.Text = LocalizationService.Instance.T("QrScanner.OpenFailed"));
        }
        finally
        {
            if (shouldResumeDetection && _hasCameraAccess)
            {
                await Task.Delay(900);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CameraView.IsDetecting = true;
                    StatusLabel.Text = LocalizationService.Instance.T("QrScanner.Ready");
                });
            }

            Interlocked.Exchange(ref _isProcessingScan, 0);
        }
    }

    private async void OnCancelTapped(object? sender, TappedEventArgs e)
    {
        await CloseScannerAsync();
    }

    private async Task CloseScannerAsync()
    {
        CameraView.IsDetecting = false;

        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
        }
    }
}
