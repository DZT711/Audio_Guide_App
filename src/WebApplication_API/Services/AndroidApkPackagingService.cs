using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Project_SharedClassLibrary.Constants;

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

    public string? ResolveConfiguredAndroidApkUrl() =>
        string.IsNullOrWhiteSpace(_options.AndroidApkUrl)
            ? null
            : _options.AndroidApkUrl.Trim();

    public string? ResolveConfiguredAndroidStoreUrl() =>
        string.IsNullOrWhiteSpace(_options.AndroidStoreUrl)
            ? null
            : _options.AndroidStoreUrl.Trim();

    public AndroidApkPackageResult? TryGetLatestLocalPackage(string publicBaseUrl)
    {
        var context = ResolveBuildContext(publicBaseUrl, requireProjectFile: false);
        var latestAliasPath = context.LatestAliasPath;
        var manifest = TryReadLatestManifest(context.LatestManifestPath);
        var localCandidatePath = File.Exists(latestAliasPath)
            ? latestAliasPath
            : FindLatestVersionedPackage(context.PackageDirectory);

        if (string.IsNullOrWhiteSpace(localCandidatePath))
        {
            return null;
        }

        var fileInfo = new FileInfo(localCandidatePath);
        if (!fileInfo.Exists || fileInfo.Length <= 0)
        {
            return null;
        }

        var downloadFileName = !string.IsNullOrWhiteSpace(manifest?.FileName)
            ? manifest!.FileName
            : Path.GetFileName(localCandidatePath);

        return new AndroidApkPackageResult(
            fileInfo.FullName,
            downloadFileName,
            "application/vnd.android.package-archive");
    }

    public int CleanupOldLocalPackages(string publicBaseUrl)
    {
        var context = ResolveBuildContext(publicBaseUrl, requireProjectFile: false);
        return CleanupOldPackages(context.PackageDirectory, context.LatestAliasPath, Math.Max(1, _options.AndroidPackageRetentionCount));
    }

    public async Task<AndroidApkPackageResult> GetOrBuildPackageAsync(
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsDynamicBuildEnabled)
        {
            throw new InvalidOperationException("Dynamic Android APK packaging is disabled.");
        }

        var context = ResolveBuildContext(publicBaseUrl, requireProjectFile: true);
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

        if (!File.Exists(context.LatestAliasPath) || !File.Exists(context.LegacyMetadataPath))
        {
            return false;
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<AndroidApkBuildMetadata>(
                File.ReadAllText(context.LegacyMetadataPath),
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
                context.LatestAliasPath,
                Path.GetFileName(context.LatestAliasPath),
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
        Directory.CreateDirectory(context.PackageDirectory);
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

            var buildTimeUtc = DateTime.UtcNow;
            var gitSha = ResolveGitShortSha(context.ProjectDirectory);
            var versionedFileName = BuildVersionedFileName(buildTimeUtc, gitSha);
            var versionedPackagePath = Path.Combine(context.PackageDirectory, versionedFileName);
            File.Copy(publishedApkPath, versionedPackagePath, overwrite: true);
            UpdateLatestAliasAtomically(versionedPackagePath, context.LatestAliasPath);

            var fileInfo = new FileInfo(versionedPackagePath);
            var payloadHash = ComputeSha256(versionedPackagePath);
            var relativeDownloadPath = BuildRelativePublicPath(versionedPackagePath);
            var normalizedBaseUrl = NormalizeBaseUrl(context.PublicBaseUrl);
            var downloadUrl = new Uri(new Uri(normalizedBaseUrl), relativeDownloadPath).AbsoluteUri;
            var version = $"{buildTimeUtc:yyyyMMdd-HHmm}-{gitSha}";

            var latestManifest = new AndroidApkLatestManifest
            {
                FileName = versionedFileName,
                Version = version,
                BuildTimeUtc = buildTimeUtc,
                GitSha = gitSha,
                Sha256 = payloadHash,
                FileSizeBytes = fileInfo.Length,
                DownloadUrl = downloadUrl
            };

            await File.WriteAllTextAsync(
                context.LatestManifestPath,
                JsonSerializer.Serialize(latestManifest, JsonOptions),
                cancellationToken);

            var metadata = new AndroidApkBuildMetadata
            {
                PublicBaseUrl = context.PublicBaseUrl,
                SourceLastWriteUtc = context.SourceLastWriteUtc,
                GeneratedAtUtc = DateTime.UtcNow,
                SourceApkPath = publishedApkPath
            };

            await File.WriteAllTextAsync(
                context.LegacyMetadataPath,
                JsonSerializer.Serialize(metadata, JsonOptions),
                cancellationToken);

            CleanupOldPackages(context.PackageDirectory, context.LatestAliasPath, Math.Max(1, _options.AndroidPackageRetentionCount));

            _logger.LogInformation(
                "Android APK packaged successfully for {PublicBaseUrl}. Latest alias: {LatestAliasPath}, versioned: {VersionedPackagePath}.",
                context.PublicBaseUrl,
                context.LatestAliasPath,
                versionedPackagePath);

            return new AndroidApkPackageResult(
                context.LatestAliasPath,
                versionedFileName,
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
        configuration.AllowLocalhostFallback = false;

        if (configuration.FallbackBaseUrls.Count == 0)
        {
            configuration.FallbackBaseUrls =
            [
                context.PublicBaseUrl
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

    private AndroidApkBuildContext ResolveBuildContext(string publicBaseUrl, bool requireProjectFile)
    {
        var contentRoot = _environment.ContentRootPath;
        var projectFilePath = ResolvePath(contentRoot, _options.AndroidProjectFilePath);
        if (requireProjectFile && !File.Exists(projectFilePath))
        {
            throw new FileNotFoundException("Android project file not found.", projectFilePath);
        }

        var mobileConfigFilePath = ResolvePath(contentRoot, _options.AndroidMobileConfigFilePath);
        var packagePath = ResolvePath(contentRoot, _options.AndroidPackageOutputRelativePath);
        var packageDirectory = Path.GetDirectoryName(packagePath)
                              ?? throw new InvalidOperationException("Android package output directory cannot be resolved.");
        var latestAliasFileName = string.IsNullOrWhiteSpace(_options.AndroidLatestAliasFileName)
            ? Path.GetFileName(packagePath)
            : _options.AndroidLatestAliasFileName.Trim();
        var latestAliasPath = ResolvePath(packageDirectory, latestAliasFileName);
        var latestManifestFileName = string.IsNullOrWhiteSpace(_options.AndroidLatestManifestFileName)
            ? "latest.json"
            : _options.AndroidLatestManifestFileName.Trim();
        var publishDirectory = Path.Combine(
            packageDirectory,
            "publish-cache");
        var metadataPath = Path.Combine(packageDirectory, "smarttour-latest.metadata.json");
        var latestManifestPath = Path.Combine(packageDirectory, latestManifestFileName);
        var sourceRoot = Path.GetDirectoryName(projectFilePath)!;
        var sourceLastWriteUtc = File.Exists(projectFilePath)
            ? GetSourceLastWriteUtc(sourceRoot)
            : DateTime.MinValue;

        return new AndroidApkBuildContext(
            NormalizeBaseUrl(publicBaseUrl),
            projectFilePath,
            Path.GetDirectoryName(projectFilePath)!,
            mobileConfigFilePath,
            packageDirectory,
            latestAliasPath,
            publishDirectory,
            metadataPath,
            latestManifestPath,
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

    private static string? FindLatestVersionedPackage(string packageDirectory)
    {
        if (!Directory.Exists(packageDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(packageDirectory, "smarttour-*.apk", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "smarttour-latest.apk", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private AndroidApkLatestManifest? TryReadLatestManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AndroidApkLatestManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to parse Android latest manifest at {ManifestPath}.", manifestPath);
            return null;
        }
    }

    private static void UpdateLatestAliasAtomically(string sourcePath, string latestAliasPath)
    {
        var latestDirectory = Path.GetDirectoryName(latestAliasPath)
                              ?? throw new InvalidOperationException("Latest alias directory is invalid.");
        Directory.CreateDirectory(latestDirectory);

        var tempPath = Path.Combine(latestDirectory, $"{Path.GetFileName(latestAliasPath)}.tmp-{Guid.NewGuid():N}");
        File.Copy(sourcePath, tempPath, overwrite: true);

        if (File.Exists(latestAliasPath))
        {
            File.Replace(tempPath, latestAliasPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, latestAliasPath);
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var hash = SHA256.Create();
        var payload = hash.ComputeHash(stream);
        return Convert.ToHexString(payload).ToLowerInvariant();
    }

    private string BuildRelativePublicPath(string absoluteFilePath)
    {
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var relativePath = Path.GetRelativePath(webRoot, absoluteFilePath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            return ApiRoutes.PublicAndroidApkDownload.TrimStart('/');
        }

        return relativePath.Replace('\\', '/');
    }

    private static string BuildVersionedFileName(DateTime buildTimeUtc, string gitSha) =>
        $"smarttour-{buildTimeUtc:yyyyMMdd-HHmm}-{gitSha}.apk";

    private static string ResolveGitShortSha(string workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("rev-parse");
            startInfo.ArgumentList.Add("--short=8");
            startInfo.ArgumentList.Add("HEAD");

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            if (!process.WaitForExit(1500))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return "nogit";
            }

            if (process.ExitCode != 0)
            {
                return "nogit";
            }

            var value = process.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "nogit";
            }

            var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return string.IsNullOrWhiteSpace(normalized)
                ? "nogit"
                : normalized[..Math.Min(8, normalized.Length)];
        }
        catch
        {
            return "nogit";
        }
    }

    private static int CleanupOldPackages(string packageDirectory, string latestAliasPath, int retentionCount)
    {
        if (!Directory.Exists(packageDirectory))
        {
            return 0;
        }

        var latestAliasName = Path.GetFileName(latestAliasPath);
        var candidates = Directory.EnumerateFiles(packageDirectory, "smarttour-*.apk", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), latestAliasName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        var removed = 0;
        foreach (var stalePath in candidates.Skip(retentionCount))
        {
            try
            {
                File.Delete(stalePath);
                removed++;
            }
            catch
            {
            }
        }

        return removed;
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
        string PackageDirectory,
        string LatestAliasPath,
        string PublishDirectory,
        string LegacyMetadataPath,
        string LatestManifestPath,
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

    private sealed class AndroidApkLatestManifest
    {
        public string FileName { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public DateTime BuildTimeUtc { get; set; }

        public string GitSha { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public string DownloadUrl { get; set; } = string.Empty;
    }
}
