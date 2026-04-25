param(
    [string]$DbPath = ".\WebApplication_API\App.db",
    [string]$ImageArchiveDir = ".\WebApplication_API\wwwroot\archive\images",
    [string]$AudioArchiveDir = ".\WebApplication_API\wwwroot\archive\audio"
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$PathValue)

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    $repoRoot = Split-Path -Parent $PSScriptRoot
    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PathValue))
}

function Escape-SqlValue {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return "NULL"
    }

    return "'" + $Value.Replace("'", "''") + "'"
}

function Get-SqlScalar {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $result = sqlite3 $DatabasePath $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "sqlite3 query failed."
    }

    return [string]$result
}

function Invoke-SqlNonQuery {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    sqlite3 $DatabasePath $Sql | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sqlite3 command failed."
    }
}

function Copy-MediaFile {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$TargetDirectory,
        [Parameter(Mandatory = $true)][string]$TargetFileName
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Missing source media file: $SourcePath"
    }

    New-Item -ItemType Directory -Force -Path $TargetDirectory | Out-Null
    $destinationPath = Join-Path $TargetDirectory $TargetFileName
    Copy-Item -LiteralPath $SourcePath -Destination $destinationPath -Force
    return Get-Item -LiteralPath $destinationPath
}

function Get-LocationImageUrls {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][int]$LocationId
    )

    $raw = sqlite3 $DatabasePath "SELECT ImageUrl FROM LocationImages WHERE LocationId = $LocationId ORDER BY SortOrder, ImageId;"
    if ($LASTEXITCODE -ne 0) {
        throw "sqlite3 image query failed."
    }

    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    return @($raw -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Upsert-Location {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][hashtable]$Spot,
        [Parameter(Mandatory = $true)][int]$OwnerId,
        [Parameter(Mandatory = $true)][int]$CategoryId
    )

    $nameSql = Escape-SqlValue $Spot.Name
    $existingId = Get-SqlScalar -DatabasePath $DatabasePath -Sql "SELECT LocationId FROM Locations WHERE Name = $nameSql LIMIT 1;"

    $nowUtc = [DateTime]::UtcNow.ToString("o")
    $descriptionSql = Escape-SqlValue $Spot.Description
    $addressSql = Escape-SqlValue $Spot.Address
    $phoneSql = Escape-SqlValue $Spot.Phone
    $webUrlSql = Escape-SqlValue $Spot.WebUrl
    $emailSql = Escape-SqlValue "owner@smarttour.local"
    $preferenceImageSql = Escape-SqlValue $Spot.PreferenceImageUrl

    if (-not [string]::IsNullOrWhiteSpace($existingId)) {
        $sql = @"
UPDATE Locations
SET OwnerId = $OwnerId,
    CategoryId = $CategoryId,
    Description = $descriptionSql,
    Latitude = $($Spot.Latitude),
    Longitude = $($Spot.Longitude),
    Radius = $($Spot.Radius),
    StandbyRadius = $($Spot.StandbyRadius),
    Priority = $($Spot.Priority),
    DebounceSeconds = $($Spot.DebounceSeconds),
    IsGpsTriggerEnabled = 1,
    Address = $addressSql,
    PreferenceImageUrl = $preferenceImageSql,
    WebURL = $webUrlSql,
    Email = $emailSql,
    PhoneContact = $phoneSql,
    EstablishedYear = $($Spot.EstablishedYear),
    Status = 1,
    QrFormat = 'png',
    QrSize = 512,
    QrAutoplay = 1,
    UpdatedAt = $(Escape-SqlValue $nowUtc)
WHERE LocationId = $existingId;
"@
        Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql $sql
        return [int]$existingId
    }

    $sql = @"
INSERT INTO Locations
(
    Name, OwnerId, CategoryId, Description, Latitude, Longitude, Radius, StandbyRadius,
    Priority, DebounceSeconds, IsGpsTriggerEnabled, Address, PreferenceImageUrl, WebURL,
    Email, PhoneContact, EstablishedYear, Status, QrFormat, QrSize, QrAutoplay, CreatedAt
)
VALUES
(
    $nameSql, $OwnerId, $CategoryId, $descriptionSql, $($Spot.Latitude), $($Spot.Longitude), $($Spot.Radius), $($Spot.StandbyRadius),
    $($Spot.Priority), $($Spot.DebounceSeconds), 1, $addressSql, $preferenceImageSql, $webUrlSql,
    $emailSql, $phoneSql, $($Spot.EstablishedYear), 1, 'png', 512, 1, $(Escape-SqlValue $nowUtc)
);
"@
    Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql $sql

    $newId = Get-SqlScalar -DatabasePath $DatabasePath -Sql "SELECT LocationId FROM Locations WHERE Name = $nameSql ORDER BY LocationId DESC LIMIT 1;"
    return [int]$newId
}

