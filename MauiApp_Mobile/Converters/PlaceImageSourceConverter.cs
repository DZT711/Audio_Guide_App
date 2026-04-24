using System.Globalization;
using MauiApp_Mobile.Services;

namespace MauiApp_Mobile.Converters;

public sealed class PlaceImageSourceConverter : IValueConverter
{
    private static readonly TimeSpan FileExistenceCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<string, FileExistenceCacheEntry> FileExistenceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FileExistenceCacheGate = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ResolveImageSource(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static ImageSource ResolveImageSource(string? rawImage)
    {
        if (string.IsNullOrWhiteSpace(rawImage))
        {
            return ImageSource.FromFile("location.png");
        }

        var normalizedImage = rawImage.Trim().Replace("\\", "/");
        if (HasSvgExtension(normalizedImage))
        {
            return ImageSource.FromFile("location.png");
        }

        var resolvedImageUrl = MobileApiOptions.ResolveImageUrl(normalizedImage);
        if (Uri.TryCreate(resolvedImageUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile)
            {
                return CheckFileExistsWithCache(absoluteUri.LocalPath)
                    ? ImageSource.FromFile(absoluteUri.LocalPath)
                    : ImageSource.FromFile("location.png");
            }

            return ImageSource.FromUri(absoluteUri);
        }

        if (Path.IsPathRooted(normalizedImage) && CheckFileExistsWithCache(normalizedImage))
        {
            return ImageSource.FromFile(normalizedImage);
        }

        if (IsBundledImageAsset(normalizedImage))
        {
            return ImageSource.FromFile(normalizedImage);
        }

        return ImageSource.FromUri(new Uri(resolvedImageUrl));
    }

    private static bool HasSvgExtension(string imagePath) =>
        string.Equals(Path.GetExtension(imagePath), ".svg", StringComparison.OrdinalIgnoreCase);

    private static bool IsBundledImageAsset(string imagePath) =>
        !string.IsNullOrWhiteSpace(imagePath) &&
        !imagePath.Contains('/') &&
        !imagePath.Contains('\\');

    private static bool CheckFileExistsWithCache(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        lock (FileExistenceCacheGate)
        {
            if (FileExistenceCache.TryGetValue(filePath, out var cached)
                && nowUtc - cached.CheckedAtUtc <= FileExistenceCacheDuration)
            {
                return cached.Exists;
            }
        }

        var exists = File.Exists(filePath);
        lock (FileExistenceCacheGate)
        {
            FileExistenceCache[filePath] = new FileExistenceCacheEntry(exists, nowUtc);
        }

        return exists;
    }

    private readonly record struct FileExistenceCacheEntry(bool Exists, DateTimeOffset CheckedAtUtc);
}
