param(
    [string]$ProjectPath = ".\MauiApp_Mobile\MauiApp_Mobile.csproj",
    [string]$Configuration = "Debug",
    [string]$TargetFramework = "net10.0-android",
    [string]$RuntimeIdentifier = "android-arm64",
    [string]$PublicBaseUrl = "https://unearth-ranked-viper.ngrok-free.dev/",
    [string]$DownloadsDir = ".\WebApplication_API\wwwroot\downloads",
    [string]$PublishCacheDir = ".\WebApplication_API\wwwroot\downloads\publish-cache",
    [int]$KeepLatest = 5,
    [switch]$SkipPublishCacheSync,
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    $repoRoot = Split-Path -Parent $PSScriptRoot
    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PathValue))
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectFullPath = Resolve-RepoPath -PathValue $ProjectPath
$downloadsFullPath = Resolve-RepoPath -PathValue $DownloadsDir
$publishCacheFullPath = Resolve-RepoPath -PathValue $PublishCacheDir
$registerScriptPath = Join-Path $PSScriptRoot "register-android-apk-artifact.ps1"
$publishDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ".artifacts\android-publish\$Configuration"))
$publishDirForMsBuild = if ($publishDir.EndsWith("\", [StringComparison]::Ordinal) -or $publishDir.EndsWith("/", [StringComparison]::Ordinal)) {
    $publishDir
}
else {
    "$publishDir\"
}

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

if (-not (Test-Path -LiteralPath $registerScriptPath)) {
    throw "Missing script: $registerScriptPath"
}

Write-Host "Project:" $projectFullPath
Write-Host "Configuration:" $Configuration
Write-Host "Target framework:" $TargetFramework
Write-Host "Runtime identifier:" $RuntimeIdentifier
Write-Host "Publish dir:" $publishDir

if (-not $SkipRestore) {
    Write-Host "Running restore..."
    dotnet restore $projectFullPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing Android APK..."
dotnet publish $projectFullPath `
    -f $TargetFramework `
    -c $Configuration `
    -p:RuntimeIdentifier=$RuntimeIdentifier `
    -p:AndroidPackageFormats=apk `
    -p:EmbedAssembliesIntoApk=true `
    -p:PublishDir=$publishDirForMsBuild

$publishExitCode = $LASTEXITCODE
$projectDirectory = Split-Path -Parent $projectFullPath
$fallbackBinDir = Join-Path $projectDirectory ("bin\{0}\{1}" -f $Configuration, $TargetFramework)

$apkCandidates = @()
if (Test-Path -LiteralPath $publishDir) {
    $apkCandidates += Get-ChildItem -Path $publishDir -Filter *.apk -Recurse -ErrorAction SilentlyContinue |
        Where-Object { -not $_.PSIsContainer }
}

if (Test-Path -LiteralPath $fallbackBinDir) {
    $apkCandidates += Get-ChildItem -Path $fallbackBinDir -Filter *.apk -Recurse -ErrorAction SilentlyContinue |
        Where-Object { -not $_.PSIsContainer }
}

$apk = $apkCandidates |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $apk) {
    if ($publishExitCode -ne 0) {
        throw "dotnet publish failed with exit code $publishExitCode and no APK artifact was produced."
    }

    throw "No APK artifact found in publish output: $publishDir"
}

if ($publishExitCode -ne 0) {
    Write-Warning "dotnet publish returned exit code $publishExitCode, but an APK was produced. Continuing with the newest APK artifact."
}

Write-Host "Newest APK:" $apk.FullName

if (-not $SkipPublishCacheSync) {
    New-Item -ItemType Directory -Force -Path $publishCacheFullPath | Out-Null

    $signedTarget = Join-Path $publishCacheFullPath "com.companyname.mauiapp_mobile-Signed.apk"
    Copy-Item -LiteralPath $apk.FullName -Destination $signedTarget -Force

    $unsignedCandidate = $apk.FullName -replace '-Signed\.apk$', '.apk'
    if (Test-Path -LiteralPath $unsignedCandidate) {
        Copy-Item -LiteralPath $unsignedCandidate -Destination (Join-Path $publishCacheFullPath "com.companyname.mauiapp_mobile.apk") -Force
    }

    Write-Host "Synced publish-cache:" $publishCacheFullPath
}

& $registerScriptPath `
    -ApkPath $apk.FullName `
    -PublicBaseUrl $PublicBaseUrl `
    -DownloadsDir $downloadsFullPath `
    -KeepLatest $KeepLatest

Write-Host "Done. Output folder:" $downloadsFullPath
