param(
    [string]$ConfigPath,
    [string]$MobileApiConfigPath,
    [int]$Port = 5123,
    [string]$NgrokBaseUrl = "https://squander-neurology-overhead.ngrok-free.dev"
)

$ErrorActionPreference = "Stop"

function Get-PreferredLocalIpv4 {
    $isPrivateIpv4 = {
        param([string]$Address)
        if ([string]::IsNullOrWhiteSpace($Address)) {
            return $false
        }

        $parts = $Address.Split(".")
        if ($parts.Count -ne 4) {
            return $false
        }

        $a = [int]$parts[0]
        $b = [int]$parts[1]
        return ($a -eq 10) -or ($a -eq 192 -and $b -eq 168) -or ($a -eq 172 -and $b -ge 16 -and $b -le 31)
    }

    try {
        $route = Get-NetRoute -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue |
        Sort-Object RouteMetric |
        Select-Object -First 1

        if ($route) {
            $ip = Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue |
            Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
            Select-Object -First 1 -ExpandProperty IPAddress

            if (-not [string]::IsNullOrWhiteSpace($ip)) {
                return $ip
            }
        }

        $ip = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
        Select-Object -First 1 -ExpandProperty IPAddress

        if (-not [string]::IsNullOrWhiteSpace($ip)) {
            return $ip
        }
    }
    catch {
    }

    try {
        $ipconfigOutput = ipconfig
        foreach ($line in $ipconfigOutput) {
            if ($line -match "IPv4[^\d]*([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)") {
                $candidate = $matches[1]
                if ($candidate -notlike "127.*" -and $candidate -notlike "169.254.*" -and (& $isPrivateIpv4 $candidate)) {
                    return $candidate
                }
            }
        }
    }
    catch {
    }

    try {
        $hostAddresses = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName())
        foreach ($address in $hostAddresses) {
            if ($address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
                continue
            }

            $candidate = $address.IPAddressToString
            if ($candidate -notlike "127.*" -and $candidate -notlike "169.254.*" -and (& $isPrivateIpv4 $candidate)) {
                return $candidate
            }
        }
    }
    catch {
    }

    return ""
}

function Normalize-Url {
    param([string]$Url)

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $null
    }

    $trimmed = $Url.Trim()
    if (-not $trimmed.EndsWith("/")) {
        $trimmed = "$trimmed/"
    }

    return $trimmed
}

function Update-NetworkSecurityConfig {
    param(
        [string]$Path,
        [string]$LocalIp
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        -not (Test-Path -LiteralPath $Path) -or
        [string]::IsNullOrWhiteSpace($LocalIp)) {
        return
    }

    $content = Get-Content -LiteralPath $Path -Raw
    $autoBlock = @(
        "    <!-- AUTO_LOCAL_IPV4_START -->"
    )

    if (-not [string]::IsNullOrWhiteSpace($LocalIp)) {
        $autoBlock += "    <domain includeSubdomains=""false"">$LocalIp</domain>"
    }

    $autoBlock += "    <!-- AUTO_LOCAL_IPV4_END -->"
    $replacement = [string]::Join([Environment]::NewLine, $autoBlock)
    $pattern = "(?s)\s*<!-- AUTO_LOCAL_IPV4_START -->.*?<!-- AUTO_LOCAL_IPV4_END -->"
    $updated = [regex]::Replace($content, $pattern, [Environment]::NewLine + $replacement)

    if ($updated -ne $content) {
        Set-Content -LiteralPath $Path -Value $updated -Encoding UTF8
    }
}

function Ensure-Property {
    param(
        [psobject]$Object,
        [string]$Name,
        $DefaultValue
    )

    if (-not ($Object.PSObject.Properties.Name -contains $Name)) {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $DefaultValue
    }
}

function Update-MobileApiConfig {
    param(
        [string]$Path,
        [string]$LocalIp,
        [int]$ApiPort,
        [string]$PrimaryBaseUrl
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        -not (Test-Path -LiteralPath $Path)) {
        return
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    $config = $null

    try {
        $config = $raw | ConvertFrom-Json
    }
    catch {
        $config = [pscustomobject]@{}
    }

    Ensure-Property -Object $config -Name "baseUrl" -DefaultValue ""
    Ensure-Property -Object $config -Name "publicBaseUrl" -DefaultValue ""
    Ensure-Property -Object $config -Name "allowLocalhostFallback" -DefaultValue $true
    Ensure-Property -Object $config -Name "fallbackBaseUrls" -DefaultValue @()

    $resolvedLocalBaseUrl = Normalize-Url -Url "http://$($LocalIp):$ApiPort"
    $resolvedNgrokBaseUrl = Normalize-Url -Url $PrimaryBaseUrl

    if (-not [string]::IsNullOrWhiteSpace($resolvedNgrokBaseUrl)) {
        # Prefer ngrok as primary API endpoint in packaged mobile config.
        $config.baseUrl = $resolvedNgrokBaseUrl
        $config.publicBaseUrl = $resolvedNgrokBaseUrl
    }
    elseif (-not [string]::IsNullOrWhiteSpace($resolvedLocalBaseUrl)) {
        $config.baseUrl = $resolvedLocalBaseUrl
        $config.publicBaseUrl = $resolvedLocalBaseUrl
    }

    $knownUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $fallbacks = [System.Collections.Generic.List[string]]::new()

    function Add-FallbackUrl {
        param([string]$Url)

        $normalized = Normalize-Url -Url $Url
        if ([string]::IsNullOrWhiteSpace($normalized)) {
            return
        }

        if ($knownUrls.Add($normalized)) {
            $fallbacks.Add($normalized)
        }
    }

    # Fallback priority: local network first, then emulator, then localhost.
    Add-FallbackUrl -Url $resolvedLocalBaseUrl
    Add-FallbackUrl -Url "http://10.0.2.2:$ApiPort"
    Add-FallbackUrl -Url "http://localhost:$ApiPort"

    foreach ($existing in @($config.fallbackBaseUrls)) {
        Add-FallbackUrl -Url ([string]$existing)
    }

    Add-FallbackUrl -Url ([string]$config.baseUrl)
    Add-FallbackUrl -Url ([string]$config.publicBaseUrl)

    $config.allowLocalhostFallback = $true
    $config.fallbackBaseUrls = @($fallbacks)

    $json = $config | ConvertTo-Json -Depth 10
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

$resolvedIp = Get-PreferredLocalIpv4

Update-NetworkSecurityConfig -Path $ConfigPath -LocalIp $resolvedIp
Update-MobileApiConfig -Path $MobileApiConfigPath -LocalIp $resolvedIp -ApiPort $Port -PrimaryBaseUrl $NgrokBaseUrl
