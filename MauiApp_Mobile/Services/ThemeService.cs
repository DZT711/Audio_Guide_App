using System.ComponentModel;
using Microsoft.Maui.Storage;
#if ANDROID
using Android.Views;
using AndroidColor = Android.Graphics.Color;
using Microsoft.Maui.ApplicationModel;
#endif

namespace MauiApp_Mobile.Services;

public enum AppThemeOption
{
    Eco,
    Midnight,
    Heritage
}

public sealed class ThemeService : INotifyPropertyChanged
{
    private const string ThemePreferenceKey = "smarttour.theme";

    public static ThemeService Instance { get; } = new();

    private AppThemeOption _currentTheme = AppThemeOption.Eco;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppThemeOption CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme == value)
                return;

            _currentTheme = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    public bool IsDark => GetPalette(CurrentTheme).IsDark;

    public string MapThemeKey => GetPalette(CurrentTheme).MapThemeKey;

    private ThemeService()
    {
    }

    public void Initialize()
    {
        var savedValue = Preferences.Default.Get(ThemePreferenceKey, nameof(AppThemeOption.Eco));
        if (!Enum.TryParse(savedValue, true, out AppThemeOption parsedTheme))
        {
            parsedTheme = AppThemeOption.Eco;
        }

        ApplyTheme(parsedTheme, persistSelection: false);
    }

    public void SetTheme(AppThemeOption theme) => ApplyTheme(theme, persistSelection: true);

    public Color GetColor(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.Resources.ContainsKey(resourceKey) == true &&
            Application.Current.Resources[resourceKey] is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallbackHex);
    }

    private void ApplyTheme(AppThemeOption theme, bool persistSelection)
    {
        CurrentTheme = theme;

        if (Application.Current?.Resources is not ResourceDictionary resources)
            return;

        var palette = GetPalette(theme);

        SetColor(resources, "PrimaryGreen", palette.PrimaryGreen);
        SetColor(resources, "PrimaryGreenDark", palette.PrimaryGreenDark);
        SetColor(resources, "LightBg", palette.LightBg);
        SetColor(resources, "CardBg", palette.CardBg);
        SetColor(resources, "SurfaceAlt", palette.SurfaceAlt);
        SetColor(resources, "InputBg", palette.InputBg);
        SetColor(resources, "PopupBg", palette.PopupBg);
        SetColor(resources, "BottomSheetBg", palette.BottomSheetBg);
        SetColor(resources, "MutedText", palette.MutedText);
        SetColor(resources, "BodyText", palette.BodyText);
        SetColor(resources, "TitleText", palette.TitleText);
        SetColor(resources, "OnAccentText", palette.OnAccentText);
        SetColor(resources, "BorderColor", palette.BorderColor);
        SetColor(resources, "DividerColor", palette.DividerColor);
        SetColor(resources, "SoftGreen", palette.SoftGreen);
        SetColor(resources, "SoftPurple", palette.SoftPurple);
        SetColor(resources, "SoftOrange", palette.SoftOrange);
        SetColor(resources, "SoftRed", palette.SoftRed);
        SetColor(resources, "InfoText", palette.InfoText);
        SetColor(resources, "WarningText", palette.WarningText);
        SetColor(resources, "DangerText", palette.DangerText);
        SetColor(resources, "SuccessText", palette.SuccessText);
        SetColor(resources, "OverlayColor", palette.OverlayColor);
        SetColor(resources, "SheetHandleColor", palette.SheetHandleColor);
        SetColor(resources, "SkeletonBaseColor", palette.SkeletonBaseColor);
        SetColor(resources, "SkeletonHighlightColor", palette.SkeletonHighlightColor);
        SetColor(resources, "HeroBubbleColor", palette.HeroBubbleColor);
        SetColor(resources, "HeroBubbleAltColor", palette.HeroBubbleAltColor);
        SetColor(resources, "MapButtonBg", palette.MapButtonBg);
        SetColor(resources, "MapButtonRing", palette.MapButtonRing);
        SetColor(resources, "TabBarBackgroundColor", palette.TabBarBackgroundColor);
        SetColor(resources, "TabBarUnselectedColor", palette.TabBarUnselectedColor);
        SetColor(resources, "HeaderGradientStart", palette.HeaderGradientStart);
        SetColor(resources, "HeaderGradientEnd", palette.HeaderGradientEnd);
        SetColor(resources, "HeaderActionSurface", palette.HeaderActionSurface);
        SetColor(resources, "HeaderSupportText", palette.HeaderSupportText);

        resources["HeaderGradientBrush"] = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop
                {
                    Color = palette.HeaderGradientStart,
                    Offset = 0f
                },
                new GradientStop
                {
                    Color = palette.HeaderGradientEnd,
                    Offset = 1f
                }
            }
        };

        Application.Current.UserAppTheme = palette.IsDark ? AppTheme.Dark : AppTheme.Light;
        ApplyStatusBarColor(palette.HeaderGradientStart);

        if (persistSelection)
        {
            Preferences.Default.Set(ThemePreferenceKey, theme.ToString());
        }
    }

    private static void SetColor(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = color;
    }

    private static ThemePalette GetPalette(AppThemeOption theme) => theme switch
    {
        AppThemeOption.Midnight => new ThemePalette
        {
            IsDark = true,
            MapThemeKey = "midnight",
            PrimaryGreen = Color.FromArgb("#58D18E"),
            PrimaryGreenDark = Color.FromArgb("#41B376"),
            LightBg = Color.FromArgb("#09141D"),
            CardBg = Color.FromArgb("#112230"),
            SurfaceAlt = Color.FromArgb("#152A39"),
            InputBg = Color.FromArgb("#173242"),
            PopupBg = Color.FromArgb("#112230"),
            BottomSheetBg = Color.FromArgb("#10202D"),
            MutedText = Color.FromArgb("#9BB3C7"),
            BodyText = Color.FromArgb("#ECF7F1"),
            TitleText = Color.FromArgb("#F8FEFA"),
            OnAccentText = Color.FromArgb("#062012"),
            BorderColor = Color.FromArgb("#274053"),
            DividerColor = Color.FromArgb("#233A4A"),
            SoftGreen = Color.FromArgb("#173A2A"),
            SoftPurple = Color.FromArgb("#1A324C"),
            SoftOrange = Color.FromArgb("#3B2A20"),
            SoftRed = Color.FromArgb("#442428"),
            InfoText = Color.FromArgb("#7CC6FF"),
            WarningText = Color.FromArgb("#F5C979"),
            DangerText = Color.FromArgb("#FF9A9F"),
            SuccessText = Color.FromArgb("#7DE8A7"),
            OverlayColor = Color.FromArgb("#99040B10"),
            SheetHandleColor = Color.FromArgb("#426274"),
            SkeletonBaseColor = Color.FromArgb("#173242"),
            SkeletonHighlightColor = Color.FromArgb("#214559"),
            HeroBubbleColor = Color.FromArgb("#2BC97F"),
            HeroBubbleAltColor = Color.FromArgb("#6EE7A7"),
            MapButtonBg = Color.FromArgb("#143040"),
            MapButtonRing = Color.FromArgb("#1C4B60"),
            TabBarBackgroundColor = Color.FromArgb("#0C1B26"),
            TabBarUnselectedColor = Color.FromArgb("#7390A3"),
            HeaderGradientStart = Color.FromArgb("#08141D"),
            HeaderGradientEnd = Color.FromArgb("#1A4053"),
            HeaderActionSurface = Color.FromArgb("#26485A"),
            HeaderSupportText = Color.FromArgb("#B8D6E6")
        },
        AppThemeOption.Heritage => new ThemePalette
        {
            IsDark = false,
            MapThemeKey = "heritage",
            PrimaryGreen = Color.FromArgb("#C46A34"),
            PrimaryGreenDark = Color.FromArgb("#A65627"),
            LightBg = Color.FromArgb("#F8F2EC"),
            CardBg = Color.FromArgb("#FFF9F2"),
            SurfaceAlt = Color.FromArgb("#FFF6EC"),
            InputBg = Color.FromArgb("#FFFDF8"),
            PopupBg = Color.FromArgb("#FFF9F2"),
            BottomSheetBg = Color.FromArgb("#FFF9F2"),
            MutedText = Color.FromArgb("#8F6F5B"),
            BodyText = Color.FromArgb("#472B1C"),
            TitleText = Color.FromArgb("#30180E"),
            OnAccentText = Color.FromArgb("#FFF7EF"),
            BorderColor = Color.FromArgb("#E9D5C8"),
            DividerColor = Color.FromArgb("#F0E2D7"),
            SoftGreen = Color.FromArgb("#F8E3D4"),
            SoftPurple = Color.FromArgb("#E9F0EA"),
            SoftOrange = Color.FromArgb("#FFE8D8"),
            SoftRed = Color.FromArgb("#FDE4DF"),
            InfoText = Color.FromArgb("#2D7A78"),
            WarningText = Color.FromArgb("#A35E1E"),
            DangerText = Color.FromArgb("#C1543E"),
            SuccessText = Color.FromArgb("#5F7C42"),
            OverlayColor = Color.FromArgb("#73402412"),
            SheetHandleColor = Color.FromArgb("#D7B8A3"),
            SkeletonBaseColor = Color.FromArgb("#EEDFD2"),
            SkeletonHighlightColor = Color.FromArgb("#F9EFE6"),
            HeroBubbleColor = Color.FromArgb("#F4C9A7"),
            HeroBubbleAltColor = Color.FromArgb("#EAC59F"),
            MapButtonBg = Color.FromArgb("#FFF6EA"),
            MapButtonRing = Color.FromArgb("#F4D7C0"),
            TabBarBackgroundColor = Color.FromArgb("#FFF8F0"),
            TabBarUnselectedColor = Color.FromArgb("#B59683"),
            HeaderGradientStart = Color.FromArgb("#8E4A26"),
            HeaderGradientEnd = Color.FromArgb("#D08243"),
            HeaderActionSurface = Color.FromArgb("#E4A56A"),
            HeaderSupportText = Color.FromArgb("#FFE9D5")
        },
        _ => new ThemePalette
        {
            IsDark = false,
            MapThemeKey = "emerald",
            PrimaryGreen = Color.FromArgb("#18A94B"),
            PrimaryGreenDark = Color.FromArgb("#148F40"),
            LightBg = Color.FromArgb("#F3F4F6"),
            CardBg = Color.FromArgb("#FFFFFF"),
            SurfaceAlt = Color.FromArgb("#F8FAFC"),
            InputBg = Color.FromArgb("#FFFFFF"),
            PopupBg = Color.FromArgb("#FFFFFF"),
            BottomSheetBg = Color.FromArgb("#FFFFFF"),
            MutedText = Color.FromArgb("#8A94A6"),
            BodyText = Color.FromArgb("#1E3250"),
            TitleText = Color.FromArgb("#0F172A"),
            OnAccentText = Color.FromArgb("#FFFFFF"),
            BorderColor = Color.FromArgb("#E5E7EB"),
            DividerColor = Color.FromArgb("#EEF0F3"),
            SoftGreen = Color.FromArgb("#E8F7EE"),
            SoftPurple = Color.FromArgb("#EFF6FF"),
            SoftOrange = Color.FromArgb("#FFE8D8"),
            SoftRed = Color.FromArgb("#FFE3E3"),
            InfoText = Color.FromArgb("#2563EB"),
            WarningText = Color.FromArgb("#CA8A04"),
            DangerText = Color.FromArgb("#E53935"),
            SuccessText = Color.FromArgb("#0F8E41"),
            OverlayColor = Color.FromArgb("#66000000"),
            SheetHandleColor = Color.FromArgb("#D0D5DD"),
            SkeletonBaseColor = Color.FromArgb("#E7EBF0"),
            SkeletonHighlightColor = Color.FromArgb("#F4F6F8"),
            HeroBubbleColor = Color.FromArgb("#49D16F"),
            HeroBubbleAltColor = Color.FromArgb("#A7F3C5"),
            MapButtonBg = Color.FromArgb("#FFFFFF"),
            MapButtonRing = Color.FromArgb("#D6FAE3"),
            TabBarBackgroundColor = Color.FromArgb("#FFFFFF"),
            TabBarUnselectedColor = Color.FromArgb("#98A2B3"),
            HeaderGradientStart = Color.FromArgb("#109245"),
            HeaderGradientEnd = Color.FromArgb("#1DB954"),
            HeaderActionSurface = Color.FromArgb("#45C26F"),
            HeaderSupportText = Color.FromArgb("#E8F7EE")
        }
    };

    private sealed class ThemePalette
    {
        public bool IsDark { get; init; }
        public string MapThemeKey { get; init; } = "emerald";
        public Color PrimaryGreen { get; init; } = Colors.Green;
        public Color PrimaryGreenDark { get; init; } = Colors.Green;
        public Color LightBg { get; init; } = Colors.White;
        public Color CardBg { get; init; } = Colors.White;
        public Color SurfaceAlt { get; init; } = Colors.White;
        public Color InputBg { get; init; } = Colors.White;
        public Color PopupBg { get; init; } = Colors.White;
        public Color BottomSheetBg { get; init; } = Colors.White;
        public Color MutedText { get; init; } = Colors.Gray;
        public Color BodyText { get; init; } = Colors.Black;
        public Color TitleText { get; init; } = Colors.Black;
        public Color OnAccentText { get; init; } = Colors.White;
        public Color BorderColor { get; init; } = Colors.LightGray;
        public Color DividerColor { get; init; } = Colors.LightGray;
        public Color SoftGreen { get; init; } = Colors.LightGreen;
        public Color SoftPurple { get; init; } = Colors.LightBlue;
        public Color SoftOrange { get; init; } = Colors.Orange;
        public Color SoftRed { get; init; } = Colors.Pink;
        public Color InfoText { get; init; } = Colors.Blue;
        public Color WarningText { get; init; } = Colors.Orange;
        public Color DangerText { get; init; } = Colors.Red;
        public Color SuccessText { get; init; } = Colors.Green;
        public Color OverlayColor { get; init; } = Color.FromArgb("#66000000");
        public Color SheetHandleColor { get; init; } = Colors.LightGray;
        public Color SkeletonBaseColor { get; init; } = Colors.LightGray;
        public Color SkeletonHighlightColor { get; init; } = Colors.WhiteSmoke;
        public Color HeroBubbleColor { get; init; } = Colors.LightGreen;
        public Color HeroBubbleAltColor { get; init; } = Colors.LightGreen;
        public Color MapButtonBg { get; init; } = Colors.White;
        public Color MapButtonRing { get; init; } = Colors.LightGreen;
        public Color TabBarBackgroundColor { get; init; } = Colors.White;
        public Color TabBarUnselectedColor { get; init; } = Colors.Gray;
        public Color HeaderGradientStart { get; init; } = Colors.Green;
        public Color HeaderGradientEnd { get; init; } = Colors.Green;
        public Color HeaderActionSurface { get; init; } = Colors.Green;
        public Color HeaderSupportText { get; init; } = Colors.White;
    }

#if ANDROID
    private static void ApplyStatusBarColor(Color color)
    {
        var window = Platform.CurrentActivity?.Window;
        if (window is null)
            return;

        window.SetStatusBarColor(ToAndroidColor(color));

        var decorView = window.DecorView;
        if (decorView is not null)
        {
            decorView.SystemUiVisibility &= ~((StatusBarVisibility)SystemUiFlags.LightStatusBar);
        }
    }

    private static AndroidColor ToAndroidColor(Color color)
    {
        return AndroidColor.Argb(
            (int)Math.Round(color.Alpha * 255),
            (int)Math.Round(color.Red * 255),
            (int)Math.Round(color.Green * 255),
            (int)Math.Round(color.Blue * 255));
    }
#else
    private static void ApplyStatusBarColor(Color color)
    {
    }
#endif
}
