[CmdletBinding()]
param(
    [int]$ApiPort = 5123,
    [string]$Configuration = "Debug",
    [string]$ApiProjectPath = "WebApplication_API/WebApplication_API.csproj",
    [string]$NgrokAuthtoken = $env:NGROK_AUTHTOKEN,
    [string]$NgrokDownloadUrl = "",
    [string]$AndroidApkFilePath = "",
    [string]$AndroidStoreUrl = "",
    [switch]$SkipApiStart,
    [switch]$SkipNgrok,
    [switch]$PreferLocalhostFallback
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$runtimeRoot = Join-Path $repoRoot ".runtime\smarttour-tunnel"
$toolsRoot = Join-Path $repoRoot ".tools\ngrok"
$apiHealthUrl = "http://127.0.0.1:$ApiPort/System/public/info"
$localApiBaseUrl = "http://localhost:$ApiPort/"
$apiBindUrl = "http://0.0.0.0:$ApiPort"
$report = [System.Collections.Generic.List[object]]::new()
$commands = [System.Collections.Generic.List[string]]::new()

New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null

function Write-Section {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Add-CommandLog {
    param([string]$CommandText)
    [void]$commands.Add($CommandText)
}

function Normalize-BaseUrl {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $null
    }

    $normalized = $Url.Trim()
    $uri = $null
    if (-not [Uri]::TryCreate($normalized, [UriKind]::Absolute, [ref]$uri)) {
        return $null
    }

    if ($uri.Scheme -notin @([Uri]::UriSchemeHttp, [Uri]::UriSchemeHttps)) {
        return $null
    }

    $builder = [UriBuilder]::new($uri)
    if ([string]::IsNullOrWhiteSpace($builder.Path)) {
        $builder.Path = "/"
    }
    elseif (-not $builder.Path.EndsWith("/")) {
        $builder.Path = "$($builder.Path)/"
    }

    $builder.Query = ""
    $builder.Fragment = ""
    return $builder.Uri.AbsoluteUri
}

function Join-BaseUrlAndPath {
    param(
        [string]$BaseUrl,
        [string]$RelativePath
    )

    return [Uri]::new([Uri]::new((Normalize-BaseUrl $BaseUrl)), $RelativePath.TrimStart('/')).AbsoluteUri
}

function Get-JsonDocument {
    param([string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return [pscustomobject]@{}
    }

    $raw = Get-Content -LiteralPath $FilePath -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return [pscustomobject]@{}
    }

    return $raw | ConvertFrom-Json
}

function Save-JsonDocument {
    param(
        [string]$FilePath,
        [psobject]$Document
    )

    $json = $Document | ConvertTo-Json -Depth 20
    Set-Content -LiteralPath $FilePath -Value $json
}

function Ensure-JsonObjectPath {
    param(
        [psobject]$Document,
        [string[]]$Segments
    )

    $current = $Document
    foreach ($segment in $Segments) {
        $property = $current.PSObject.Properties[$segment]
        if ($null -eq $property -or $null -eq $property.Value) {
            $child = [pscustomobject]@{}
            if ($null -eq $property) {
                $current | Add-Member -NotePropertyName $segment -NotePropertyValue $child
            }
            else {
                $current.$segment = $child
            }

            $current = $child
            continue
        }

        if ($property.Value -isnot [psobject]) {
            $child = [pscustomobject]@{}
            $current.$segment = $child
            $current = $child
            continue
        }

        $current = $property.Value
    }

    return $current
}

function Format-ReportValue {
    param($Value)

    if ($null -eq $Value) {
        return "<null>"
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        return (($Value | ConvertTo-Json -Depth 10 -Compress))
    }

    return [string]$Value
}

function Set-JsonValue {
    param(
        [string]$FilePath,
        [psobject]$Document,
        [string[]]$Segments,
        $Value
    )

    if ($Segments.Count -lt 1) {
        throw "Segments cannot be empty."
    }

    $parent = if ($Segments.Count -eq 1) {
        $Document
    }
    else {
        Ensure-JsonObjectPath -Document $Document -Segments $Segments[0..($Segments.Count - 2)]
    }

    $leaf = $Segments[-1]
    $existingProperty = $parent.PSObject.Properties[$leaf]
    $oldValue = if ($null -eq $existingProperty) { $null } else { $existingProperty.Value }

    $oldComparable = Format-ReportValue $oldValue
    $newComparable = Format-ReportValue $Value
    if ($oldComparable -eq $newComparable) {
        return
    }

    if ($null -eq $existingProperty) {
        $parent | Add-Member -NotePropertyName $leaf -NotePropertyValue $Value
    }
    else {
        $parent.$leaf = $Value
    }

    [void]$report.Add([pscustomobject]@{
        File = $FilePath
        Key = ($Segments -join ".")
        OldValue = $oldComparable
        NewValue = $newComparable
    })
}

function Test-ApiHealth {
    try {
        Invoke-RestMethod -Uri $apiHealthUrl -Method Get -TimeoutSec 3 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Wait-Until {
    param(
        [scriptblock]$Condition,
        [int]$TimeoutSeconds = 60,
        [int]$DelayMilliseconds = 1000
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) {
            return $true
        }

        Start-Sleep -Milliseconds $DelayMilliseconds
    }

    return $false
}

function Get-NgrokPath {
    $command = Get-Command ngrok -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $fallbackPath = Join-Path $toolsRoot "ngrok.exe"
    if (Test-Path -LiteralPath $fallbackPath) {
        return $fallbackPath
    }

    return $null
}

function Ensure-NgrokInstalled {
    $existingPath = Get-NgrokPath
    if ($null -ne $existingPath) {
        return $existingPath
    }

    Write-Section "Installing ngrok"
    $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        "arm64"
    }
    else {
        "amd64"
    }

    if (Get-Command winget -ErrorAction SilentlyContinue) {
        $commandText = "winget install --id ngrok.ngrok --accept-package-agreements --accept-source-agreements --silent"
        Add-CommandLog $commandText
        & winget install --id ngrok.ngrok --accept-package-agreements --accept-source-agreements --silent | Out-Null
        $existingPath = Get-NgrokPath
        if ($null -ne $existingPath) {
            return $existingPath
        }
    }

    if (Get-Command choco -ErrorAction SilentlyContinue) {
        $commandText = "choco install ngrok -y"
        Add-CommandLog $commandText
        & choco install ngrok -y | Out-Null
        $existingPath = Get-NgrokPath
        if ($null -ne $existingPath) {
            return $existingPath
        }
    }

    $downloadUrl = if ([string]::IsNullOrWhiteSpace($NgrokDownloadUrl)) {
        "https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-windows-$arch.zip"
    }
    else {
        $NgrokDownloadUrl.Trim()
    }

    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
    $zipPath = Join-Path $runtimeRoot "ngrok-windows-$arch.zip"

    Add-CommandLog "Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath

    Add-CommandLog "Expand-Archive -Path $zipPath -DestinationPath $toolsRoot -Force"
    Expand-Archive -Path $zipPath -DestinationPath $toolsRoot -Force

    $existingPath = Get-NgrokPath
    if ($null -eq $existingPath) {
        throw "ngrok installation completed but ngrok.exe was not found."
    }

    return $existingPath
}

function Test-NgrokAuthtokenConfigured {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "ngrok\ngrok.yml"),
        (Join-Path $env:USERPROFILE ".config\ngrok\ngrok.yml"),
        (Join-Path $env:USERPROFILE ".ngrok2\ngrok.yml")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (-not (Test-Path -LiteralPath $candidate)) {
            continue
        }

        $content = Get-Content -LiteralPath $candidate -Raw
        if ($content -match "authtoken\s*:\s*\S+") {
            return $true
        }
    }

    return $false
}

