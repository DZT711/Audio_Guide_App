using Project_SharedClassLibrary.Storage;

namespace WebApplication_API.Services;

public sealed class SharedImageFileStorageService
{
    private readonly string _imageDirectory;

    public SharedImageFileStorageService(IWebHostEnvironment environment)
    {
        _imageDirectory = SharedStoragePaths.GetImageDirectory(environment.ContentRootPath);
        Directory.CreateDirectory(_imageDirectory);
    }

    public async Task<string> SaveImageAsync(
        IFormFile imageFile,
        string locationName,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_imageDirectory);

        var extension = Path.GetExtension(imageFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            extension = ".bin";
        }

        var fileName = $"{SanitizeSegment(locationName)}-{sortOrder:D2}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(_imageDirectory, fileName);

        await using var targetStream = File.Create(fullPath);
        await imageFile.CopyToAsync(targetStream, cancellationToken);

        return SharedStoragePaths.ToPublicImagePath(fileName);
    }

    public void DeleteIfManaged(string? publicPath)
    {
        var fileName = SharedStoragePaths.TryGetManagedImageFileName(publicPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var fullPath = Path.Combine(_imageDirectory, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "location";
        }

        var sanitizedChars = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        var sanitized = new string(sanitizedChars);
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "location" : sanitized;
    }
}