function Sync-LocationImages {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][int]$LocationId,
        [Parameter(Mandatory = $true)][string[]]$ImageUrls,
        [Parameter(Mandatory = $true)][string]$PreferredImageUrl
    )

    Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql "DELETE FROM LocationImages WHERE LocationId = $LocationId AND ImageUrl LIKE '/archive/images/seed-location-%';"

    $desiredImageSqlList = ($ImageUrls | ForEach-Object { Escape-SqlValue $_ }) -join ", "
    Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql @"
DELETE FROM LocationImages
WHERE LocationId = $LocationId
  AND ImageUrl NOT IN ($desiredImageSqlList);
"@

    $currentImageUrls = Get-LocationImageUrls -DatabasePath $DatabasePath -LocationId $LocationId
    $currentLookup = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($currentImageUrl in $currentImageUrls) {
        $null = $currentLookup.Add($currentImageUrl)
    }
    $currentMaxSortOrderRaw = Get-SqlScalar -DatabasePath $DatabasePath -Sql "SELECT COALESCE(MAX(SortOrder), 0) FROM LocationImages WHERE LocationId = $LocationId;"
    $nextSortOrder = [int]$currentMaxSortOrderRaw + 1
    $createdAt = Escape-SqlValue ([DateTime]::UtcNow.ToString("o"))

    for ($index = 0; $index -lt $ImageUrls.Count; $index++) {
        if ($currentLookup.Contains($ImageUrls[$index])) {
            continue
        }

        $imageUrlSql = Escape-SqlValue $ImageUrls[$index]
        $descriptionSql = Escape-SqlValue ("Managed Vinh Khanh image " + ($index + 1))
        $sql = @"
INSERT INTO LocationImages (LocationId, ImageUrl, Description, SortOrder, CreatedAt)
VALUES ($LocationId, $imageUrlSql, $descriptionSql, $nextSortOrder, $createdAt);
"@
        Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql $sql
        $nextSortOrder++
    }

    for ($index = 0; $index -lt $ImageUrls.Count; $index++) {
        $imageUrlSql = Escape-SqlValue $ImageUrls[$index]
        $sortOrder = $index + 1
        Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql @"
UPDATE LocationImages
SET SortOrder = $sortOrder
WHERE LocationId = $LocationId
  AND ImageUrl = $imageUrlSql;
"@
    }

    $currentPreference = Get-SqlScalar -DatabasePath $DatabasePath -Sql "SELECT COALESCE(PreferenceImageUrl, '') FROM Locations WHERE LocationId = $LocationId LIMIT 1;"
    $shouldReplacePreference =
        [string]::IsNullOrWhiteSpace($currentPreference) -or
        $currentPreference -like "/archive/images/seed-location-*" -or
        $currentPreference -like "/archive/images/vinh-khanh-*"

    if ($shouldReplacePreference) {
        Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql @"
UPDATE Locations
SET PreferenceImageUrl = $(Escape-SqlValue $PreferredImageUrl),
    UpdatedAt = $(Escape-SqlValue ([DateTime]::UtcNow.ToString("o")))
WHERE LocationId = $LocationId;
"@
    }
}

function Remove-SeedAudioVariants {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][int]$LocationId,
        [Parameter(Mandatory = $true)][string]$LocationName
    )

    $seedTtsTitleSql = Escape-SqlValue "$LocationName TTS Guide"
    $seedRecordedTitleSql = Escape-SqlValue "$LocationName Recorded Guide"
    $seedHybridTitleSql = Escape-SqlValue "$LocationName Hybrid Guide"
    $sql = @"
DELETE FROM AudioContents
WHERE LocationId = $LocationId
  AND (
        FilePath LIKE '/archive/audio/seed-location-%'
        OR Title IN ($seedTtsTitleSql, $seedRecordedTitleSql, $seedHybridTitleSql)
      );
"@
    Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql $sql
}