function Ensure-NgrokAuthtoken {
    param([string]$NgrokPath)

    if (Test-NgrokAuthtokenConfigured) {
        return $true
    }

    if ([string]::IsNullOrWhiteSpace($NgrokAuthtoken)) {
        Write-Warning "NGROK_AUTHTOKEN is not set. Falling back to localhost mode."
        return $false
    }

    Add-CommandLog "$NgrokPath config add-authtoken <redacted>"
    & $NgrokPath config add-authtoken $NgrokAuthtoken | Out-Null
    return $true
}

function Get-NgrokTunnels {
    try {
        return Invoke-RestMethod -Uri "http://127.0.0.1:4040/api/tunnels" -Method Get -TimeoutSec 3
    }
    catch {
        return $null
    }
}

function Get-NgrokPublicUrl {
    $tunnels = Get-NgrokTunnels
    if ($null -eq $tunnels -or $null -eq $tunnels.tunnels) {
        return $null
    }

    $expectedForwards = @(
        "http://127.0.0.1:$ApiPort",
        "http://localhost:$ApiPort",
        "localhost:$ApiPort",
        "127.0.0.1:$ApiPort"
    )

    $matchingTunnel = $tunnels.tunnels |
        Where-Object {
            $_.public_url -like "https://*" -and (
                $expectedForwards -contains $_.config.addr -or
                $expectedForwards -contains $_.forwards_to
            )
        } |
        Select-Object -First 1

    if ($null -eq $matchingTunnel) {
        $matchingTunnel = $tunnels.tunnels |
            Where-Object { $_.public_url -like "https://*" } |
            Select-Object -First 1
    }

    if ($null -eq $matchingTunnel) {
        return $null
    }

    return Normalize-BaseUrl $matchingTunnel.public_url
}

