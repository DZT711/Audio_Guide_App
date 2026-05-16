namespace MauiApp_Mobile.Models;

public sealed class PlaceGallerySlide
{
    public string Image { get; init; } = string.Empty;
    public string BadgeText { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string FooterText { get; init; } = string.Empty;
    public Color GradientStart { get; init; } = Colors.Black;
    public Color GradientEnd { get; init; } = Colors.Black;
    public Color BadgeBackground { get; init; } = Colors.White;
    public Color BadgeForeground { get; init; } = Colors.Black;
}
