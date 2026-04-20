param(
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,
    [string]$PublicBaseUrl = "https://example.com/",
    [string]$DownloadsDir = ".\WebApplication_API\wwwroot\downloads",
    [int]$KeepLatest = 5,
    [string]$GitSha = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ApkPath)) {
    throw "APK not found: $ApkPath"
}

New-Item -ItemType Directory -Force -Path $DownloadsDir | Out-Null

if ([string]::IsNullOrWhiteSpace($GitSha)) {
    try {
        $GitSha = (git rev-parse --short=8 HEAD).Trim()
    }
    catch {
        $GitSha = "nogit"
    }
}

$buildUtc = [DateTime]::UtcNow
$fileName = "smarttour-{0}-{1}.apk" -f $buildUtc.ToString("yyyyMMdd-HHmm"), $GitSha
$versionedPath = Join-Path $DownloadsDir $fileName
$latestPath = Join-Path $DownloadsDir "smarttour-latest.apk"
$manifestPath = Join-Path $DownloadsDir "latest.json"

Copy-Item -LiteralPath $ApkPath -Destination $versionedPath -Force

$tempLatest = "$latestPath.tmp-$([Guid]::NewGuid().ToString('N'))"
Copy-Item -LiteralPath $versionedPath -Destination $tempLatest -Force
if (Test-Path -LiteralPath $latestPath) {
    [System.IO.File]::Replace($tempLatest, $latestPath, $null, $true)
}
else {
    Move-Item -LiteralPath $tempLatest -Destination $latestPath -Force
}

$sha256 = (Get-FileHash -LiteralPath $versionedPath -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item -LiteralPath $versionedPath).Length
$normalizedBase = $PublicBaseUrl.TrimEnd("/") + "/"
$downloadUrl = "$normalizedBase" + "downloads/" + $fileName

$manifest = [ordered]@{
    fileName = $fileName
    version = "$($buildUtc.ToString("yyyyMMdd-HHmm"))-$GitSha"
    buildTimeUtc = $buildUtc.ToString("o")
    gitSha = $GitSha
    sha256 = $sha256
    fileSizeBytes = $size
    downloadUrl = $downloadUrl
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$packages = Get-ChildItem -LiteralPath $DownloadsDir -Filter "smarttour-*.apk" |
    Where-Object { $_.Name -ne "smarttour-latest.apk" } |
    Sort-Object LastWriteTimeUtc -Descending

$remove = $packages | Select-Object -Skip ([Math]::Max(1, $KeepLatest))
foreach ($item in $remove) {
    Remove-Item -LiteralPath $item.FullName -Force
}

Write-Host "Registered APK artifact: $fileName"