function Ensure-ApiRunning {
    if (Test-ApiHealth) {
        Write-Host "API is already healthy at $apiHealthUrl"
        return
    }

    if ($SkipApiStart) {
        throw "API is not running and -SkipApiStart was specified."
    }

    Write-Section "Starting WebApplication_API"
    $apiLog = Join-Path $runtimeRoot "api.log"
    $apiErrorLog = Join-Path $runtimeRoot "api.error.log"
    $apiProjectFullPath = Join-Path $repoRoot $ApiProjectPath
    $apiWorkingDirectory = Split-Path -Path $apiProjectFullPath -Parent
    $startupTimeoutSeconds = 90

    Add-CommandLog "dotnet run --project `"$ApiProjectFullPath`" --configuration $Configuration --no-launch-profile --urls $apiBindUrl"
    $apiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProjectFullPath, "--configuration", $Configuration, "--no-launch-profile", "--urls", $apiBindUrl) `
        -WorkingDirectory $apiWorkingDirectory `
        -RedirectStandardOutput $apiLog `
        -RedirectStandardError $apiErrorLog `
        -WindowStyle Hidden `
        -PassThru

    for ($elapsed = 0; $elapsed -lt $startupTimeoutSeconds; $elapsed++) {
        if (Test-ApiHealth) {
            Write-Host "API is healthy at $apiHealthUrl"
            return
        }

        if ($apiProcess.HasExited) {
            $stdoutTail = if (Test-Path -LiteralPath $apiLog) {
                (Get-Content -LiteralPath $apiLog -ErrorAction SilentlyContinue | Select-Object -Last 20) -join [Environment]::NewLine
            }
            else {
                ""
            }

            $stderrTail = if (Test-Path -LiteralPath $apiErrorLog) {
                (Get-Content -LiteralPath $apiErrorLog -ErrorAction SilentlyContinue | Select-Object -Last 20) -join [Environment]::NewLine
            }
            else {
                ""
            }

            throw "WebApplication_API exited early with code $($apiProcess.ExitCode). Stdout:`n$stdoutTail`nStderr:`n$stderrTail"
        }

        if (($elapsed % 10) -eq 0) {
            Write-Host "Waiting for API health... ${elapsed}s/${startupTimeoutSeconds}s"
        }

        Start-Sleep -Seconds 1
    }

    throw "WebApplication_API did not become healthy in $startupTimeoutSeconds seconds. Check $apiLog and $apiErrorLog."
}

function Ensure-NgrokTunnel {
    if ($SkipNgrok -or $PreferLocalhostFallback) {
        return $null
    }

    $ngrokPath = Ensure-NgrokInstalled
    Add-CommandLog "$ngrokPath version"
    & $ngrokPath version | Write-Host

    if (-not (Ensure-NgrokAuthtoken -NgrokPath $ngrokPath)) {
        return $null
    }

    $existingPublicUrl = Get-NgrokPublicUrl
    if ($null -ne $existingPublicUrl) {
        Write-Host "Reusing existing ngrok tunnel: $existingPublicUrl"
        return $existingPublicUrl
    }

    Write-Section "Starting ngrok tunnel"
    $ngrokLog = Join-Path $runtimeRoot "ngrok.log"
    $ngrokErrorLog = Join-Path $runtimeRoot "ngrok.error.log"
    Add-CommandLog "$ngrokPath http http://127.0.0.1:$ApiPort --log stdout"
    Start-Process -FilePath $ngrokPath `
        -ArgumentList @("http", "http://127.0.0.1:$ApiPort", "--log", "stdout") `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $ngrokLog `
        -RedirectStandardError $ngrokErrorLog `
        -WindowStyle Hidden | Out-Null

    if (-not (Wait-Until -Condition { $null -ne (Get-NgrokPublicUrl) } -TimeoutSeconds 45 -DelayMilliseconds 1500)) {
        throw "ngrok did not expose a public URL. Check $ngrokLog and $ngrokErrorLog."
    }

    return Get-NgrokPublicUrl
}