function Upsert-Audio {
    param(
        [Parameter(Mandatory = $true)][string]$DatabasePath,
        [Parameter(Mandatory = $true)][int]$LocationId,
        [Parameter(Mandatory = $true)][hashtable]$AudioSpec
    )

    $titleSql = Escape-SqlValue $AudioSpec.Title
    $existingId = Get-SqlScalar -DatabasePath $DatabasePath -Sql "SELECT AudioId FROM AudioContents WHERE LocationId = $LocationId AND Title = $titleSql LIMIT 1;"

    $descriptionSql = Escape-SqlValue $AudioSpec.Description
    $scriptSql = Escape-SqlValue $AudioSpec.Script
    $filePathSql = Escape-SqlValue $AudioSpec.FilePath
    $voiceNameSql = Escape-SqlValue $AudioSpec.VoiceName
    $voiceGenderSql = Escape-SqlValue $AudioSpec.VoiceGender
    $languageCodeSql = Escape-SqlValue $AudioSpec.LanguageCode
    $sourceTypeSql = Escape-SqlValue $AudioSpec.SourceType
    $nowUtcSql = Escape-SqlValue ([DateTime]::UtcNow.ToString("o"))
    $fileSizeValue = if ($null -eq $AudioSpec.FileSizeBytes) { "NULL" } else { [string]$AudioSpec.FileSizeBytes }

    if (-not [string]::IsNullOrWhiteSpace($existingId)) {
        $sql = @"
UPDATE AudioContents
SET Description = $descriptionSql,
    DurationSeconds = $($AudioSpec.DurationSeconds),
    FilePath = $filePathSql,
    FileSizeBytes = $fileSizeValue,
    InterruptPolicy = 'NotificationFirst',
    IsDownloadable = 1,
    LanguageCode = $languageCodeSql,
    PlaybackMode = 'Auto',
    Priority = $($AudioSpec.Priority),
    Script = $scriptSql,
    SourceType = $sourceTypeSql,
    Status = 1,
    VoiceGender = $voiceGenderSql,
    VoiceName = $voiceNameSql,
    UpdatedAt = $nowUtcSql
WHERE AudioId = $existingId;
"@
        Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql $sql
        return [int]$existingId
    }

    $sql = @"
INSERT INTO AudioContents
(
    CreatedAt, Description, DurationSeconds, FilePath, FileSizeBytes, InterruptPolicy,
    IsDownloadable, LanguageCode, LocationId, PlaybackMode, Priority, Script, SourceType,
    Status, Title, VoiceGender, VoiceName
)
VALUES
(
    $nowUtcSql, $descriptionSql, $($AudioSpec.DurationSeconds), $filePathSql, $fileSizeValue, 'NotificationFirst',
    1, $languageCodeSql, $LocationId, 'Auto', $($AudioSpec.Priority), $scriptSql, $sourceTypeSql,
    1, $titleSql, $voiceGenderSql, $voiceNameSql
);
"@
    Invoke-SqlNonQuery -DatabasePath $DatabasePath -Sql $sql

    $newId = Get-SqlScalar -DatabasePath $DatabasePath -Sql "SELECT AudioId FROM AudioContents WHERE LocationId = $LocationId AND Title = $titleSql ORDER BY AudioId DESC LIMIT 1;"
    return [int]$newId
}

$dbFullPath = Resolve-RepoPath -PathValue $DbPath
$imageArchiveFullPath = Resolve-RepoPath -PathValue $ImageArchiveDir
$audioArchiveFullPath = Resolve-RepoPath -PathValue $AudioArchiveDir

if (-not (Test-Path -LiteralPath $dbFullPath)) {
    throw "Database not found: $dbFullPath"
}

$foodCategoryId = [int](Get-SqlScalar -DatabasePath $dbFullPath -Sql "SELECT CategoryId FROM Categories WHERE Name = 'Food' LIMIT 1;")
$ownerId = [int](Get-SqlScalar -DatabasePath $dbFullPath -Sql "SELECT UserId FROM DashboardUsers WHERE Username = 'owner' LIMIT 1;")

