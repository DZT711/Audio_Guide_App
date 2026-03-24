using Project_SharedClassLibrary.Storage;

namespace WebApplication_API.Services;

public sealed class SharedAudioFileStorageService
{
    private readonly string _audioDirectory;

    public SharedAudioFileStorageService(IWebHostEnvironment environment)
    {
        _audioDirectory = SharedStoragePaths.GetAudioDirectory(environment.ContentRootPath);
        Directory.CreateDirectory(_audioDirectory);
    }

    public string AudioDirectory => _audioDirectory;

    public async Task<string> SaveAudioAsync(
        IFormFile audioFile,
        string locationName,
        string title,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_audioDirectory);

        var extension = Path.GetExtension(audioFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            extension = ".bin";
        }

        var fileName = $"{SanitizeSegment(locationName)}-{SanitizeSegment(title)}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(_audioDirectory, fileName);

        await using var targetStream = File.Create(fullPath);
        await audioFile.CopyToAsync(targetStream, cancellationToken);

        return SharedStoragePaths.ToPublicAudioPath(fileName);
    }

    public void DeleteIfManaged(string? publicPath)
    {
        var fileName = SharedStoragePaths.TryGetManagedAudioFileName(publicPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var fullPath = Path.Combine(_audioDirectory, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "audio";
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
        return string.IsNullOrWhiteSpace(sanitized) ? "audio" : sanitized;
    }
}
