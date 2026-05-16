using Project_SharedClassLibrary.Storage;

namespace WebApplication_API.Services;

public sealed class ManagedMediaArchiveMigrationService(
    IWebHostEnvironment environment)
{
    public string AudioDirectory => SharedStoragePaths.GetAudioDirectory(environment.ContentRootPath);

    public string ImageDirectory => SharedStoragePaths.GetImageDirectory(environment.ContentRootPath);

    public void EnsureArchiveIsReady()
    {
        Directory.CreateDirectory(AudioDirectory);
        Directory.CreateDirectory(ImageDirectory);
    }
}
