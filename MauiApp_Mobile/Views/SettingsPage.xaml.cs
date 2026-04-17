using System.ComponentModel;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private bool _hasAnimated;
    private bool _isSyncingGpsControls;

    public SettingsPage()
    {
        InitializeComponent();
        LoadSavedSettings();
        ApplyTexts();
        ConfigureGpsAccuracyOptions();
        UpdateSliderLabels();
        UpdateLanguageSelectionUI();
        UpdateThemeSelectionUI();
        UpdateApiModeUI();

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        ThemeService.Instance.PropertyChanged += OnThemeChanged;
        AppDataModeService.Instance.PropertyChanged += OnAppDataModeChanged;
        AppSettingsService.Instance.SettingsSaved += OnSettingsSaved;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSavedSettings();
        UpdateLanguageSelectionUI();
        UpdateThemeSelectionUI();
        UpdateApiModeUI();
        UpdateSliderLabels();
        ConfigureGpsAccuracyOptions();

        if (_hasAnimated)
            return;

        _hasAnimated = true;
        _ = UiEffectsService.AnimateEntranceAsync(AudioCard, AppearanceCard, GpsCard, BehaviorCard);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = UiEffectsService.CrossfadeTextAsync(
            ApplyTexts,
            TitleLabel,
            AudioLabel,
            AppearanceLabel,
            ThemeHintLabel,
            GpsLabel,
            BehaviorLabel,
            LanguageRowLabel,
            LanguageValueLabel,
            VoiceRowLabel,
            VoiceValueLabel,
            ReadingSpeedLabel,
            VolumeLabel,
            TestVoiceButton,
            ThemeEcoTitleLabel,
            ThemeEcoSubtitleLabel,
            ThemeMidnightTitleLabel,
            ThemeMidnightSubtitleLabel,
            ThemeHeritageTitleLabel,
            ThemeHeritageSubtitleLabel,
            GpsAccuracyLabel,
            TriggerRadiusLabel,
            AlertRadiusLabel,
            ShowPoiRadiusLabel,
            AutoFocusIdleLabel,
            AutoFocusIdleHintLabel,
            AutoPlayLabel,
            NotifyNearLabel,
            BackgroundTrackingLabel,
            BatterySaverLabel,
            ApiModeLabel,
            DeveloperModeLabel,
            LanguagePopupTitleLabel,
            SaveSettingsButton);
        UpdateLanguageSelectionUI();
        UpdateApiModeUI();
    }

    private void OnThemeChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateLanguageSelectionUI();
        UpdateThemeSelectionUI();
    }

    private void OnAppDataModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(AppDataModeService.IsApiEnabled), StringComparison.Ordinal))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(UpdateApiModeUI);
    }

    private void OnSettingsSaved(object? sender, AppSettingsSnapshot snapshot)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadSavedSettings();
            UpdateApiModeUI();
            UpdateSliderLabels();
        });
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Settings.Title");
        AudioLabel.Text = LocalizationService.Instance.T("Settings.Audio");
        AppearanceLabel.Text = LocalizationService.Instance.T("Settings.Appearance");
        ThemeHintLabel.Text = LocalizationService.Instance.T("Settings.ThemeHint");
        GpsLabel.Text = LocalizationService.Instance.T("Settings.Gps");
        BehaviorLabel.Text = LocalizationService.Instance.T("Settings.Behavior");

        LanguageRowLabel.Text = LocalizationService.Instance.T("Settings.Language");
        LanguageValueLabel.Text = LocalizationService.Instance.T("Settings.LanguageValue");

        VoiceRowLabel.Text = LocalizationService.Instance.T("Settings.Voice");
        VoiceValueLabel.Text = LocalizationService.Instance.T("Settings.VoiceValue");
        ReadingSpeedLabel.Text = LocalizationService.Instance.T("Settings.Speed");
        VolumeLabel.Text = LocalizationService.Instance.T("Settings.Volume");
        TestVoiceButton.Text = LocalizationService.Instance.T("Settings.TestVoice");

        ThemeEcoTitleLabel.Text = LocalizationService.Instance.T("Settings.ThemeEcoTitle");
        ThemeEcoSubtitleLabel.Text = LocalizationService.Instance.T("Settings.ThemeEcoSubtitle");
        ThemeMidnightTitleLabel.Text = LocalizationService.Instance.T("Settings.ThemeMidnightTitle");
        ThemeMidnightSubtitleLabel.Text = LocalizationService.Instance.T("Settings.ThemeMidnightSubtitle");
        ThemeHeritageTitleLabel.Text = LocalizationService.Instance.T("Settings.ThemeHeritageTitle");
        ThemeHeritageSubtitleLabel.Text = LocalizationService.Instance.T("Settings.ThemeHeritageSubtitle");

        GpsAccuracyLabel.Text = LocalizationService.Instance.T("Settings.Accuracy");
        TriggerRadiusLabel.Text = LocalizationService.Instance.T("Settings.TriggerRadius");
        AlertRadiusLabel.Text = LocalizationService.Instance.T("Settings.AlertRadius");
        ShowPoiRadiusLabel.Text = "Hiện bán kính POI trên bản đồ";
        AutoFocusIdleLabel.Text = "Tự focus POI sau khi không chạm bản đồ";
        AutoFocusIdleHintLabel.Text = "s, -1 để tắt";

        AutoPlayLabel.Text = LocalizationService.Instance.T("Settings.AutoPlay");
        NotifyNearLabel.Text = LocalizationService.Instance.T("Settings.NotifyNear");
        BackgroundTrackingLabel.Text = LocalizationService.Instance.T("Settings.BackgroundTracking");
        BatterySaverLabel.Text = LocalizationService.Instance.T("Settings.BatterySaver");
        ApiModeLabel.Text = LocalizationService.Instance.T("Settings.Offline");
        DeveloperModeLabel.Text = "Hiện nút dev trên bản đồ";
        MiniPlayerLabel.Text = "Hiện thanh phát mini";
        SaveSettingsButton.Text = LocalizationService.Instance.T("Settings.Save");

        LanguagePopupTitleLabel.Text = LocalizationService.Instance.T("Settings.ChooseLanguage");

        LangViLabel.Text = "Tiếng Việt";
        LangEnLabel.Text = "English";
        LangCnLabel.Text = "中文";
        LangJpLabel.Text = "日本語";
        LangKrLabel.Text = "한국어";
        LangFrLabel.Text = "Français";

        ConfigureGpsAccuracyOptions();
    }

    private void ConfigureGpsAccuracyOptions()
    {
        if (GpsAccuracyPicker is null)
        {
            return;
        }

        var labels = new[]
        {
            "Very low",
            "Low",
            "Medium",
            "High",
            "Very high"
        };

        GpsAccuracyPicker.ItemsSource = labels;
        GpsAccuracyPicker.SelectedIndex = (int)AppSettingsService.Instance.GpsAccuracy;
    }

    private async void OnToggleLanguagePopupTapped(object sender, TappedEventArgs e)
    {
        await UiEffectsService.TogglePopupAsync(LanguagePopup, !LanguagePopup.IsVisible);
    }

    private async void ChangeLanguage(string code)
    {
        LocalizationService.Instance.Language = code;
        await UiEffectsService.TogglePopupAsync(LanguagePopup, false);
        UpdateLanguageSelectionUI();
    }

    private void UpdateLanguageSelectionUI()
    {
        ResetLanguageItem(LangViItem, LangViLabel, LangViIndicator);
        ResetLanguageItem(LangEnItem, LangEnLabel, LangEnIndicator);
        ResetLanguageItem(LangCnItem, LangCnLabel, LangCnIndicator);
        ResetLanguageItem(LangJpItem, LangJpLabel, LangJpIndicator);
        ResetLanguageItem(LangKrItem, LangKrLabel, LangKrIndicator);
        ResetLanguageItem(LangFrItem, LangFrLabel, LangFrIndicator);

        switch (LocalizationService.Instance.Language)
        {
            case "vi":
                SelectLanguageItem(LangViItem, LangViLabel, LangViIndicator);
                break;
            case "en":
                SelectLanguageItem(LangEnItem, LangEnLabel, LangEnIndicator);
                break;
            case "cn":
                SelectLanguageItem(LangCnItem, LangCnLabel, LangCnIndicator);
                break;
            case "jp":
                SelectLanguageItem(LangJpItem, LangJpLabel, LangJpIndicator);
                break;
            case "kr":
                SelectLanguageItem(LangKrItem, LangKrLabel, LangKrIndicator);
                break;
            case "fr":
                SelectLanguageItem(LangFrItem, LangFrLabel, LangFrIndicator);
                break;
        }
    }

    private void ResetLanguageItem(Grid item, Label label, BoxView indicator)
    {
        item.BackgroundColor = Colors.Transparent;
        label.TextColor = ThemeService.Instance.GetColor("BodyText", "#243B5A");
        label.FontAttributes = FontAttributes.None;
        indicator.IsVisible = false;
    }

    private void SelectLanguageItem(Grid item, Label label, BoxView indicator)
    {
        item.BackgroundColor = ThemeService.Instance.GetColor("SoftGreen", "#E8F7EE");
        label.TextColor = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
        label.FontAttributes = FontAttributes.Bold;
        indicator.IsVisible = true;
    }

    private void UpdateThemeSelectionUI()
    {
        SetThemeCardState(ThemeEcoCard, ThemeEcoBadge, ThemeService.Instance.CurrentTheme == AppThemeOption.Eco);
        SetThemeCardState(ThemeMidnightCard, ThemeMidnightBadge, ThemeService.Instance.CurrentTheme == AppThemeOption.Midnight);
        SetThemeCardState(ThemeHeritageCard, ThemeHeritageBadge, ThemeService.Instance.CurrentTheme == AppThemeOption.Heritage);
    }

    private void SetThemeCardState(Border card, Border badge, bool isSelected)
    {
        card.BackgroundColor = isSelected
            ? ThemeService.Instance.GetColor("SoftGreen", "#E8F7EE")
            : ThemeService.Instance.GetColor("CardBg", "#FFFFFF");
        card.Stroke = new SolidColorBrush(
            isSelected
                ? ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B")
                : ThemeService.Instance.GetColor("BorderColor", "#E5E7EB"));
        card.StrokeThickness = isSelected ? 2 : 1;
        badge.IsVisible = isSelected;
    }

    private void OnThemeEcoTapped(object sender, TappedEventArgs e) => ApplyTheme(AppThemeOption.Eco);
    private void OnThemeMidnightTapped(object sender, TappedEventArgs e) => ApplyTheme(AppThemeOption.Midnight);
    private void OnThemeHeritageTapped(object sender, TappedEventArgs e) => ApplyTheme(AppThemeOption.Heritage);

    private void ApplyTheme(AppThemeOption theme)
    {
        ThemeService.Instance.SetTheme(theme);
        UpdateThemeSelectionUI();
    }

    private void UpdateSliderLabels()
    {
        ReadingSpeedValueLabel.Text = $"{ReadingSpeedSlider.Value:0.0}x";
        VolumeValueLabel.Text = $"{Math.Round(VolumeSlider.Value):0}%";
        TriggerRadiusValueLabel.Text = $"{Math.Round(TriggerRadiusSlider.Value):0}m";
        AlertRadiusValueLabel.Text = $"{Math.Round(AlertRadiusSlider.Value):0}m";
    }

    private void UpdateApiModeUI()
    {
        var isOfflineModeEnabled = !AppDataModeService.Instance.IsApiEnabled;
        if (ApiModeSwitch.IsToggled != isOfflineModeEnabled)
        {
            ApiModeSwitch.IsToggled = isOfflineModeEnabled;
        }
    }

    private void LoadSavedSettings()
    {
        var settings = AppSettingsService.Instance.CreateSnapshot();
        ReadingSpeedSlider.Value = settings.ReadingSpeed;
        VolumeSlider.Value = settings.VolumePercent;
        TriggerRadiusSlider.Value = settings.TriggerRadiusMeters;
        AlertRadiusSlider.Value = settings.AlertRadiusMeters;
        AutoPlaySwitch.IsToggled = settings.AutoPlayEnabled;
        NotifyNearSwitch.IsToggled = settings.NotifyNearEnabled;
        BackgroundTrackingSwitch.IsToggled = settings.BackgroundTrackingEnabled;
        BatterySaverSwitch.IsToggled = settings.BatterySaverEnabled;
        DeveloperModeSwitch.IsToggled = settings.DeveloperModeEnabled;
        MiniPlayerSwitch.IsToggled = settings.MiniPlayerEnabled;
        ShowPoiRadiusSwitch.IsToggled = settings.ShowPoiRadiusEnabled;
        AutoFocusIdleEntry.Text = settings.AutoFocusIdleSeconds.ToString();
        _isSyncingGpsControls = true;
        GpsAccuracyPicker.SelectedIndex = (int)settings.GpsAccuracy;
        _isSyncingGpsControls = false;
    }

    private void OnReadingSpeedChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnVolumeChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnTriggerRadiusChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnAlertRadiusChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();

    private void OnGpsAccuracyChanged(object sender, EventArgs e)
    {
        if (_isSyncingGpsControls || GpsAccuracyPicker.SelectedIndex < 0)
        {
            return;
        }

        var selectedAccuracy = (GpsAccuracyOption)GpsAccuracyPicker.SelectedIndex;
        if (selectedAccuracy is GpsAccuracyOption.High or GpsAccuracyOption.VeryHigh)
        {
            BatterySaverSwitch.IsToggled = false;
        }
    }

    private void OnBatterySaverToggled(object sender, ToggledEventArgs e)
    {
        if (_isSyncingGpsControls)
        {
            return;
        }

        if (!e.Value)
        {
            return;
        }

        _isSyncingGpsControls = true;
        BackgroundTrackingSwitch.IsToggled = false;
        GpsAccuracyPicker.SelectedIndex = (int)GpsAccuracyOption.VeryLow;
        _isSyncingGpsControls = false;
    }

    private async void OnBackgroundTrackingToggled(object sender, ToggledEventArgs e)
    {
        if (_isSyncingGpsControls || !e.Value)
        {
            return;
        }

        if (!BatterySaverSwitch.IsToggled)
        {
            return;
        }

        _isSyncingGpsControls = true;
        BatterySaverSwitch.IsToggled = false;
        _isSyncingGpsControls = false;

        await DisplayAlertAsync(
            "Theo dõi nền",
            "Bật theo dõi nền sẽ tự tắt chế độ tiết kiệm pin để vị trí được cập nhật ổn định hơn.",
            "OK");
    }

    private async void OnTestVoiceClicked(object sender, EventArgs e)
    {
        try
        {
            TestVoiceButton.IsEnabled = false;
            await AudioPlaybackService.Instance.TestCurrentVoiceAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Audio", FriendlyMessageService.Resolve(ex, "Server connect failure"), "OK");
        }
        finally
        {
            TestVoiceButton.IsEnabled = true;
        }
    }

    private async void OnSaveSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettingsButton.IsEnabled = false;

            if (BackgroundTrackingSwitch.IsToggled && BatterySaverSwitch.IsToggled)
            {
                BatterySaverSwitch.IsToggled = false;
            }

            await AppSettingsService.Instance.SaveAsync(new AppSettingsSnapshot(
                ReadingSpeedSlider.Value,
                VolumeSlider.Value,
                TriggerRadiusSlider.Value,
                AlertRadiusSlider.Value,
                AutoPlaySwitch.IsToggled,
                NotifyNearSwitch.IsToggled,
                BackgroundTrackingSwitch.IsToggled,
                BatterySaverSwitch.IsToggled,
                LocalizationService.Instance.Language,
                ThemeService.Instance.CurrentTheme,
                !ApiModeSwitch.IsToggled,
                DeveloperModeSwitch.IsToggled,
                GpsAccuracyPicker.SelectedIndex >= 0 ? (GpsAccuracyOption)GpsAccuracyPicker.SelectedIndex : GpsAccuracyOption.High,
                MiniPlayerSwitch.IsToggled,
                ShowPoiRadiusSwitch.IsToggled,
                ParseAutoFocusIdleSeconds()));

            await AudioPlaybackService.Instance.ApplyRuntimeVolumeAsync();
            await LocationTrackingService.Instance.StartTrackingFromSettingsAsync(requestBackgroundUpgrade: BackgroundTrackingSwitch.IsToggled);

            await DisplayAlertAsync(
                LocalizationService.Instance.T("Settings.Title"),
                LocalizationService.Instance.T("Settings.SaveSuccess"),
                "OK");
        }
        finally
        {
            SaveSettingsButton.IsEnabled = true;
        }
    }

    private void OnLanguageViTapped(object sender, TappedEventArgs e) => ChangeLanguage("vi");
    private void OnLanguageEnTapped(object sender, TappedEventArgs e) => ChangeLanguage("en");
    private void OnLanguageCnTapped(object sender, TappedEventArgs e) => ChangeLanguage("cn");
    private void OnLanguageJpTapped(object sender, TappedEventArgs e) => ChangeLanguage("jp");
    private void OnLanguageKrTapped(object sender, TappedEventArgs e) => ChangeLanguage("kr");
    private void OnLanguageFrTapped(object sender, TappedEventArgs e) => ChangeLanguage("fr");

    private void OnApiModeToggled(object sender, ToggledEventArgs e)
    {
        AppDataModeService.Instance.IsApiEnabled = !e.Value;
    }

    private int ParseAutoFocusIdleSeconds()
    {
        if (!int.TryParse(AutoFocusIdleEntry.Text?.Trim(), out var value))
        {
            return 60;
        }

        return Math.Clamp(value, -1, 3600);
    }
}
