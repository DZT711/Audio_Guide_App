using System.ComponentModel;
using System.Globalization;
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
    private const string LanguageKey = "app_settings.language";
    private const string ThemeKey = "app_settings.theme";
    private const string ApiModeKey = "app_settings.api_mode";
    private const string DeveloperModeKey = "app_settings.developer_mode";
    private const string InitializationMarkerKey = "app_settings.initialized";

    public static AppSettingsService Instance { get; } = new();

    private AppSettingsService()
    {
        ApplySnapshot(GetLegacySnapshot(), persistToLegacyPreferences: false);
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
    public string LanguageCode { get; private set; } = "vi";
    public AppThemeOption Theme { get; private set; } = AppThemeOption.Eco;
    public bool ApiModeEnabled { get; private set; } = true;
    public bool DeveloperModeEnabled { get; private set; }
    public float PlaybackVolumeRatio => (float)Math.Clamp(VolumePercent / 100d, 0d, 1d);
    public float AndroidSpeechRate => (float)Math.Clamp(ReadingSpeed, 0.5d, 2.0d);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await MobileDatabaseService.Instance.InitializeAsync(cancellationToken);

        var hasInitialized = await MobileDatabaseService.Instance.GetSettingAsync(InitializationMarkerKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(hasInitialized))
        {
            var migratedSnapshot = GetLegacySnapshot();
            await SaveAsync(migratedSnapshot, cancellationToken);
            return;
        }

        var snapshot = new AppSettingsSnapshot(
            await ReadDoubleAsync(ReadingSpeedKey, ReadingSpeed, cancellationToken),
            await ReadDoubleAsync(VolumePercentKey, VolumePercent, cancellationToken),
            await ReadDoubleAsync(TriggerRadiusKey, TriggerRadiusMeters, cancellationToken),
            await ReadDoubleAsync(AlertRadiusKey, AlertRadiusMeters, cancellationToken),
            await ReadDoubleAsync(WaitTimeKey, WaitTimeSeconds, cancellationToken),
            await ReadBoolAsync(AutoPlayKey, AutoPlayEnabled, cancellationToken),
            await ReadBoolAsync(NotifyNearKey, NotifyNearEnabled, cancellationToken),
            await ReadBoolAsync(BackgroundTrackingKey, BackgroundTrackingEnabled, cancellationToken),
            await ReadBoolAsync(BatterySaverKey, BatterySaverEnabled, cancellationToken),
            await ReadStringAsync(LanguageKey, LanguageCode, cancellationToken),
            await ReadThemeAsync(cancellationToken),
            await ReadBoolAsync(ApiModeKey, ApiModeEnabled, cancellationToken),
            await ReadBoolAsync(DeveloperModeKey, DeveloperModeEnabled, cancellationToken));

        ApplySnapshot(snapshot, persistToLegacyPreferences: false);
    }

    public AppSettingsSnapshot CreateSnapshot() => new(
        ReadingSpeed,
        VolumePercent,
        TriggerRadiusMeters,
        AlertRadiusMeters,
        WaitTimeSeconds,
        AutoPlayEnabled,
        NotifyNearEnabled,
        BackgroundTrackingEnabled,
        BatterySaverEnabled,
        LanguageCode,
        Theme,
        ApiModeEnabled,
        DeveloperModeEnabled);

    public async Task SaveAsync(AppSettingsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ApplySnapshot(snapshot, persistToLegacyPreferences: true);

        await MobileDatabaseService.Instance.SetSettingAsync(ReadingSpeedKey, ReadingSpeed.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(VolumePercentKey, VolumePercent.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(TriggerRadiusKey, TriggerRadiusMeters.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(AlertRadiusKey, AlertRadiusMeters.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(WaitTimeKey, WaitTimeSeconds.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(AutoPlayKey, AutoPlayEnabled.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(NotifyNearKey, NotifyNearEnabled.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(BackgroundTrackingKey, BackgroundTrackingEnabled.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(BatterySaverKey, BatterySaverEnabled.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(LanguageKey, LanguageCode, cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(ThemeKey, Theme.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(ApiModeKey, ApiModeEnabled.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(DeveloperModeKey, DeveloperModeEnabled.ToString(), cancellationToken);
        await MobileDatabaseService.Instance.SetSettingAsync(InitializationMarkerKey, bool.TrueString, cancellationToken);
    }

    private void ApplySnapshot(AppSettingsSnapshot snapshot, bool persistToLegacyPreferences)
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
        LanguageCode = string.IsNullOrWhiteSpace(snapshot.LanguageCode) ? "vi" : snapshot.LanguageCode.Trim().ToLowerInvariant();
        Theme = snapshot.Theme;
        ApiModeEnabled = snapshot.ApiModeEnabled;
        DeveloperModeEnabled = snapshot.DeveloperModeEnabled;

        if (persistToLegacyPreferences)
        {
            Preferences.Default.Set(ReadingSpeedKey, ReadingSpeed);
            Preferences.Default.Set(VolumePercentKey, VolumePercent);
            Preferences.Default.Set(TriggerRadiusKey, TriggerRadiusMeters);
            Preferences.Default.Set(AlertRadiusKey, AlertRadiusMeters);
            Preferences.Default.Set(WaitTimeKey, WaitTimeSeconds);
            Preferences.Default.Set(AutoPlayKey, AutoPlayEnabled);
            Preferences.Default.Set(NotifyNearKey, NotifyNearEnabled);
            Preferences.Default.Set(BackgroundTrackingKey, BackgroundTrackingEnabled);
            Preferences.Default.Set(BatterySaverKey, BatterySaverEnabled);
            Preferences.Default.Set(LanguageKey, LanguageCode);
            Preferences.Default.Set(ThemeKey, Theme.ToString());
            Preferences.Default.Set(ApiModeKey, ApiModeEnabled);
            Preferences.Default.Set(DeveloperModeKey, DeveloperModeEnabled);
        }

        LocalizationService.Instance.Language = LanguageCode;
        AppDataModeService.Instance.Initialize(ApiModeEnabled);
        ThemeService.Instance.ApplyPersistedTheme(Theme);

        RaisePropertyChanged(nameof(ReadingSpeed));
        RaisePropertyChanged(nameof(VolumePercent));
        RaisePropertyChanged(nameof(TriggerRadiusMeters));
        RaisePropertyChanged(nameof(AlertRadiusMeters));
        RaisePropertyChanged(nameof(WaitTimeSeconds));
        RaisePropertyChanged(nameof(AutoPlayEnabled));
        RaisePropertyChanged(nameof(NotifyNearEnabled));
        RaisePropertyChanged(nameof(BackgroundTrackingEnabled));
        RaisePropertyChanged(nameof(BatterySaverEnabled));
        RaisePropertyChanged(nameof(LanguageCode));
        RaisePropertyChanged(nameof(Theme));
        RaisePropertyChanged(nameof(ApiModeEnabled));
        RaisePropertyChanged(nameof(DeveloperModeEnabled));
        RaisePropertyChanged(nameof(PlaybackVolumeRatio));
        RaisePropertyChanged(nameof(AndroidSpeechRate));
    }

    private static AppSettingsSnapshot GetLegacySnapshot()
    {
        var themePreference = Preferences.Default.Get(ThemeKey, nameof(AppThemeOption.Eco));
        if (!Enum.TryParse(themePreference, true, out AppThemeOption theme))
        {
            theme = AppThemeOption.Eco;
        }

        return new AppSettingsSnapshot(
            Preferences.Default.Get(ReadingSpeedKey, 1.0d),
            Preferences.Default.Get(VolumePercentKey, 100d),
            Preferences.Default.Get(TriggerRadiusKey, 50d),
            Preferences.Default.Get(AlertRadiusKey, 100d),
            Preferences.Default.Get(WaitTimeKey, 300d),
            Preferences.Default.Get(AutoPlayKey, true),
            Preferences.Default.Get(NotifyNearKey, true),
            Preferences.Default.Get(BackgroundTrackingKey, true),
            Preferences.Default.Get(BatterySaverKey, false),
            Preferences.Default.Get(LanguageKey, LocalizationService.Instance.Language),
            theme,
            Preferences.Default.Get(ApiModeKey, true),
            Preferences.Default.Get(DeveloperModeKey, false));
    }

    private async Task<double> ReadDoubleAsync(string key, double fallback, CancellationToken cancellationToken)
    {
        var value = await MobileDatabaseService.Instance.GetSettingAsync(key, cancellationToken);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private async Task<bool> ReadBoolAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await MobileDatabaseService.Instance.GetSettingAsync(key, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private async Task<string> ReadStringAsync(string key, string fallback, CancellationToken cancellationToken)
    {
        var value = await MobileDatabaseService.Instance.GetSettingAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private async Task<AppThemeOption> ReadThemeAsync(CancellationToken cancellationToken)
    {
        var value = await MobileDatabaseService.Instance.GetSettingAsync(ThemeKey, cancellationToken);
        return Enum.TryParse(value, true, out AppThemeOption parsed) ? parsed : Theme;
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
    bool BatterySaverEnabled,
    string LanguageCode,
    AppThemeOption Theme,
    bool ApiModeEnabled,
    bool DeveloperModeEnabled);
