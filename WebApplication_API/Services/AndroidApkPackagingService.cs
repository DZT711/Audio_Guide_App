using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WebApplication_API.Services;

public sealed class AndroidApkPackagingService(
    IOptions<QrLinkOptions> options,
    IWebHostEnvironment environment,
    ILogger<AndroidApkPackagingService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly QrLinkOptions _options = options.Value;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<AndroidApkPackagingService> _logger = logger;
    private readonly SemaphoreSlim _buildSemaphore = new(1, 1);

    public bool IsDynamicBuildEnabled => _options.EnableDynamicAndroidApkBuild;

    public async Task<AndroidApkPackageResult> GetOrBuildPackageAsync(
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsDynamicBuildEnabled)
        {
            throw new InvalidOperationException("Dynamic Android APK packaging is disabled.");
        }

        var context = ResolveBuildContext(publicBaseUrl);
        if (TryUseCachedPackage(context, out var cachedPackage))
        {
            return cachedPackage;
        }

        await _buildSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (TryUseCachedPackage(context, out cachedPackage))
            {
                return cachedPackage;
            }

            return await BuildPackageAsync(context, cancellationToken);
        }
        finally
        {
            _buildSemaphore.Release();
        }
    }

    private bool TryUseCachedPackage(AndroidApkBuildContext context, out AndroidApkPackageResult package)
    {
        package = default!;

        if (!File.Exists(context.PackagePath) || !File.Exists(context.MetadataPath))
        {
            return false;
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<AndroidApkBuildMetadata>(
                File.ReadAllText(context.MetadataPath),
                JsonOptions);

            if (metadata is null)
            {
                return false;
            }

            if (!string.Equals(metadata.PublicBaseUrl, context.PublicBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (metadata.SourceLastWriteUtc < context.SourceLastWriteUtc)
            {
                return false;
            }

            package = new AndroidApkPackageResult(
                context.PackagePath,
                context.DownloadFileName,
                "application/vnd.android.package-archive");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read cached Android APK metadata.");
            return false;
        }
    }

    private async Task<AndroidApkPackageResult> BuildPackageAsync(
        AndroidApkBuildContext context,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(context.PackagePath)!);
        Directory.CreateDirectory(context.PublishDirectory);

        var originalMobileConfig = File.Exists(context.MobileConfigFilePath)
            ? await File.ReadAllTextAsync(context.MobileConfigFilePath, cancellationToken)
            : null;
        var originalMobileConfigWriteTimeUtc = File.Exists(context.MobileConfigFilePath)
            ? File.GetLastWriteTimeUtc(context.MobileConfigFilePath)
            : DateTime.MinValue;

        try
        {
            await WritePatchedMobileConfigurationAsync(context, cancellationToken);
            await RunPublishAsync(context, cancellationToken);

            var publishedApkPath = FindLatestApk(context.PublishDirectory, context.ProjectDirectory);
            if (publishedApkPath is null)
            {
                throw new InvalidOperationException("The Android publish completed, but no APK file was produced.");
            }

            File.Copy(publishedApkPath, context.PackagePath, overwrite: true);
            var metadata = new AndroidApkBuildMetadata
            {
                PublicBaseUrl = context.PublicBaseUrl,
                SourceLastWriteUtc = context.SourceLastWriteUtc,
                GeneratedAtUtc = DateTime.UtcNow,
                SourceApkPath = publishedApkPath
            };

            await File.WriteAllTextAsync(
                context.MetadataPath,
                JsonSerializer.Serialize(metadata, JsonOptions),
                cancellationToken);

            _logger.LogInformation(
                "Android APK packaged successfully for {PublicBaseUrl} at {PackagePath}.",
                context.PublicBaseUrl,
                context.PackagePath);

            return new AndroidApkPackageResult(
                context.PackagePath,
                context.DownloadFileName,
                "application/vnd.android.package-archive");
        }
        finally
        {
            if (originalMobileConfig is not null)
            {
                await File.WriteAllTextAsync(context.MobileConfigFilePath, originalMobileConfig, cancellationToken);
                File.SetLastWriteTimeUtc(context.MobileConfigFilePath, originalMobileConfigWriteTimeUtc);
            }
        }
    }

    private async Task WritePatchedMobileConfigurationAsync(
        AndroidApkBuildContext context,
        CancellationToken cancellationToken)
    {
        AndroidMobileApiConfiguration configuration;
        if (File.Exists(context.MobileConfigFilePath))
        {
            await using var stream = File.OpenRead(context.MobileConfigFilePath);
            configuration = await JsonSerializer.DeserializeAsync<AndroidMobileApiConfiguration>(
                                stream,
                                JsonOptions,
                                cancellationToken)
                            ?? new AndroidMobileApiConfiguration();
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(context.MobileConfigFilePath)!);
            configuration = new AndroidMobileApiConfiguration();
        }

        configuration.BaseUrl = context.PublicBaseUrl;
        configuration.PublicBaseUrl = context.PublicBaseUrl;
        configuration.AllowLocalhostFallback = true;

        if (configuration.FallbackBaseUrls.Count == 0)
        {
            configuration.FallbackBaseUrls =
            [
                "http://10.0.2.2:5123/",
                "http://localhost:5123/"
            ];
        }

        await File.WriteAllTextAsync(
            context.MobileConfigFilePath,
            JsonSerializer.Serialize(configuration, JsonOptions),
            cancellationToken);
    }

    private async Task RunPublishAsync(AndroidApkBuildContext context, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = context.ProjectDirectory
        };

        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(context.ProjectFilePath);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(_options.AndroidTargetFramework);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(_options.AndroidBuildConfiguration);
        startInfo.ArgumentList.Add("-p:AndroidPackageFormats=" + _options.AndroidPackageFormat);
        startInfo.ArgumentList.Add("-p:EmbedAssembliesIntoApk=true");
        startInfo.ArgumentList.Add("-p:PublishDir=" + EnsureTrailingDirectorySeparator(context.PublishDirectory));

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(Math.Max(60, _options.AndroidBuildTimeoutSeconds)));

        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            throw new InvalidOperationException("Android APK packaging timed out.");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode == 0)
        {
            _logger.LogInformation("Android APK publish completed successfully. {Output}", standardOutput);
            return;
        }

        _logger.LogError(
            "Android APK publish failed with exit code {ExitCode}. Output: {Output} Error: {Error}",
            process.ExitCode,
            standardOutput,
            standardError);

        var message = string.IsNullOrWhiteSpace(standardError)
            ? standardOutput
            : standardError;
        throw new InvalidOperationException($"Android APK packaging failed: {message.Trim()}");
    }

    private AndroidApkBuildContext ResolveBuildContext(string publicBaseUrl)
    {
        var contentRoot = _environment.ContentRootPath;
        var projectFilePath = ResolvePath(contentRoot, _options.AndroidProjectFilePath);
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException("Android project file not found.", projectFilePath);
        }

        var mobileConfigFilePath = ResolvePath(contentRoot, _options.AndroidMobileConfigFilePath);
        var packagePath = ResolvePath(contentRoot, _options.AndroidPackageOutputRelativePath);
        var publishDirectory = Path.Combine(
            Path.GetDirectoryName(packagePath)!,
            "publish-cache");
        var metadataPath = Path.Combine(
            Path.GetDirectoryName(packagePath)!,
            Path.GetFileNameWithoutExtension(packagePath) + ".metadata.json");
        var sourceRoot = Path.GetDirectoryName(projectFilePath)!;
        var sourceLastWriteUtc = GetSourceLastWriteUtc(sourceRoot);

        return new AndroidApkBuildContext(
            NormalizeBaseUrl(publicBaseUrl),
            projectFilePath,
            Path.GetDirectoryName(projectFilePath)!,
            mobileConfigFilePath,
            packagePath,
            publishDirectory,
            metadataPath,
            Path.GetFileName(packagePath),
            sourceLastWriteUtc);
    }

    private static DateTime GetSourceLastWriteUtc(string sourceRoot)
    {
        var latestWriteUtc = File.GetLastWriteTimeUtc(sourceRoot);
        foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, filePath);
            if (relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith(".verify" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("binverify" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("objverify" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            if (lastWriteUtc > latestWriteUtc)
            {
                latestWriteUtc = lastWriteUtc;
            }
        }

        return latestWriteUtc;
    }

    private static string? FindLatestApk(string publishDirectory, string projectDirectory)
    {
        var searchRoots = new[]
        {
            publishDirectory,
            Path.Combine(projectDirectory, "bin")
        };

        return searchRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.apk", SearchOption.AllDirectories))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string EnsureTrailingDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string ResolvePath(string contentRoot, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(contentRoot, configuredPath));
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid public base URL for Android packaging: {baseUrl}");
        }

        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri.AbsoluteUri
            : uri.AbsoluteUri + "/";
    }

    public sealed record AndroidApkPackageResult(
        string PhysicalPath,
        string DownloadFileName,
        string ContentType);

    private sealed record AndroidApkBuildContext(
        string PublicBaseUrl,
        string ProjectFilePath,
        string ProjectDirectory,
        string MobileConfigFilePath,
        string PackagePath,
        string PublishDirectory,
        string MetadataPath,
        string DownloadFileName,
        DateTime SourceLastWriteUtc);

    private sealed class AndroidMobileApiConfiguration
    {
        public string? BaseUrl { get; set; }

        public string? PublicBaseUrl { get; set; }

        public bool AllowLocalhostFallback { get; set; } = true;

        public List<string> FallbackBaseUrls { get; set; } = [];
    }

    private sealed class AndroidApkBuildMetadata
    {
        public string PublicBaseUrl { get; set; } = string.Empty;

        public DateTime SourceLastWriteUtc { get; set; }

        public DateTime GeneratedAtUtc { get; set; }

        public string SourceApkPath { get; set; } = string.Empty;
    }
}
