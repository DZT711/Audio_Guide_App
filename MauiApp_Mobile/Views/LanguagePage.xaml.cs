using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Views;

public partial class LanguagePage : ContentPage
{
    private bool _hasAnimated;

    public LanguagePage()
    {
        InitializeComponent();
        ApplyLanguage("vi");
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

    private void ApplyLanguage(string languageCode)
    {
        LocalizationService.Instance.Language = languageCode;

        TitleLabel.Text = LocalizationService.Instance.T("Lang.Title");
        SubtitleLabel.Text = LocalizationService.Instance.T("Lang.Subtitle");
        ChooseLanguageLabel.Text = LocalizationService.Instance.T("Lang.Choose");
        StartButton.Text = LocalizationService.Instance.T("Lang.Start");
        FooterLabel.Text = LocalizationService.Instance.T("Lang.Footer");

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

    private void ResetSelectionStyle()
    {
        ResetCard(CardVN, TickVN);
        ResetCard(CardEN, TickEN);
        ResetCard(CardCN, TickCN);
        ResetCard(CardJP, TickJP);
        ResetCard(CardKR, TickKR);
        ResetCard(CardFR, TickFR);
    }

    private void ResetCard(Frame card, Frame tick)
    {
        card.BorderColor = ThemeService.Instance.GetColor("BorderColor", "#D6DBE3");
        card.BackgroundColor = ThemeService.Instance.GetColor("CardBg", "#FFFFFF");
        tick.IsVisible = false;
    }

    private void SelectCard(Frame card, Frame tick)
    {
        card.BorderColor = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
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
        await Shell.Current.GoToAsync("//places");
    }
}
