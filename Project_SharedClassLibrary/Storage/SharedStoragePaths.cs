namespace Project_SharedClassLibrary.Storage;

public static class SharedStoragePaths
{
    public const string SharedStorageFolderName = "SharedStorage";
    public const string AudioFolderName = "Audio";
    public const string ImageFolderName = "Images";
    public const string AudioRequestPath = "/shared-library/audio";
    public const string ImageRequestPath = "/shared-library/images";

    public static string GetSharedLibraryRoot(string contentRootPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(contentRootPath));

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Project_SharedClassLibrary");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, "..", "Project_SharedClassLibrary"));
    }

    public static string GetAudioDirectory(string contentRootPath) =>
        Path.Combine(GetSharedLibraryRoot(contentRootPath), SharedStorageFolderName, AudioFolderName);

    public static string GetImageDirectory(string contentRootPath) =>
        Path.Combine(GetSharedLibraryRoot(contentRootPath), SharedStorageFolderName, ImageFolderName);

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
        if (normalizedPath.StartsWith("/audio/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(normalizedPath);
            return string.IsNullOrWhiteSpace(fileName)
                ? normalizedPath
                : ToPublicAudioPath(fileName);
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
        if (normalizedPath.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(normalizedPath);
            return string.IsNullOrWhiteSpace(fileName)
                ? normalizedPath
                : ToPublicImagePath(fileName);
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

        return Path.GetFileName(normalizedPath);
    }

    private static string ToPublicPath(string requestPath, string fileName) =>
        $"{requestPath}/{fileName.Replace("\\", "/")}";
}
