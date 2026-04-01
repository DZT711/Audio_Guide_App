using System.ComponentModel;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class SettingsPage : ContentPage
{
    private bool _hasAnimated;

    public SettingsPage()
    {
        InitializeComponent();
        ApplyTexts();
        UpdateSliderLabels();
        UpdateLanguageSelectionUI();
        UpdateThemeSelectionUI();

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        ThemeService.Instance.PropertyChanged += OnThemeChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_hasAnimated)
            return;

        _hasAnimated = true;
        _ = UiEffectsService.AnimateEntranceAsync(AudioCard, AppearanceCard, GpsCard, BehaviorCard, SaveButton);
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
            GpsAccuracyValueLabel,
            TriggerRadiusLabel,
            AlertRadiusLabel,
            WaitTimeLabel,
            AutoPlayLabel,
            NotifyNearLabel,
            BackgroundTrackingLabel,
            BatterySaverLabel,
            OfflineModeLabel,
            SaveButton,
            LanguagePopupTitleLabel);
        UpdateLanguageSelectionUI();
    }

    private void OnThemeChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateLanguageSelectionUI();
        UpdateThemeSelectionUI();
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Settings.Title");
        AudioLabel.Text = LocalizationService.Instance.T("Settings.Audio");
        AppearanceLabel.Text = LocalizationService.Instance.T("Settings.Appearance");
        ThemeHintLabel.Text = LocalizationService.Instance.T("Settings.ThemeHint");
        GpsLabel.Text = LocalizationService.Instance.T("Settings.Gps");
        BehaviorLabel.Text = LocalizationService.Instance.T("Settings.Behavior");
        SaveButton.Text = LocalizationService.Instance.T("Settings.Save");

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
        GpsAccuracyValueLabel.Text = LocalizationService.Instance.T("Settings.AccuracyValue");
        TriggerRadiusLabel.Text = LocalizationService.Instance.T("Settings.TriggerRadius");
        AlertRadiusLabel.Text = LocalizationService.Instance.T("Settings.AlertRadius");
        WaitTimeLabel.Text = LocalizationService.Instance.T("Settings.WaitTime");

        AutoPlayLabel.Text = LocalizationService.Instance.T("Settings.AutoPlay");
        NotifyNearLabel.Text = LocalizationService.Instance.T("Settings.NotifyNear");
        BackgroundTrackingLabel.Text = LocalizationService.Instance.T("Settings.BackgroundTracking");
        BatterySaverLabel.Text = LocalizationService.Instance.T("Settings.BatterySaver");
        OfflineModeLabel.Text = LocalizationService.Instance.T("Settings.Offline");

        LanguagePopupTitleLabel.Text = LocalizationService.Instance.T("Settings.ChooseLanguage");

        LangViLabel.Text = "Tiếng Việt";
        LangEnLabel.Text = "English";
        LangCnLabel.Text = "中文";
        LangJpLabel.Text = "日本語";
        LangKrLabel.Text = "한국어";
        LangFrLabel.Text = "Français";
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
        WaitTimeValueLabel.Text = $"{Math.Round(WaitTimeSlider.Value):0}s";
    }

    private void OnReadingSpeedChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnVolumeChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnTriggerRadiusChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnAlertRadiusChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();
    private void OnWaitTimeChanged(object sender, ValueChangedEventArgs e) => UpdateSliderLabels();

    private async void OnSaveTapped(object sender, EventArgs e)
    {
        await SaveButton.ScaleToAsync(0.98, 70, Easing.CubicIn);
        await SaveButton.ScaleToAsync(1, 150, Easing.CubicOut);
        await DisplayAlertAsync(
            LocalizationService.Instance.T("Settings.Title"),
            LocalizationService.Instance.T("Settings.SaveSuccess"),
            "OK");
    }

    private void OnLanguageViTapped(object sender, TappedEventArgs e) => ChangeLanguage("vi");
    private void OnLanguageEnTapped(object sender, TappedEventArgs e) => ChangeLanguage("en");
    private void OnLanguageCnTapped(object sender, TappedEventArgs e) => ChangeLanguage("cn");
    private void OnLanguageJpTapped(object sender, TappedEventArgs e) => ChangeLanguage("jp");
    private void OnLanguageKrTapped(object sender, TappedEventArgs e) => ChangeLanguage("kr");
    private void OnLanguageFrTapped(object sender, TappedEventArgs e) => ChangeLanguage("fr");
}