function Select-AndroidApkFile {
    if (-not [string]::IsNullOrWhiteSpace($AndroidApkFilePath)) {
        $fullPath = if ([System.IO.Path]::IsPathRooted($AndroidApkFilePath)) {
            [System.IO.Path]::GetFullPath($AndroidApkFilePath)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $repoRoot $AndroidApkFilePath))
        }

        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "Android APK file not found: $fullPath"
        }

        return $fullPath
    }

    $searchRoots = @(
        (Join-Path $repoRoot "MauiApp_Mobile\bin"),
        (Join-Path $repoRoot "artifacts"),
        (Join-Path $repoRoot "publish"),
        (Join-Path $repoRoot "drops")
    ) | Where-Object { Test-Path -LiteralPath $_ }

    foreach ($searchRoot in $searchRoots) {
        $apk = Get-ChildItem -Path $searchRoot -Recurse -Filter *.apk -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1

        if ($null -ne $apk) {
            return $apk.FullName
        }
    }

    return $null
}

function Publish-AndroidApk {
    param([string]$BaseUrl)

    $apkSourcePath = Select-AndroidApkFile
    if ($null -eq $apkSourcePath) {
        return $null
    }

    $downloadsDirectory = Join-Path $repoRoot "WebApplication_API\wwwroot\downloads"
    New-Item -ItemType Directory -Force -Path $downloadsDirectory | Out-Null

    $destinationFileName = "smarttour-latest.apk"
    $destinationPath = Join-Path $downloadsDirectory $destinationFileName
    Copy-Item -LiteralPath $apkSourcePath -Destination $destinationPath -Force
    return Join-BaseUrlAndPath -BaseUrl $BaseUrl -RelativePath "downloads/$destinationFileName"
}

function Rewrite-OriginIfNeeded {
    param(
        [string]$CurrentValue,
        [string]$TargetBaseUrl
    )

    if ([string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $null
    }

    $currentUri = $null
    if (-not [Uri]::TryCreate($CurrentValue.Trim(), [UriKind]::Absolute, [ref]$currentUri)) {
        return $CurrentValue
    }

    $shouldRewrite = $currentUri.Host -in @("localhost", "127.0.0.1", "0.0.0.0", "10.0.2.2") -or
        $currentUri.Host -like "*.ngrok.app" -or
        $currentUri.Host -like "*.ngrok-free.app"

    if (-not $shouldRewrite) {
        return $CurrentValue
    }

    $targetBaseUri = [Uri]::new((Normalize-BaseUrl $TargetBaseUrl))
    $builder = [UriBuilder]::new($targetBaseUri)
    $builder.Path = $currentUri.AbsolutePath
    $builder.Query = $currentUri.Query.TrimStart('?')
    return $builder.Uri.AbsoluteUri
}

function Update-ConfigurationFiles {
    param(
        [string]$ResolvedBaseUrl,
        [string]$AndroidApkUrlResolved,
        [string]$AndroidStoreUrlResolved
    )

    $apiConfigFiles = @(
        (Join-Path $repoRoot "WebApplication_API\appsettings.json"),
        (Join-Path $repoRoot "WebApplication_API\appsettings.Development.json")
    )

    foreach ($configFile in $apiConfigFiles) {
        $document = Get-JsonDocument -FilePath $configFile
        Set-JsonValue -FilePath $configFile -Document $document -Segments @("QrLinks", "PublicBaseUrl") -Value $ResolvedBaseUrl

        $currentAndroidApkUrl = $null
        if ($document.PSObject.Properties["QrLinks"] -and $document.QrLinks.PSObject.Properties["AndroidApkUrl"]) {
            $currentAndroidApkUrl = [string]$document.QrLinks.AndroidApkUrl
        }

        $rewrittenAndroidApkUrl = if ([string]::IsNullOrWhiteSpace($AndroidApkUrlResolved)) {
            Rewrite-OriginIfNeeded -CurrentValue $currentAndroidApkUrl -TargetBaseUrl $ResolvedBaseUrl
        }
        else {
            $AndroidApkUrlResolved
        }

        if ($null -ne $rewrittenAndroidApkUrl) {
            Set-JsonValue -FilePath $configFile -Document $document -Segments @("QrLinks", "AndroidApkUrl") -Value $rewrittenAndroidApkUrl
        }

        if (-not [string]::IsNullOrWhiteSpace($AndroidStoreUrlResolved)) {
            Set-JsonValue -FilePath $configFile -Document $document -Segments @("QrLinks", "AndroidStoreUrl") -Value $AndroidStoreUrlResolved
        }

        Save-JsonDocument -FilePath $configFile -Document $document
    }

    $adminConfigFiles = @(
        (Join-Path $repoRoot "BlazorApp_AdminWeb\appsettings.json"),
        (Join-Path $repoRoot "BlazorApp_AdminWeb\appsettings.Development.json")
    )

    foreach ($configFile in $adminConfigFiles) {
        $document = Get-JsonDocument -FilePath $configFile
        Set-JsonValue -FilePath $configFile -Document $document -Segments @("AdminApi", "BaseUrl") -Value $ResolvedBaseUrl
        Save-JsonDocument -FilePath $configFile -Document $document
    }

    $mobileConfigFile = Join-Path $repoRoot "MauiApp_Mobile\Resources\Raw\mobile-api.json"
    $mobileDocument = Get-JsonDocument -FilePath $mobileConfigFile
    Set-JsonValue -FilePath $mobileConfigFile -Document $mobileDocument -Segments @("BaseUrl") -Value $ResolvedBaseUrl
    Set-JsonValue -FilePath $mobileConfigFile -Document $mobileDocument -Segments @("PublicBaseUrl") -Value $ResolvedBaseUrl
    Set-JsonValue -FilePath $mobileConfigFile -Document $mobileDocument -Segments @("AllowLocalhostFallback") -Value $true
    Set-JsonValue -FilePath $mobileConfigFile -Document $mobileDocument -Segments @("FallbackBaseUrls") -Value @(
        "http://10.0.2.2:$ApiPort/",
        "http://localhost:$ApiPort/"
    )
    Save-JsonDocument -FilePath $mobileConfigFile -Document $mobileDocument
}

Write-Section "Environment"
if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "This setup script currently supports Windows only."
}

