using MauiApp_Mobile.Services;
using Microsoft.Maui.ApplicationModel;

namespace MauiApp_Mobile.Views;

public partial class LanguagePage : ContentPage
{
    private bool _hasAnimated;
    private bool _isRefreshingLanguageText;

    public LanguagePage()
    {
        InitializeComponent();
        ApplyLanguage(LocalizationService.Instance.Language, animateText: false);
        ThemeService.Instance.PropertyChanged += (_, _) => ApplyLanguage(LocalizationService.Instance.Language);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_hasAnimated)
            return;

        _hasAnimated = true;
        _ = UiEffectsService.AnimateEntranceAsync(HeroStack, LanguageCardGrid, StartButton, FooterLabel);
    }

    private void ApplyLanguage(string languageCode, bool animateText = true)
    {
        if (_isRefreshingLanguageText)
            return;

        _isRefreshingLanguageText = true;

        LocalizationService.Instance.Language = languageCode;

        Action updateText = () =>
        {
            TitleLabel.Text = LocalizationService.Instance.T("Lang.Title");
            SubtitleLabel.Text = LocalizationService.Instance.T("Lang.Subtitle");
            ChooseLanguageLabel.Text = LocalizationService.Instance.T("Lang.Choose");
            StartButton.Text = LocalizationService.Instance.T("Lang.Start");
            FooterLabel.Text = LocalizationService.Instance.T("Lang.Footer");
        };

        if (animateText && _hasAnimated)
        {
            _ = AnimateLanguageTextAsync(updateText);
        }
        else
        {
            updateText();
            _isRefreshingLanguageText = false;
        }

        ResetSelectionStyle();

        switch (languageCode)
        {
            case "vi": SelectCard(CardVN, TickVN); break;
            case "en": SelectCard(CardEN, TickEN); break;
            case "cn": SelectCard(CardCN, TickCN); break;
            case "jp": SelectCard(CardJP, TickJP); break;
            case "kr": SelectCard(CardKR, TickKR); break;
            case "fr": SelectCard(CardFR, TickFR); break;
        }
    }

    private async Task AnimateLanguageTextAsync(Action updateText)
    {
        await UiEffectsService.CrossfadeTextAsync(
            updateText,
            TitleLabel,
            SubtitleLabel,
            ChooseLanguageLabel,
            StartButton,
            FooterLabel);

        _isRefreshingLanguageText = false;
    }

    private void ResetSelectionStyle()
    {
        ResetCard(CardVN, TickVN);
        ResetCard(CardEN, TickEN);
        ResetCard(CardCN, TickCN);
        ResetCard(CardJP, TickJP);
        ResetCard(CardKR, TickKR);
        ResetCard(CardFR, TickFR);
    }

    private void ResetCard(Border card, Border tick)
    {
        card.Stroke = new SolidColorBrush(ThemeService.Instance.GetColor("BorderColor", "#D6DBE3"));
        card.StrokeThickness = 1;
        card.BackgroundColor = ThemeService.Instance.GetColor("CardBg", "#FFFFFF");
        tick.IsVisible = false;
    }

    private void SelectCard(Border card, Border tick)
    {
        card.Stroke = new SolidColorBrush(ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B"));
        card.StrokeThickness = 1.4;
        card.BackgroundColor = ThemeService.Instance.GetColor("SoftGreen", "#EEF8F1");
        tick.IsVisible = true;
    }

    private void OnTapVietnamese(object sender, TappedEventArgs e) => ApplyLanguage("vi");
    private void OnTapEnglish(object sender, TappedEventArgs e) => ApplyLanguage("en");
    private void OnTapChinese(object sender, TappedEventArgs e) => ApplyLanguage("cn");
    private void OnTapJapanese(object sender, TappedEventArgs e) => ApplyLanguage("jp");
    private void OnTapKorean(object sender, TappedEventArgs e) => ApplyLanguage("kr");
    private void OnTapFrench(object sender, TappedEventArgs e) => ApplyLanguage("fr");

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await StartButton.ScaleToAsync(0.98, 70, Easing.CubicIn);
        await StartButton.ScaleToAsync(1, 160, Easing.CubicOut);

        var currentSettings = AppSettingsService.Instance.CreateSnapshot();
        await AppSettingsService.Instance.SaveAsync(currentSettings with
        {
            LanguageCode = LocalizationService.Instance.Language
        });

#if ANDROID
        if (!await EnsureAndroidTrackingPermissionsAsync())
        {
            return;
        }
#endif

        await Shell.Current.GoToAsync("//places");
    }

#if ANDROID
    private async Task<bool> EnsureAndroidTrackingPermissionsAsync()
    {
        while (true)
        {
            var foregroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (foregroundStatus != PermissionStatus.Granted)
            {
                foregroundStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (foregroundStatus != PermissionStatus.Granted)
            {
                var retryForeground = await DisplayAlertAsync(
                    "Quyền vị trí",
                    "Ứng dụng cần quyền vị trí chính xác để định vị bạn trên bản đồ và kích hoạt audio theo hành trình.",
                    "Yêu cầu lại",
                    "Để sau");

                if (!retryForeground)
                {
                    return false;
                }

                if (!Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
                {
                    return false;
                }

                continue;
            }

            var backgroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (backgroundStatus != PermissionStatus.Granted)
            {
                backgroundStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
            }

            if (backgroundStatus == PermissionStatus.Granted)
            {
                return true;
            }

            var retryBackground = await DisplayAlertAsync(
                "Theo dõi nền",
                "Hãy cho phép vị trí mọi lúc để app vẫn theo dõi được khi bạn rời khỏi màn hình và tiếp tục phát audio theo POI.",
                "Yêu cầu lại",
                "Mở cài đặt");

            if (!retryBackground)
            {
                return false;
            }

            if (!Permissions.ShouldShowRationale<Permissions.LocationAlways>())
            {
                return false;
            }
        }
    }
#endif
}