$spots = @(
    @{
        Name = "Quan Oc Gio Xanh Vinh Khanh"
        Description = "Quan oc binh dan noi bat voi oc huong xao cay va mam cham dam vi, phu hop de demo cum diem am thuc tren pho Vinh Khanh."
        Latitude = 10.759338
        Longitude = 106.704216
        Radius = 34
        StandbyRadius = 12
        Priority = 9
        DebounceSeconds = 180
        Address = "149 Vinh Khanh, Ward 8, District 4, Ho Chi Minh City"
        Phone = "0904000101"
        WebUrl = "https://squander-neurology-overhead.ngrok-free.dev/LocationQr/public/location/22"
        EstablishedYear = 2016
        ImageSources = @(
            "C:\Users\LENOVO\Downloads\download (1).jfif",
            "C:\Users\LENOVO\Downloads\download (4).jfif",
            "C:\Users\LENOVO\Downloads\download (7).jfif"
        )
        ImageTargets = @(
            "vinh-khanh-oc-gio-xanh-01.jfif",
            "vinh-khanh-oc-gio-xanh-02.jfif",
            "vinh-khanh-oc-gio-xanh-03.jfif"
        )
        RecordedSource = "C:\Users\LENOVO\Downloads\PhoAmThucVK.mp3"
        RecordedTarget = "vinh-khanh-oc-gio-xanh-recorded.mp3"
        RecordedTitle = "Quan Oc Gio Xanh Recorded Guide"
        RecordedDescription = "Audio recorded gioi thieu quan oc gio xanh o Vinh Khanh."
        RecordedDurationSeconds = 72
        TtsTitle = "Quan Oc Gio Xanh TTS Guide"
        TtsDescription = "Audio TTS gioi thieu mon oc huong xao cay va khong khi an dem tren pho Vinh Khanh."
        TtsScript = "Chao mung ban den Quan Oc Gio Xanh Vinh Khanh. Quan noi tieng voi dia oc huong xao cay, nuoc cham xanh va khong khi an dem ron rang. Khi dung trong vung kich hoat, ung dung se uu tien phat audio huong dan cho trai nghiem am thuc duong pho."
    },
    @{
        Name = "Tiem Sua Thach Mat Lanh Vinh Khanh"
        Description = "Diem dung chan giai nhiet ban mon sua thach da va do uong lanh, duoc dat gan cum quan an toi de tao diem dung nhe trong tour."
        Latitude = 10.759608
        Longitude = 106.704038
        Radius = 34
        StandbyRadius = 12
        Priority = 8
        DebounceSeconds = 180
        Address = "171 Vinh Khanh, Ward 8, District 4, Ho Chi Minh City"
        Phone = "0904000102"
        WebUrl = "https://squander-neurology-overhead.ngrok-free.dev/LocationQr/public/location/23"
        EstablishedYear = 2019
        ImageSources = @(
            "C:\Users\LENOVO\Downloads\download (2).jfif",
            "C:\Users\LENOVO\Downloads\download (3).jfif",
            "C:\Users\LENOVO\Downloads\download (1) (1).jfif"
        )
        ImageTargets = @(
            "vinh-khanh-sua-thach-01.jfif",
            "vinh-khanh-sua-thach-02.jfif",
            "vinh-khanh-sua-thach-03.jfif"
        )
        RecordedSource = "C:\Users\LENOVO\Downloads\BenVanDon.mp3"
        RecordedTarget = "vinh-khanh-sua-thach-recorded.mp3"
        RecordedTitle = "Tiem Sua Thach Recorded Guide"
        RecordedDescription = "Audio recorded gioi thieu diem sua thach mat lanh."
        RecordedDurationSeconds = 68
        TtsTitle = "Tiem Sua Thach TTS Guide"
        TtsDescription = "Audio TTS gioi thieu diem sua thach mat lanh tren pho Vinh Khanh."
        TtsScript = "Ban dang dung truoc Tiem Sua Thach Mat Lanh Vinh Khanh. Quan phuc vu cac ly sua thach mat, ngot vua va rat hop de giai nhiet sau khi thu cac mon hai san cay nong. Day la diem dung co cung ban kinh kich hoat voi mot quan lan can de kiem thu geofence dong cap."
    },
    @{
        Name = "Sinh To Sau Rieng Co Ba Vinh Khanh"
        Description = "Quan nho chuyen sinh to sau rieng, bo va cac loai nuoc xay trai cay, bo sung diem do uong dac trung cho cum am thuc Vinh Khanh."
        Latitude = 10.759881
        Longitude = 106.703856
        Radius = 30
        StandbyRadius = 11
        Priority = 7
        DebounceSeconds = 180
        Address = "189 Vinh Khanh, Ward 8, District 4, Ho Chi Minh City"
        Phone = "0904000103"
        WebUrl = "https://squander-neurology-overhead.ngrok-free.dev/LocationQr/public/location/24"
        EstablishedYear = 2018
        ImageSources = @(
            "C:\Users\LENOVO\Downloads\download (3).jfif",
            "C:\Users\LENOVO\Downloads\download (2).jfif",
            "C:\Users\LENOVO\Downloads\download (6).jfif"
        )
        ImageTargets = @(
            "vinh-khanh-sinh-to-sau-rieng-01.jfif",
            "vinh-khanh-sinh-to-sau-rieng-02.jfif",
            "vinh-khanh-sinh-to-sau-rieng-03.jfif"
        )
        RecordedSource = "C:\Users\LENOVO\Downloads\CauOngLanh.mp3"
        RecordedTarget = "vinh-khanh-sinh-to-sau-rieng-recorded.mp3"
        RecordedTitle = "Sinh To Sau Rieng Recorded Guide"
        RecordedDescription = "Audio recorded gioi thieu quan sinh to sau rieng."
        RecordedDurationSeconds = 66
        TtsTitle = "Sinh To Sau Rieng TTS Guide"
        TtsDescription = "Audio TTS gioi thieu quan sinh to sau rieng tren pho Vinh Khanh."
        TtsScript = "Day la Sinh To Sau Rieng Co Ba Vinh Khanh, noi duoc nhieu ban tre ghe de thu sinh to sau rieng dam, beo va mat. Quay nuoc nay giup bo sung mot diem do uong de nguoi dung co trai nghiem da dang hon khi di bo tren pho am thuc."
    },
    @{
        Name = "Pha Lau Co Tham Vinh Khanh"
        Description = "Quan pha lau vi dam phuc vu to pha lau nuoc cot dua va mot so mon nong an kem, rat hop cho khung gio toi muon."
        Latitude = 10.760133
        Longitude = 106.703692
        Radius = 32
        StandbyRadius = 11
        Priority = 8
        DebounceSeconds = 210
        Address = "205 Vinh Khanh, Ward 8, District 4, Ho Chi Minh City"
        Phone = "0904000104"
        WebUrl = "https://squander-neurology-overhead.ngrok-free.dev/LocationQr/public/location/25"
        EstablishedYear = 2014
        ImageSources = @(
            "C:\Users\LENOVO\Downloads\download (5).jfif",
            "C:\Users\LENOVO\Downloads\download (7).jfif",
            "C:\Users\LENOVO\Downloads\download (4).jfif"
        )
        ImageTargets = @(
            "vinh-khanh-pha-lau-01.jfif",
            "vinh-khanh-pha-lau-02.jfif",
            "vinh-khanh-pha-lau-03.jfif"
        )
        RecordedSource = "C:\Users\LENOVO\Downloads\XomChieu.mp3"
        RecordedTarget = "vinh-khanh-pha-lau-recorded.mp3"
        RecordedTitle = "Pha Lau Co Tham Recorded Guide"
        RecordedDescription = "Audio recorded gioi thieu quan pha lau co tham."
        RecordedDurationSeconds = 70
        TtsTitle = "Pha Lau Co Tham TTS Guide"
        TtsDescription = "Audio TTS gioi thieu quan pha lau co tham tren pho Vinh Khanh."
        TtsScript = "Ban dang den Pha Lau Co Tham Vinh Khanh. Mon noi bat la pha lau nuoc cot dua beo nhe, an kem banh mi, ngoai ra quan con co cac mon nong cho nhom ban toi muon. Dia diem nay duoc dat ban kinh vua phai de tranh chong trigger voi cac quan sat ben."
    },
    @{
        Name = "Banh Trang Nuong Bun Thai Bep Than"
        Description = "Quan an vat ket hop banh trang nuong va bun thai hai san, tao diem dung vui ve va de nhan dien trong cum am thuc Vinh Khanh."
        Latitude = 10.760402
        Longitude = 106.703498
        Radius = 28
        StandbyRadius = 10
        Priority = 7
        DebounceSeconds = 210
        Address = "227 Vinh Khanh, Ward 8, District 4, Ho Chi Minh City"
        Phone = "0904000105"
        WebUrl = "https://squander-neurology-overhead.ngrok-free.dev/LocationQr/public/location/26"
        EstablishedYear = 2020
        ImageSources = @(
            "C:\Users\LENOVO\Downloads\download (6).jfif",
            "C:\Users\LENOVO\Downloads\download (1) (1).jfif",
            "C:\Users\LENOVO\Downloads\download (5).jfif"
        )
        ImageTargets = @(
            "vinh-khanh-banh-trang-nuong-01.jfif",
            "vinh-khanh-banh-trang-nuong-02.jfif",
            "vinh-khanh-banh-trang-nuong-03.jfif"
        )
        RecordedSource = "C:\Users\LENOVO\Downloads\PhoAmThucVK.mp3"
        RecordedTarget = "vinh-khanh-banh-trang-nuong-recorded.mp3"
        RecordedTitle = "Banh Trang Nuong Bun Thai Recorded Guide"
        RecordedDescription = "Audio recorded gioi thieu quan banh trang nuong va bun thai."
        RecordedDurationSeconds = 74
        TtsTitle = "Banh Trang Nuong Bun Thai TTS Guide"
        TtsDescription = "Audio TTS gioi thieu quan an vat ket hop banh trang nuong va bun thai."
        TtsScript = "Day la Banh Trang Nuong Bun Thai Bep Than, mot quan an vat vui mat voi banh trang nuong gion, them lua chon bun thai hai san cho nhung ai muon bua an dam hon. Diem nay giup bo sung mau sac tre trung cho bo du lieu nam quan an o Vinh Khanh."
    }
)