Write-Host "Repo root: $repoRoot"
Write-Host "API port: $ApiPort"

Ensure-ApiRunning

$publicBaseUrl = if ($PreferLocalhostFallback) {
    $localApiBaseUrl
}
else {
    Ensure-NgrokTunnel
}

if ([string]::IsNullOrWhiteSpace($publicBaseUrl)) {
    $publicBaseUrl = $localApiBaseUrl
    Write-Warning "Using localhost fallback because no ngrok public URL is available."
}

$publicBaseUrl = Normalize-BaseUrl $publicBaseUrl
$publishedAndroidApkUrl = Publish-AndroidApk -BaseUrl $publicBaseUrl
$androidStoreUrlResolved = if ([string]::IsNullOrWhiteSpace($AndroidStoreUrl)) { "" } else { $AndroidStoreUrl.Trim() }

$publicBaseUri = [Uri]::new($publicBaseUrl)
if ($publicBaseUri.Host -like "*.ngrok-free.dev" -or $publicBaseUri.Host -like "*.ngrok-free.app") {
    Write-Warning "This ngrok free URL may show the browser interstitial ERR_NGROK_6024 on first visit. API calls still work, but QR/browser landing flows are cleaner with a paid ngrok plan or a reserved/custom domain."
}

Update-ConfigurationFiles `
    -ResolvedBaseUrl $publicBaseUrl `
    -AndroidApkUrlResolved $publishedAndroidApkUrl `
    -AndroidStoreUrlResolved $androidStoreUrlResolved

Write-Section "Final URLs"
$sampleLandingUrl = Join-BaseUrlAndPath -BaseUrl $publicBaseUrl -RelativePath "LocationQr/public/location/1"
$catalogUrl = Join-BaseUrlAndPath -BaseUrl $publicBaseUrl -RelativePath "Location/public/catalog"

Write-Host "Public base URL : $publicBaseUrl"
Write-Host "Local API URL   : $localApiBaseUrl"
Write-Host "Catalog URL     : $catalogUrl"
Write-Host "Sample landing  : $sampleLandingUrl"
if (-not [string]::IsNullOrWhiteSpace($publishedAndroidApkUrl)) {
    Write-Host "APK URL         : $publishedAndroidApkUrl"
}
elseif (-not [string]::IsNullOrWhiteSpace($androidStoreUrlResolved)) {
    Write-Host "Store URL       : $androidStoreUrlResolved"
}
else {
    Write-Warning "No APK file was found and no AndroidStoreUrl was provided. Download page will stay available, but install targets still need to be configured."
}

Write-Section "Config updates"
if ($report.Count -eq 0) {
    Write-Host "No config changes were needed."
}
else {
    $report | Sort-Object File, Key | Format-Table -AutoSize
}

Write-Section "Commands executed"
if ($commands.Count -eq 0) {
    Write-Host "No external commands were executed by this run."
}
else {
    $commands | ForEach-Object { Write-Host $_ }
}
