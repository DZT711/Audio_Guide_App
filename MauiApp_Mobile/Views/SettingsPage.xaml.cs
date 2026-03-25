using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        ApplyTexts();
        UpdateLanguageSelectionUI();

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            ApplyTexts();
            UpdateLanguageSelectionUI();
        };
    }

    private void ApplyTexts()
    {
        TitleLabel.Text = LocalizationService.Instance.T("Settings.Title");
        AudioLabel.Text = LocalizationService.Instance.T("Settings.Audio");
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

    private void OnToggleLanguagePopupTapped(object sender, TappedEventArgs e)
    {
        LanguagePopup.IsVisible = !LanguagePopup.IsVisible;
    }

    private void ChangeLanguage(string code)
    {
        LocalizationService.Instance.Language = code;
        LanguagePopup.IsVisible = false;
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
        label.TextColor = Color.FromArgb("#243B5A");
        label.FontAttributes = FontAttributes.None;
        indicator.IsVisible = false;
    }

    private void SelectLanguageItem(Grid item, Label label, BoxView indicator)
    {
        item.BackgroundColor = Color.FromArgb("#E8F7EE");
        label.TextColor = Color.FromArgb("#18A94B");
        label.FontAttributes = FontAttributes.Bold;
        indicator.IsVisible = true;
    }

    private void OnLanguageViTapped(object sender, TappedEventArgs e) => ChangeLanguage("vi");
    private void OnLanguageEnTapped(object sender, TappedEventArgs e) => ChangeLanguage("en");
    private void OnLanguageCnTapped(object sender, TappedEventArgs e) => ChangeLanguage("cn");
    private void OnLanguageJpTapped(object sender, TappedEventArgs e) => ChangeLanguage("jp");
    private void OnLanguageKrTapped(object sender, TappedEventArgs e) => ChangeLanguage("kr");
    private void OnLanguageFrTapped(object sender, TappedEventArgs e) => ChangeLanguage("fr");
}