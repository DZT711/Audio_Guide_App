namespace Project_SharedClassLibrary.Storage;

public static class SharedStoragePaths
{
    public const string WebRootFolderName = "wwwroot";
    public const string ArchiveFolderName = "archive";
    public const string AudioFolderName = "audio";
    public const string ImageFolderName = "images";
    public const string AudioRequestPath = "/archive/audio";
    public const string ImageRequestPath = "/archive/images";
    public const string LegacySharedAudioRequestPath = "/shared-library/audio";
    public const string LegacySharedImageRequestPath = "/shared-library/images";
    public const string LegacyAudioRequestPath = "/audio";
    public const string LegacyImageRequestPath = "/images";

    public static string GetArchiveRoot(string contentRootPath) =>
        Path.Combine(Path.GetFullPath(contentRootPath), WebRootFolderName, ArchiveFolderName);

    public static string GetAudioDirectory(string contentRootPath) =>
        Path.Combine(GetArchiveRoot(contentRootPath), AudioFolderName);

    public static string GetImageDirectory(string contentRootPath) =>
        Path.Combine(GetArchiveRoot(contentRootPath), ImageFolderName);

    public static string ToPublicAudioPath(string fileName) =>
        ToPublicPath(AudioRequestPath, fileName);

    public static string ToPublicImagePath(string fileName) =>
        ToPublicPath(ImageRequestPath, fileName);

    public static string? NormalizePublicAudioPath(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return publicPath;
        }

        var normalizedPath = publicPath.Trim().Replace("\\", "/");
        if (TryNormalizeManagedPath(
                normalizedPath,
                AudioRequestPath,
                LegacyAudioRequestPath,
                LegacySharedAudioRequestPath,
                out var normalizedManagedPath))
        {
            return normalizedManagedPath;
        }

        return normalizedPath;
    }

    public static string? NormalizePublicImagePath(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return publicPath;
        }

        var normalizedPath = publicPath.Trim().Replace("\\", "/");
        if (TryNormalizeManagedPath(
                normalizedPath,
                ImageRequestPath,
                LegacyImageRequestPath,
                LegacySharedImageRequestPath,
                out var normalizedManagedPath))
        {
            return normalizedManagedPath;
        }

        return normalizedPath;
    }

    public static string? TryGetManagedAudioFileName(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return null;
        }

        var normalizedPath = NormalizePublicAudioPath(publicPath) ?? string.Empty;
        if (!normalizedPath.StartsWith(AudioRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(normalizedPath.TrimEnd('/'), AudioRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFileName(normalizedPath);
    }

    public static string? TryGetManagedImageFileName(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return null;
        }

        var normalizedPath = NormalizePublicImagePath(publicPath) ?? string.Empty;
        if (!normalizedPath.StartsWith(ImageRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(normalizedPath.TrimEnd('/'), ImageRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFileName(normalizedPath);
    }

    private static string ToPublicPath(string requestPath, string fileName) =>
        $"{requestPath}/{fileName.Replace("\\", "/")}";

    private static bool TryNormalizeManagedPath(
        string value,
        string canonicalRequestPath,
        string legacyShortRequestPath,
        string legacySharedRequestPath,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri) && !absoluteUri.IsFile)
        {
            return TryNormalizeManagedPath(
                absoluteUri.AbsolutePath,
                canonicalRequestPath,
                legacyShortRequestPath,
                legacySharedRequestPath,
                out normalizedPath);
        }

        var requestPath = value.TrimEnd('/');
        if (!requestPath.StartsWith(canonicalRequestPath, StringComparison.OrdinalIgnoreCase)
            && !requestPath.StartsWith(legacyShortRequestPath, StringComparison.OrdinalIgnoreCase)
            && !requestPath.StartsWith(legacySharedRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(requestPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            normalizedPath = canonicalRequestPath;
            return true;
        }

        normalizedPath = ToPublicPath(canonicalRequestPath, fileName);
        return true;
    }
}
