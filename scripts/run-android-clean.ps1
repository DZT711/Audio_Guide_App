$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "MauiApp_Mobile\MauiApp_Mobile.csproj"
$lpPath = Join-Path $repoRoot "MauiApp_Mobile\obj\Debug\net10.0-android\lp"
$binPath = Join-Path $repoRoot "MauiApp_Mobile\bin\Debug\net10.0-android"

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet-cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Write-Host "Shutting down build servers..."
dotnet build-server shutdown | Out-Host

if (Test-Path $lpPath) {
    Write-Host "Cleaning $lpPath"
    Remove-Item -LiteralPath $lpPath -Recurse -Force
}

if (Test-Path $binPath) {
    Write-Host "Cleaning $binPath"
    Remove-Item -LiteralPath $binPath -Recurse -Force
}

Write-Host "Starting Android app..."
dotnet run -f net10.0-android --project $projectPath
