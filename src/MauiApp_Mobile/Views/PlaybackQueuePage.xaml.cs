using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class PlaybackQueuePage : ContentPage
{
    private CancellationTokenSource? _volumeApplyCts;

    public PlaybackQueuePage()
    {
        InitializeComponent();
        BindingContext = PlaybackCoordinatorService.Instance;
        SyncVolumeUi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AppSettingsService.Instance.SettingsSaved += OnSettingsSaved;
        SyncVolumeUi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        AppSettingsService.Instance.SettingsSaved -= OnSettingsSaved;
        _volumeApplyCts?.Cancel();
        _volumeApplyCts?.Dispose();
        _volumeApplyCts = null;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Playback queue back navigation error: {ex}");
        }
    }

    private void OnRemoveTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not PlaybackQueueItem item)
        {
            return;
        }

        PlaybackCoordinatorService.Instance.RemoveQueueItem(item);
    }

    private void OnClearTapped(object? sender, TappedEventArgs e) =>
        PlaybackCoordinatorService.Instance.ClearQueuedUpcoming();

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        VolumeValueLabel.Text = $"{Math.Round(e.NewValue):0}%";
        QueueApplyVolumeDebounced(e.NewValue);
    }

    private async void OnVolumeDragCompleted(object? sender, EventArgs e) =>
        await ApplyVolumeAsync(VolumeSlider.Value);

    private void SyncVolumeUi()
    {
        var volume = AppSettingsService.Instance.VolumePercent;
        VolumeSlider.Value = volume;
        VolumeValueLabel.Text = $"{Math.Round(volume):0}%";
    }

    private void OnSettingsSaved(object? sender, AppSettingsSnapshot snapshot) =>
        MainThread.BeginInvokeOnMainThread(SyncVolumeUi);

    private void QueueApplyVolumeDebounced(double volumePercent)
    {
        _volumeApplyCts?.Cancel();
        _volumeApplyCts?.Dispose();
        _volumeApplyCts = new CancellationTokenSource();
        var token = _volumeApplyCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(180, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await ApplyVolumeAsync(volumePercent);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private static async Task ApplyVolumeAsync(double volumePercent)
    {
        var snapshot = AppSettingsService.Instance.CreateSnapshot() with
        {
            VolumePercent = volumePercent
        };
        await AppSettingsService.Instance.SaveAsync(snapshot);
        await AudioPlaybackService.Instance.ApplyRuntimeVolumeAsync();
    }
}