foreach ($spot in $spots) {
    $imageUrls = New-Object System.Collections.Generic.List[string]
    for ($index = 0; $index -lt $spot.ImageSources.Count; $index++) {
        $copiedImage = Copy-MediaFile `
            -SourcePath $spot.ImageSources[$index] `
            -TargetDirectory $imageArchiveFullPath `
            -TargetFileName $spot.ImageTargets[$index]
        $imageUrls.Add("/archive/images/$($copiedImage.Name)")
    }

    $spot.PreferenceImageUrl = $imageUrls[0]

    $copiedAudio = Copy-MediaFile `
        -SourcePath $spot.RecordedSource `
        -TargetDirectory $audioArchiveFullPath `
        -TargetFileName $spot.RecordedTarget

    $locationId = Upsert-Location `
        -DatabasePath $dbFullPath `
        -Spot $spot `
        -OwnerId $ownerId `
        -CategoryId $foodCategoryId

    Sync-LocationImages `
        -DatabasePath $dbFullPath `
        -LocationId $locationId `
        -ImageUrls $imageUrls.ToArray() `
        -PreferredImageUrl $spot.PreferenceImageUrl

    Remove-SeedAudioVariants `
        -DatabasePath $dbFullPath `
        -LocationId $locationId `
        -LocationName $spot.Name

    $recordedAudioId = Upsert-Audio `
        -DatabasePath $dbFullPath `
        -LocationId $locationId `
        -AudioSpec @{
            Title = $spot.RecordedTitle
            Description = $spot.RecordedDescription
            DurationSeconds = $spot.RecordedDurationSeconds
            FilePath = "/archive/audio/$($copiedAudio.Name)"
            FileSizeBytes = $copiedAudio.Length
            Priority = 8
            Script = $null
            SourceType = "Recorded"
            VoiceGender = "Female"
            VoiceName = "Recorded Guide"
            LanguageCode = "vi-VN"
        }

    $null = Upsert-Audio `
        -DatabasePath $dbFullPath `
        -LocationId $locationId `
        -AudioSpec @{
            Title = $spot.TtsTitle
            Description = $spot.TtsDescription
            DurationSeconds = 58
            FilePath = $null
            FileSizeBytes = $null
            Priority = 6
            Script = $spot.TtsScript
            SourceType = "TTS"
            VoiceGender = "Female"
            VoiceName = "Smart Tour Voice"
            LanguageCode = "vi-VN"
        }

    Invoke-SqlNonQuery -DatabasePath $dbFullPath -Sql @"
UPDATE Locations
SET QrAudioTrackId = $recordedAudioId,
    UpdatedAt = $(Escape-SqlValue ([DateTime]::UtcNow.ToString("o")))
WHERE LocationId = $locationId;
"@
}

Write-Host "Imported" $spots.Count "Vinh Khanh food spots into" $dbFullPath
