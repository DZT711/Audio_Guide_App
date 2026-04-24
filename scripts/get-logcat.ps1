param(
    [string]$OutputDir = "artifacts\adb-logs",
    [string]$Serial,
    [switch]$ClearBeforeStart,
    [switch]$WaitForDevice,
    [int]$WaitTimeoutSec = 60                                                         
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-AdbCommand {
    $adbCmd = Get-Command adb -ErrorAction SilentlyContinue
    if (-not $adbCmd) {
        throw "adb was not found in PATH. Install Android platform-tools and ensure adb is available."
    }
    return $adbCmd.Source
}

function Get-AdbArgs {
    param(
        [string]$SerialValue,
        [string[]]$CommandArgs
    )

    if ([string]::IsNullOrWhiteSpace($SerialValue)) {
        return $CommandArgs
    }

    return @("-s", $SerialValue) + $CommandArgs
}

function Get-AdbDevices {
    param([string]$AdbPath)

    $output = & $AdbPath devices
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to query adb devices."
    }

    $devices = @()
    foreach ($line in $output) {
        if ($line -match "^(List of devices attached|\s*$)") {
            continue
        }

        if ($line -match "^(?<serial>\S+)\s+(?<state>\S+)$") {
            $devices += [PSCustomObject]@{
                Serial = $Matches["serial"]
                State  = $Matches["state"]
            }
        }
    }

    return $devices
}

function Format-DeviceList {
    param([object[]]$Devices)

    if (-not $Devices -or $Devices.Count -eq 0) {
        return "No entries from 'adb devices'."
    }

    return ($Devices | ForEach-Object { "$($_.Serial) [$($_.State)]" }) -join "; "
}

try {
    $adb = Get-AdbCommand
    $projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

    if ($WaitTimeoutSec -lt 1) {
        throw "WaitTimeoutSec must be greater than 0."
    }

    $resolvedOutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) {
        $OutputDir
    }
    else {
        Join-Path $projectRoot $OutputDir
    }

    if (-not (Test-Path -LiteralPath $resolvedOutputDir)) {
        New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null
    }

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $logFile = Join-Path $resolvedOutputDir "logcat-$timestamp.txt"

    $startServerArgs = Get-AdbArgs -SerialValue $Serial -CommandArgs @("start-server")
    & $adb @startServerArgs | Out-Null

    $deadline = (Get-Date).AddSeconds($WaitTimeoutSec)
    $deviceReady = $false

    while (-not $deviceReady) {
        $devices = Get-AdbDevices -AdbPath $adb
        $readyDevices = @($devices | Where-Object { $_.State -eq "device" })

        if ([string]::IsNullOrWhiteSpace($Serial)) {
            if ($readyDevices.Count -eq 1) {
                $Serial = $readyDevices[0].Serial
                $deviceReady = $true
            }
            elseif ($readyDevices.Count -gt 1) {
                throw "Multiple ready devices found: $(Format-DeviceList -Devices $readyDevices). Use -Serial to choose one."
            }
        }
        else {
            $selected = @($devices | Where-Object { $_.Serial -eq $Serial })
            if ($selected.Count -gt 0 -and $selected[0].State -eq "device") {
                $deviceReady = $true
            }
        }

        if ($deviceReady) { break }
        if (-not $WaitForDevice -or (Get-Date) -ge $deadline) {
            $hint = if ([string]::IsNullOrWhiteSpace($Serial)) {
                "No ready Android device found."
            }
            else {
                "Device '$Serial' is not ready."
            }
            throw "$hint Current adb devices: $(Format-DeviceList -Devices $devices)"
        }

        Start-Sleep -Seconds 2
    }

    if ($ClearBeforeStart) {
        Write-Host "Clearing existing logcat buffer..."
        $clearArgs = Get-AdbArgs -SerialValue $Serial -CommandArgs @("logcat", "-c")
        & $adb @clearArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to clear logcat buffer."
        }
    }

    Write-Host "Collecting adb logcat..."
    Write-Host "Using device: $Serial"
    Write-Host "Output file: $logFile"
    Write-Host "Press Ctrl+C to stop."

    $logcatArgs = Get-AdbArgs -SerialValue $Serial -CommandArgs @("logcat", "-v", "threadtime")
    & $adb @logcatArgs | Tee-Object -FilePath $logFile
}
catch {
    Write-Error $_
    exit 1
}
