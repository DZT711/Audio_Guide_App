using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;

namespace MauiApp_Mobile.Services;

public sealed class AppSettingsService : INotifyPropertyChanged
{
    private const string ReadingSpeedKey = "app_settings.reading_speed";
    private const string VolumePercentKey = "app_settings.volume_percent";
    private const string TriggerRadiusKey = "app_settings.trigger_radius";
    private const string AlertRadiusKey = "app_settings.alert_radius";
    private const string WaitTimeKey = "app_settings.wait_time";
    private const string AutoPlayKey = "app_settings.auto_play";
    private const string NotifyNearKey = "app_settings.notify_near";
    private const string BackgroundTrackingKey = "app_settings.background_tracking";
    private const string BatterySaverKey = "app_settings.battery_saver";

    public static AppSettingsService Instance { get; } = new();

    private AppSettingsService()
    {
        ReadingSpeed = Preferences.Default.Get(ReadingSpeedKey, 1.0d);
        VolumePercent = Preferences.Default.Get(VolumePercentKey, 100d);
        TriggerRadiusMeters = Preferences.Default.Get(TriggerRadiusKey, 50d);
        AlertRadiusMeters = Preferences.Default.Get(AlertRadiusKey, 100d);
        WaitTimeSeconds = Preferences.Default.Get(WaitTimeKey, 300d);
        AutoPlayEnabled = Preferences.Default.Get(AutoPlayKey, true);
        NotifyNearEnabled = Preferences.Default.Get(NotifyNearKey, true);
        BackgroundTrackingEnabled = Preferences.Default.Get(BackgroundTrackingKey, true);
        BatterySaverEnabled = Preferences.Default.Get(BatterySaverKey, false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double ReadingSpeed { get; private set; }

    public double VolumePercent { get; private set; }

    public double TriggerRadiusMeters { get; private set; }

    public double AlertRadiusMeters { get; private set; }

    public double WaitTimeSeconds { get; private set; }

    public bool AutoPlayEnabled { get; private set; }

    public bool NotifyNearEnabled { get; private set; }

    public bool BackgroundTrackingEnabled { get; private set; }

    public bool BatterySaverEnabled { get; private set; }

    public float PlaybackVolumeRatio => (float)Math.Clamp(VolumePercent / 100d, 0d, 1d);

    public float AndroidSpeechRate => (float)Math.Clamp(ReadingSpeed, 0.5d, 2.0d);

    public AppSettingsSnapshot CreateSnapshot() => new(
        ReadingSpeed,
        VolumePercent,
        TriggerRadiusMeters,
        AlertRadiusMeters,
        WaitTimeSeconds,
        AutoPlayEnabled,
        NotifyNearEnabled,
        BackgroundTrackingEnabled,
        BatterySaverEnabled);

    public void Save(AppSettingsSnapshot snapshot)
    {
        ReadingSpeed = Clamp(snapshot.ReadingSpeed, 0.5d, 2.0d);
        VolumePercent = Clamp(snapshot.VolumePercent, 0d, 100d);
        TriggerRadiusMeters = Clamp(snapshot.TriggerRadiusMeters, 10d, 100d);
        AlertRadiusMeters = Clamp(snapshot.AlertRadiusMeters, 20d, 200d);
        WaitTimeSeconds = Clamp(snapshot.WaitTimeSeconds, 60d, 600d);
        AutoPlayEnabled = snapshot.AutoPlayEnabled;
        NotifyNearEnabled = snapshot.NotifyNearEnabled;
        BackgroundTrackingEnabled = snapshot.BackgroundTrackingEnabled;
        BatterySaverEnabled = snapshot.BatterySaverEnabled;

        Preferences.Default.Set(ReadingSpeedKey, ReadingSpeed);
        Preferences.Default.Set(VolumePercentKey, VolumePercent);
        Preferences.Default.Set(TriggerRadiusKey, TriggerRadiusMeters);
        Preferences.Default.Set(AlertRadiusKey, AlertRadiusMeters);
        Preferences.Default.Set(WaitTimeKey, WaitTimeSeconds);
        Preferences.Default.Set(AutoPlayKey, AutoPlayEnabled);
        Preferences.Default.Set(NotifyNearKey, NotifyNearEnabled);
        Preferences.Default.Set(BackgroundTrackingKey, BackgroundTrackingEnabled);
        Preferences.Default.Set(BatterySaverKey, BatterySaverEnabled);

        RaisePropertyChanged(nameof(ReadingSpeed));
        RaisePropertyChanged(nameof(VolumePercent));
        RaisePropertyChanged(nameof(TriggerRadiusMeters));
        RaisePropertyChanged(nameof(AlertRadiusMeters));
        RaisePropertyChanged(nameof(WaitTimeSeconds));
        RaisePropertyChanged(nameof(AutoPlayEnabled));
        RaisePropertyChanged(nameof(NotifyNearEnabled));
        RaisePropertyChanged(nameof(BackgroundTrackingEnabled));
        RaisePropertyChanged(nameof(BatterySaverEnabled));
        RaisePropertyChanged(nameof(PlaybackVolumeRatio));
        RaisePropertyChanged(nameof(AndroidSpeechRate));
    }

    private static double Clamp(double value, double min, double max) => Math.Clamp(value, min, max);

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public readonly record struct AppSettingsSnapshot(
    double ReadingSpeed,
    double VolumePercent,
    double TriggerRadiusMeters,
    double AlertRadiusMeters,
    double WaitTimeSeconds,
    bool AutoPlayEnabled,
    bool NotifyNearEnabled,
    bool BackgroundTrackingEnabled,
    bool BatterySaverEnabled);
