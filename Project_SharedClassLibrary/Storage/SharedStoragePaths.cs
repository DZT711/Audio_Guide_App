namespace Project_SharedClassLibrary.Storage;

public static class SharedStoragePaths
{
    public const string SharedStorageFolderName = "SharedStorage";
    public const string AudioFolderName = "Audio";
    public const string AudioRequestPath = "/shared-library/audio";

    public static string GetSharedLibraryRoot(string contentRootPath) =>
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "Project_SharedClassLibrary"));

    public static string GetAudioDirectory(string contentRootPath) =>
        Path.Combine(GetSharedLibraryRoot(contentRootPath), SharedStorageFolderName, AudioFolderName);

    public static string ToPublicAudioPath(string fileName) =>
        $"{AudioRequestPath}/{fileName.Replace("\\", "/")}";

    public static string? TryGetManagedAudioFileName(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return null;
        }

        var normalizedPath = publicPath.Replace("\\", "/");
        if (!normalizedPath.StartsWith(AudioRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetFileName(normalizedPath);
    }
}
