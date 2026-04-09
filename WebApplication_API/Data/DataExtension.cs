using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Security;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Data;

public static class DataExtension
{
    public static void MigrateDb(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DBContext>();
        context.Database.Migrate();
        SeedAsync(context, app.Environment.ContentRootPath).GetAwaiter().GetResult();
    }

    public static void AddDataToDatabase(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("ConnectionKey");
        builder.Services.AddSqlite<DBContext>(connectionString);
    }

    private static async Task SeedAsync(DBContext context, string contentRootPath)
    {
        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                new Category { Name = "Historical Site", Description = "Places with cultural and historical value." },
                new Category { Name = "Food", Description = "Restaurants, cafes, and local specialties." },
                new Category { Name = "Bus Stop", Description = "Transit locations prepared for QR and GPS guidance." },
                new Category { Name = "Landmark", Description = "High-priority tourist landmarks and city icons." });

            await context.SaveChangesAsync();
        }

        if (!await context.Languages.AnyAsync())
        {
            context.Languages.AddRange(
                new Language
                {
                    LangCode = "vi-VN",
                    LangName = "Vietnamese",
                    NativeName = "Tieng Viet",
                    PreferNativeVoice = true,
                    IsDefault = true,
                    Status = 1
                },
                new Language
                {
                    LangCode = "en-US",
                    LangName = "English",
                    NativeName = "English",
                    PreferNativeVoice = true,
                    IsDefault = false,
                    Status = 1
                });

            await context.SaveChangesAsync();
        }

        if (!await context.DashboardUsers.AnyAsync())
        {
            var passwordHasher = new PasswordHasher<DashboardUser>();
            var users = new[]
            {
                CreateSeedUser("admin", "admin", "System Admin", AdminRoles.Admin, "admin@smarttour.local", "0900000001"),
                CreateSeedUser("editor", "editor", "Content Editor", AdminRoles.Editor, "editor@smarttour.local", "0900000002"),
                CreateSeedUser("analyst", "analyst", "Data Analyst", AdminRoles.DataAnalyst, "analyst@smarttour.local", "0900000003"),
                CreateSeedUser("developer", "developer", "Platform Developer", AdminRoles.Developer, "developer@smarttour.local", "0900000004"),
                CreateSeedUser("owner", "owner", "POI Owner", AdminRoles.User, "owner@smarttour.local", "0900000005")
            };

            foreach (var user in users)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, user.PasswordHash);
            }

            context.DashboardUsers.AddRange(users);
            await context.SaveChangesAsync();
        }

        if (!await context.Locations.AnyAsync())
        {
            var categories = await context.Categories.ToDictionaryAsync(item => item.Name);
            var owner = await context.DashboardUsers.FirstAsync(item => item.Username == "owner");

            context.Locations.AddRange(
                new Location
                {
                    Name = "Ben Nha Rong",
                    CategoryId = categories["Historical Site"].CategoryId,
                    OwnerId = owner.UserId,
                    Description = "Historic riverside museum with GPS-triggered audio for nearby visitors.",
                    Latitude = 10.767261,
                    Longitude = 106.707725,
                    Radius = 40,
                    StandbyRadius = 15,
                    Priority = 10,
                    DebounceSeconds = 240,
                    IsGpsTriggerEnabled = true,
                    Address = "1 Nguyen Tat Thanh, Ward 12, District 4, Ho Chi Minh City",
                    WebURL = "https://example.com/ben-nha-rong",
                    Email = "owner@smarttour.local",
                    PhoneContact = "0900000005",
                    EstablishedYear = 1863,
                    Status = 1
                },
                new Location
                {
                    Name = "Ben Thanh Bus Stop",
                    CategoryId = categories["Bus Stop"].CategoryId,
                    OwnerId = owner.UserId,
                    Description = "Transit information point for testing GPS and nearby trigger logic.",
                    Latitude = 10.772474,
                    Longitude = 106.698059,
                    Radius = 35,
                    StandbyRadius = 12,
                    Priority = 5,
                    DebounceSeconds = 180,
                    IsGpsTriggerEnabled = true,
                    Address = "Le Loi Street, Ben Thanh Ward, District 1, Ho Chi Minh City",
                    EstablishedYear = 2000,
                    Status = 1
                });

            await context.SaveChangesAsync();
        }

        await EnsureSeedLocationImagesAsync(context, contentRootPath);
        await EnsureLocationPreferenceImagesAsync(context);

        await EnsureSeedAudioVariantsAsync(context, contentRootPath);

        if (!await context.Tours.AnyAsync())
        {
            var owner = await context.DashboardUsers.FirstAsync(item => item.Username == "owner");
            var seededLocations = await context.Locations
                .OrderBy(item => item.LocationId)
                .Take(2)
                .ToListAsync();

            if (seededLocations.Count > 0)
            {
                var metrics = TourPlanningService.CalculateMetrics(
                    seededLocations,
                    TourDefaults.DefaultWalkingSpeedKph,
                    TourDefaults.DefaultStartTime);
                var tour = new Tour
                {
                    OwnerId = owner.UserId,
                    Name = "District 4 Riverside Starter Tour",
                    Description = "A short curated walk connecting the seeded District 4 points of interest.",
                    TotalDistanceKm = metrics.TotalDistanceKm,
                    EstimatedDurationMinutes = metrics.EstimatedDurationMinutes,
                    WalkingSpeedKph = TourDefaults.DefaultWalkingSpeedKph,
                    StartTime = metrics.StartTime,
                    Status = 1,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var stop in seededLocations.Select((location, index) => new { location, index }))
                {
                    tour.Stops.Add(new TourLocation
                    {
                        LocationId = stop.location.LocationId,
                        SequenceOrder = stop.index + 1,
                        SegmentDistanceKm = stop.index == 0
                            ? 0d
                            : Math.Round(
                                TourPlanningService.CalculateDistanceKm(
                                    seededLocations[stop.index - 1],
                                    stop.location),
                                2,
                                MidpointRounding.AwayFromZero)
                    });
                }

                context.Tours.Add(tour);
                await context.SaveChangesAsync();
            }
        }

        if (!await context.PlaybackEvents.AnyAsync())
        {
            var location = await context.Locations.OrderBy(item => item.LocationId).FirstAsync();
            var audio = await context.AudioContents.OrderBy(item => item.AudioId).FirstAsync();

            context.PlaybackEvents.AddRange(
                new PlaybackEvent
                {
                    DeviceId = "android-demo-01",
                    LocationId = location.LocationId,
                    AudioId = audio.AudioId,
                    TriggerSource = "GeofenceEnter",
                    EventType = "Started",
                    ListeningSeconds = 12,
                    BatteryPercent = 84,
                    NetworkType = "WiFi",
                    SessionId = "session-demo-01",
                    EventAt = DateTime.UtcNow.AddMinutes(-30)
                },
                new PlaybackEvent
                {
                    DeviceId = "android-demo-01",
                    LocationId = location.LocationId,
                    AudioId = audio.AudioId,
                    TriggerSource = "GeofenceEnter",
                    EventType = "Completed",
                    ListeningSeconds = 96,
                    BatteryPercent = 83,
                    NetworkType = "WiFi",
                    SessionId = "session-demo-01",
                    EventAt = DateTime.UtcNow.AddMinutes(-28)
                });

            await context.SaveChangesAsync();
        }

        if (!await context.LocationTrackingEvents.AnyAsync())
        {
            context.LocationTrackingEvents.AddRange(
                new LocationTrackingEvent
                {
                    DeviceId = "android-demo-01",
                    SessionId = "session-demo-01",
                    Latitude = 10.767261,
                    Longitude = 106.707725,
                    AccuracyMeters = 8.4,
                    SpeedMetersPerSecond = 0.5,
                    BatteryPercent = 84,
                    IsForeground = true,
                    CapturedAt = DateTime.UtcNow.AddMinutes(-31)
                },
                new LocationTrackingEvent
                {
                    DeviceId = "android-demo-01",
                    SessionId = "session-demo-01",
                    Latitude = 10.767300,
                    Longitude = 106.707800,
                    AccuracyMeters = 7.8,
                    SpeedMetersPerSecond = 0.4,
                    BatteryPercent = 83,
                    IsForeground = true,
                    CapturedAt = DateTime.UtcNow.AddMinutes(-29)
                });

            await context.SaveChangesAsync();
        }

        await EnsureAnalyticsSamplesAsync(context, contentRootPath);
    }

    private static async Task EnsureAnalyticsSamplesAsync(DBContext context, string contentRootPath)
    {
        await NormalizeSeedAddressesAsync(context);

        var users = await context.DashboardUsers
            .Where(item => item.Username == "admin" || item.Username == "owner")
            .ToDictionaryAsync(item => item.Username, StringComparer.OrdinalIgnoreCase);

        if (!users.TryGetValue("admin", out var adminUser) || !users.TryGetValue("owner", out var ownerUser))
        {
            return;
        }

        var categories = await context.Categories
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var historicalCategoryId = categories.TryGetValue("Historical Site", out var historicalCategory)
            ? historicalCategory.CategoryId
            : await context.Categories.OrderBy(item => item.CategoryId).Select(item => item.CategoryId).FirstAsync();
        var busStopCategoryId = ResolveCategoryId(
            categories,
            historicalCategoryId,
            "Bus Stop",
            "Markets & Food Halls",
            "Historical Sites");
        var landmarkCategoryId = categories.TryGetValue("Landmark", out var landmarkCategory)
            ? landmarkCategory.CategoryId
            : historicalCategoryId;

        var benNhaRong = await EnsureAnalyticsLocationAsync(
            context,
            "Ben Nha Rong",
            ownerUser.UserId,
            historicalCategoryId,
            "Historic riverside museum with GPS-triggered audio for nearby visitors.",
            10.767261,
            106.707725,
            "1 Nguyen Tat Thanh, Ward 12, District 4, Ho Chi Minh City",
            1863);

        var benThanh = await EnsureAnalyticsLocationAsync(
            context,
            "Ben Thanh Bus Stop",
            ownerUser.UserId,
            busStopCategoryId,
            "Transit information point for testing GPS and nearby trigger logic.",
            10.772474,
            106.698059,
            "Le Loi Street, Ben Thanh Ward, District 1, Ho Chi Minh City",
            2000);

        var khanhHoi = await EnsureAnalyticsLocationAsync(
            context,
            "Khanh Hoi Riverside Pier",
            ownerUser.UserId,
            historicalCategoryId,
            "Additional owner-scoped POI used to test ward filters, route clustering, and riverfront telemetry.",
            10.760210,
            106.705610,
            "1 Ton That Thuyet, Ward 13, District 4, Ho Chi Minh City",
            1988);

        var operaSquare = await EnsureAnalyticsLocationAsync(
            context,
            "Opera House Square",
            adminUser.UserId,
            landmarkCategoryId,
            "Admin-scoped cultural POI used to contrast global analytics with owner-only dashboards.",
            10.776428,
            106.703438,
            "7 Cong Truong Lam Son, Ben Nghe Ward, District 1, Ho Chi Minh City",
            1900);

        var audioByLocationId = new Dictionary<int, Audio>
        {
            [benNhaRong.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                benNhaRong.LocationId,
                "Ben Nha Rong Introduction",
                "Historic arrival story for the riverside museum approach.",
                "Chao mung ban den Ben Nha Rong, diem bat dau phu hop de kiem tra tuyen du lich ben song va audio tu dong.",
                96,
                "Local Guide",
                "Female",
                "vi-VN"),
            [benThanh.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                benThanh.LocationId,
                "Ben Thanh Arrival Guide",
                "Transit and orientation script for the central stop.",
                "Guests arriving here are close to the central market corridor and can continue into nearby cultural stops.",
                88,
                "City Host Minh",
                "Male",
                "en-US"),
            [khanhHoi.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                khanhHoi.LocationId,
                "Khanh Hoi Riverside Route",
                "Owner-side sample narration for riverfront movement analytics.",
                "Khanh Hoi is a good sample stop for measuring anonymous route history near the waterfront.",
                92,
                "River Guide Lan",
                "Female",
                "vi-VN"),
            [operaSquare.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                operaSquare.LocationId,
                "Opera House Square Highlights",
                "Admin-scoped narration for a high-traffic downtown landmark.",
                "This square helps test city-center playback counts, tour filters, and listening-time reports.",
                104,
                "Culture Guide Alex",
                "Male",
                "en-US")
        };

        await EnsureAnalyticsRecordedAudioAsync(
            context,
            contentRootPath,
            benNhaRong.LocationId,
            "Ben Nha Rong Recorded Demo",
            "Synthetic recorded clip used to verify Android file playback and button loading states.",
            null,
            4,
            "Recorded Demo Lan",
            "Female",
            "vi-VN",
            "Recorded",
            "demo-ben-nha-rong-recorded.wav",
            392,
            554);

        await EnsureAnalyticsRecordedAudioAsync(
            context,
            contentRootPath,
            benThanh.LocationId,
            "Ben Thanh Hybrid Demo",
            "Hybrid sample that keeps both a script and a stored file for fallback testing.",
            "This hybrid sample lets the mobile app switch between stored audio and on-device speech when needed.",
            4,
            "Recorded Demo Minh",
            "Male",
            "en-US",
            "Hybrid",
            "demo-ben-thanh-hybrid.wav",
            523,
            659);

        await EnsureAnalyticsTourAsync(
            context,
            "District 4 River Stories",
            ownerUser.UserId,
            "Owner-facing analytics tour that connects the seeded District 4 stops.",
            [benNhaRong, khanhHoi]);

        await EnsureAnalyticsTourAsync(
            context,
            "Downtown Culture Sprint",
            adminUser.UserId,
            "Admin-facing downtown loop for contrasting global traffic with owner-only analytics.",
            [operaSquare, benThanh]);

        var hasAnalyticsPlayback = await context.PlaybackEvents
            .AnyAsync(item => item.DeviceId != null && item.DeviceId.StartsWith("analytics-demo-"));

        if (hasAnalyticsPlayback)
        {
            return;
        }

        var seededLocations = new[] { benNhaRong, benThanh, khanhHoi, operaSquare };
        var pointOffsets = new (double LatitudeOffset, double LongitudeOffset, double Accuracy, double Speed)[]
        {
            (-0.00042, -0.00026, 11.2, 1.3),
            (-0.00024, -0.00012, 8.7, 0.9),
            (-0.00006, -0.00003, 6.8, 0.6),
            (0.00011, 0.00008, 6.1, 0.4),
            (0.00024, 0.00016, 7.2, 0.5),
            (0.00038, 0.00027, 8.8, 0.7)
        };

        var telemetryStart = DateTime.UtcNow.Date.AddDays(-9).AddHours(8);
        for (var index = 0; index < 12; index++)
        {
            var location = seededLocations[index % seededLocations.Length];
            var audio = audioByLocationId[location.LocationId];
            var sessionId = $"analytics-session-{index + 1:D2}";
            var deviceId = $"analytics-demo-{(index % 4) + 1:D2}";
            var sessionStart = telemetryStart.AddDays(index).AddMinutes(index * 17);
            var battery = 92 - (index * 3 % 18);
            var triggerSource = (index % 3) switch
            {
                0 => "GeofenceEnter",
                1 => "Nearby",
                _ => "QrScan"
            };
            var networkType = index % 2 == 0 ? "WiFi" : "4G";

            for (var pointIndex = 0; pointIndex < pointOffsets.Length; pointIndex++)
            {
                var offset = pointOffsets[pointIndex];
                context.LocationTrackingEvents.Add(new LocationTrackingEvent
                {
                    DeviceId = deviceId,
                    SessionId = sessionId,
                    Latitude = location.Latitude + offset.LatitudeOffset,
                    Longitude = location.Longitude + offset.LongitudeOffset,
                    AccuracyMeters = offset.Accuracy,
                    SpeedMetersPerSecond = offset.Speed,
                    BatteryPercent = battery - pointIndex,
                    IsForeground = pointIndex < pointOffsets.Length - 1,
                    CapturedAt = sessionStart.AddSeconds(pointIndex * 95)
                });
            }

            context.PlaybackEvents.Add(new PlaybackEvent
            {
                DeviceId = deviceId,
                SessionId = sessionId,
                LocationId = location.LocationId,
                AudioId = audio.AudioId,
                TriggerSource = triggerSource,
                EventType = "Started",
                ListeningSeconds = 14 + index,
                QueuePosition = 1,
                BatteryPercent = battery,
                NetworkType = networkType,
                EventAt = sessionStart.AddMinutes(3)
            });

            context.PlaybackEvents.Add(new PlaybackEvent
            {
                DeviceId = deviceId,
                SessionId = sessionId,
                LocationId = location.LocationId,
                AudioId = audio.AudioId,
                TriggerSource = triggerSource,
                EventType = "Completed",
                ListeningSeconds = (audio.DurationSeconds ?? 90) - 4 + (index % 9),
                QueuePosition = 1,
                BatteryPercent = battery - 1,
                NetworkType = networkType,
                EventAt = sessionStart.AddMinutes(6)
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task NormalizeSeedAddressesAsync(DBContext context)
    {
        var hasChanges = false;
        var benNhaRong = await context.Locations.FirstOrDefaultAsync(item => item.Name == "Ben Nha Rong");
        if (benNhaRong is not null && (string.IsNullOrWhiteSpace(benNhaRong.Address) || !benNhaRong.Address.Contains("Ward", StringComparison.OrdinalIgnoreCase)))
        {
            benNhaRong.Address = "1 Nguyen Tat Thanh, Ward 12, District 4, Ho Chi Minh City";
            benNhaRong.UpdatedAt = DateTime.UtcNow;
            hasChanges = true;
        }

        var benThanh = await context.Locations.FirstOrDefaultAsync(item => item.Name == "Ben Thanh Bus Stop");
        if (benThanh is not null && (string.IsNullOrWhiteSpace(benThanh.Address) || !benThanh.Address.Contains("Ward", StringComparison.OrdinalIgnoreCase)))
        {
            benThanh.Address = "Le Loi Street, Ben Thanh Ward, District 1, Ho Chi Minh City";
            benThanh.UpdatedAt = DateTime.UtcNow;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureLocationPreferenceImagesAsync(DBContext context)
    {
        var locations = await context.Locations
            .Include(item => item.Images)
            .Where(item => item.Images.Any())
            .ToListAsync();

        var hasChanges = false;
        foreach (var location in locations)
        {
            var orderedImages = location.Images
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.ImageId)
                .ToList();

            if (orderedImages.Count == 0)
            {
                continue;
            }

            var preferenceImageUrl = location.PreferenceImageUrl;
            if (string.IsNullOrWhiteSpace(preferenceImageUrl)
                || orderedImages.All(item => !string.Equals(item.ImageUrl, preferenceImageUrl, StringComparison.OrdinalIgnoreCase)))
            {
                preferenceImageUrl = orderedImages[0].ImageUrl;
                location.PreferenceImageUrl = preferenceImageUrl;
                hasChanges = true;
            }

            var preferenceImage = orderedImages.FirstOrDefault(item =>
                string.Equals(item.ImageUrl, preferenceImageUrl, StringComparison.OrdinalIgnoreCase));

            if (preferenceImage is not null)
            {
                orderedImages.Remove(preferenceImage);
                orderedImages.Insert(0, preferenceImage);
            }

            for (var index = 0; index < orderedImages.Count; index++)
            {
                var desiredSortOrder = index + 1;
                if (orderedImages[index].SortOrder != desiredSortOrder)
                {
                    orderedImages[index].SortOrder = desiredSortOrder;
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task<Location> EnsureAnalyticsLocationAsync(
        DBContext context,
        string name,
        int ownerId,
        int categoryId,
        string description,
        double latitude,
        double longitude,
        string address,
        int establishedYear)
    {
        var existingLocation = await context.Locations.FirstOrDefaultAsync(item => item.Name == name);
        if (existingLocation is not null)
        {
            return existingLocation;
        }

        var location = new Location
        {
            Name = name,
            OwnerId = ownerId,
            CategoryId = categoryId,
            Description = description,
            Latitude = latitude,
            Longitude = longitude,
            Radius = 36,
            StandbyRadius = 12,
            Priority = 7,
            DebounceSeconds = 180,
            IsGpsTriggerEnabled = true,
            Address = address,
            EstablishedYear = establishedYear,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task<Audio> EnsureAnalyticsAudioAsync(
        DBContext context,
        int locationId,
        string title,
        string description,
        string script,
        int durationSeconds,
        string voiceName,
        string voiceGender,
        string languageCode)
    {
        var existingAudio = await context.AudioContents.FirstOrDefaultAsync(item => item.LocationId == locationId && item.Title == title);
        if (existingAudio is not null)
        {
            return existingAudio;
        }

        var audio = new Audio
        {
            LocationId = locationId,
            Title = title,
            Description = description,
            LanguageCode = languageCode,
            SourceType = "TTS",
            Script = script,
            DurationSeconds = durationSeconds,
            VoiceName = voiceName,
            VoiceGender = voiceGender,
            Priority = 6,
            PlaybackMode = "Auto",
            InterruptPolicy = "NotificationFirst",
            IsDownloadable = true,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.AudioContents.Add(audio);
        await context.SaveChangesAsync();
        return audio;
    }

    private static async Task<Audio> EnsureAnalyticsRecordedAudioAsync(
        DBContext context,
        string contentRootPath,
        int locationId,
        string title,
        string description,
        string? script,
        int durationSeconds,
        string voiceName,
        string voiceGender,
        string languageCode,
        string sourceType,
        string fileName,
        int startFrequencyHz,
        int endFrequencyHz)
    {
        var audioFile = await EnsureSeedAudioClipAsync(
            contentRootPath,
            fileName,
            durationSeconds,
            startFrequencyHz,
            endFrequencyHz);

        var existingAudio = await context.AudioContents.FirstOrDefaultAsync(item => item.LocationId == locationId && item.Title == title);
        if (existingAudio is not null)
        {
            return existingAudio;
        }

        var audio = new Audio
        {
            LocationId = locationId,
            Title = title,
            Description = description,
            LanguageCode = languageCode,
            SourceType = sourceType,
            Script = script,
            FilePath = SharedStoragePaths.ToPublicAudioPath(audioFile.Name),
            FileSizeBytes = checked((int)audioFile.Length),
            DurationSeconds = durationSeconds,
            VoiceName = voiceName,
            VoiceGender = voiceGender,
            Priority = 7,
            PlaybackMode = "Auto",
            InterruptPolicy = "NotificationFirst",
            IsDownloadable = true,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.AudioContents.Add(audio);
        await context.SaveChangesAsync();
        return audio;
    }

    private static async Task<FileInfo> EnsureSeedAudioClipAsync(
        string contentRootPath,
        string fileName,
        int durationSeconds,
        int startFrequencyHz,
        int endFrequencyHz)
    {
        var audioDirectory = SharedStoragePaths.GetAudioDirectory(contentRootPath);
        Directory.CreateDirectory(audioDirectory);

        var filePath = Path.Combine(audioDirectory, fileName);
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            return new FileInfo(filePath);
        }

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        WriteSeedWaveTone(writer, durationSeconds, startFrequencyHz, endFrequencyHz);
        await stream.FlushAsync();

        return new FileInfo(filePath);
    }

    private static void WriteSeedWaveTone(
        BinaryWriter writer,
        int durationSeconds,
        int startFrequencyHz,
        int endFrequencyHz)
    {
        const short channelCount = 1;
        const int sampleRate = 16000;
        const short bitsPerSample = 16;

        var totalSamples = Math.Max(sampleRate, sampleRate * Math.Max(1, durationSeconds));
        var blockAlign = (short)(channelCount * bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;
        var dataSize = totalSamples * blockAlign;

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (var sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
        {
            var progress = totalSamples <= 1 ? 0d : sampleIndex / (double)(totalSamples - 1);
            var frequency = startFrequencyHz + ((endFrequencyHz - startFrequencyHz) * progress);
            var time = sampleIndex / (double)sampleRate;
            var envelope = Math.Sin(Math.PI * progress);
            var amplitude = Math.Sin(2 * Math.PI * frequency * time) * 0.24 * envelope;
            writer.Write((short)(amplitude * short.MaxValue));
        }
    }

    private static async Task EnsureAnalyticsTourAsync(
        DBContext context,
        string name,
        int ownerId,
        string description,
        IReadOnlyList<Location> stops)
    {
        if (await context.Tours.AnyAsync(item => item.Name == name))
        {
            return;
        }

        var metrics = TourPlanningService.CalculateMetrics(
            stops,
            TourDefaults.DefaultWalkingSpeedKph,
            TourDefaults.DefaultStartTime);

        var tour = new Tour
        {
            OwnerId = ownerId,
            Name = name,
            Description = description,
            TotalDistanceKm = metrics.TotalDistanceKm,
            EstimatedDurationMinutes = metrics.EstimatedDurationMinutes,
            WalkingSpeedKph = TourDefaults.DefaultWalkingSpeedKph,
            StartTime = metrics.StartTime,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        for (var index = 0; index < stops.Count; index++)
        {
            tour.Stops.Add(new TourLocation
            {
                LocationId = stops[index].LocationId,
                SequenceOrder = index + 1,
                SegmentDistanceKm = index == 0
                    ? 0d
                    : Math.Round(
                        TourPlanningService.CalculateDistanceKm(stops[index - 1], stops[index]),
                        2,
                        MidpointRounding.AwayFromZero)
            });
        }

        context.Tours.Add(tour);
        await context.SaveChangesAsync();
    }

    private static int ResolveCategoryId(
        IReadOnlyDictionary<string, Category> categories,
        int fallbackCategoryId,
        params string[] preferredNames)
    {
        foreach (var preferredName in preferredNames)
        {
            if (categories.TryGetValue(preferredName, out var category))
            {
                return category.CategoryId;
            }
        }

        return fallbackCategoryId;
    }

    private static DashboardUser CreateSeedUser(
        string username,
        string password,
        string fullName,
        string role,
        string email,
        string phone) =>
        new()
        {
            Username = username,
            PasswordHash = password,
            FullName = fullName,
            Role = role,
            Email = email,
            Phone = phone,
            Status = 1
        };

    private static async Task EnsureSeedLocationImagesAsync(DBContext context, string contentRootPath)
    {
        var locations = await context.Locations
            .Include(item => item.Images)
            .OrderBy(item => item.LocationId)
            .ToListAsync();

        var hasChanges = false;
        foreach (var location in locations)
        {
            var existingImageUrls = location.Images
                .Select(item => item.ImageUrl)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nextSortOrder = location.Images.Count == 0
                ? 1
                : location.Images.Max(item => item.SortOrder) + 1;
            var sampleImages = new List<string>(3);

            for (var variantIndex = 1; variantIndex <= 3; variantIndex++)
            {
                var imagePath = await EnsureSeedImageAsync(contentRootPath, location.Name, location.LocationId, variantIndex);
                sampleImages.Add(imagePath);

                if (existingImageUrls.Contains(imagePath))
                {
                    continue;
                }

                context.LocationImages.Add(new LocationImage
                {
                    LocationId = location.LocationId,
                    ImageUrl = imagePath,
                    Description = $"Seed sample image {variantIndex} for {location.Name}",
                    SortOrder = nextSortOrder++,
                    CreatedAt = DateTime.UtcNow
                });
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(location.PreferenceImageUrl) ||
                HasLegacySeedSvg(location.PreferenceImageUrl) ||
                !existingImageUrls.Contains(location.PreferenceImageUrl))
            {
                location.PreferenceImageUrl = sampleImages[0];
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureSeedAudioVariantsAsync(DBContext context, string contentRootPath)
    {
        var locations = await context.Locations
            .Where(item => item.Status == 1)
            .OrderBy(item => item.LocationId)
            .ToListAsync();

        foreach (var location in locations)
        {
            await EnsureAnalyticsAudioAsync(
                context,
                location.LocationId,
                $"{location.Name} TTS Guide",
                $"Seed TTS sample for {location.Name}.",
                BuildSeedAudioScript(location, "tts"),
                72,
                "Smart Tour Voice",
                "Female",
                "vi-VN");

            await EnsureAnalyticsRecordedAudioAsync(
                context,
                contentRootPath,
                location.LocationId,
                $"{location.Name} Recorded Guide",
                $"Seed recorded sample for {location.Name}.",
                null,
                64,
                "Field Narrator",
                "Male",
                "en-US",
                "Recorded",
                $"seed-location-{location.LocationId:D2}-recorded.wav",
                360,
                580);

            await EnsureAnalyticsRecordedAudioAsync(
                context,
                contentRootPath,
                location.LocationId,
                $"{location.Name} Hybrid Guide",
                $"Seed hybrid sample for {location.Name}.",
                BuildSeedAudioScript(location, "hybrid"),
                68,
                "Hybrid Tour Voice",
                "Female",
                "en-US",
                "Hybrid",
                $"seed-location-{location.LocationId:D2}-hybrid.wav",
                440,
                700);
        }
    }

    private static string BuildSeedAudioScript(Location location, string sourceType)
    {
        var address = string.IsNullOrWhiteSpace(location.Address)
            ? "Ho Chi Minh City"
            : location.Address;

        return sourceType switch
        {
            "hybrid" =>
                $"Welcome to {location.Name}. This hybrid sample combines a stored clip with script backup so the mobile app can test both playback paths. Destination address: {address}.",
            _ =>
                $"Xin chao, day la ban thu TTS cho dia diem {location.Name}. Vi tri nay nam tai {address} va duoc tao de kiem thu ung dung du lich thong minh."
        };
    }

    private static async Task<string> EnsureSeedImageAsync(
        string contentRootPath,
        string locationName,
        int seedIndex,
        int variantIndex = 1)
    {
        var imageDirectory = SharedStoragePaths.GetImageDirectory(contentRootPath);
        Directory.CreateDirectory(imageDirectory);

        var fileName = $"seed-location-{seedIndex:D2}-{variantIndex:D2}.bmp";
        var fullPath = Path.Combine(imageDirectory, fileName);
        if (!File.Exists(fullPath))
        {
            await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            WriteSeedBitmap(writer, locationName, seedIndex, variantIndex);
            await stream.FlushAsync();
        }

        return SharedStoragePaths.ToPublicImagePath(fileName);
    }

    private static bool HasLegacySeedSvg(string? imagePath) =>
        !string.IsNullOrWhiteSpace(imagePath) &&
        imagePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) &&
        imagePath.Contains("seed-location-", StringComparison.OrdinalIgnoreCase);

    private static void WriteSeedBitmap(BinaryWriter writer, string locationName, int seedIndex, int variantIndex)
    {
        const int width = 960;
        const int height = 540;
        const short bitsPerPixel = 24;

        var rowSize = ((width * bitsPerPixel + 31) / 32) * 4;
        var pixelArraySize = rowSize * height;
        var fileSize = 54 + pixelArraySize;
        var seed = HashCode.Combine(locationName.ToLowerInvariant(), seedIndex, variantIndex);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write(bitsPerPixel);
        writer.Write(0);
        writer.Write(pixelArraySize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var baseRed = (byte)(70 + Math.Abs(seed % 120));
        var baseGreen = (byte)(90 + Math.Abs((seed / 3) % 120));
        var baseBlue = (byte)(110 + Math.Abs((seed / 7) % 120));
        var accentRed = (byte)Math.Min(255, baseRed + 60);
        var accentGreen = (byte)Math.Min(255, baseGreen + 45);
        var accentBlue = (byte)Math.Min(255, baseBlue + 35);
        var paddingPerRow = rowSize - (width * 3);
        var padding = new byte[paddingPerRow];

        for (var y = 0; y < height; y++)
        {
            var verticalRatio = height <= 1 ? 0d : y / (double)(height - 1);
            for (var x = 0; x < width; x++)
            {
                var horizontalRatio = width <= 1 ? 0d : x / (double)(width - 1);
                var stripe = ((x / 96) + variantIndex) % 3 == 0 ? 1d : 0d;
                var glow = 1d - Math.Min(1d, Math.Abs(horizontalRatio - 0.5d) * 1.6d);
                var mix = Math.Clamp((verticalRatio * 0.55d) + (horizontalRatio * 0.25d) + (stripe * 0.20d), 0d, 1d);

                var red = (byte)Math.Clamp((baseRed * (1d - mix)) + (accentRed * mix) + (glow * 10d), 0d, 255d);
                var green = (byte)Math.Clamp((baseGreen * (1d - mix)) + (accentGreen * mix) + (glow * 16d), 0d, 255d);
                var blue = (byte)Math.Clamp((baseBlue * (1d - mix)) + (accentBlue * mix) + (glow * 22d), 0d, 255d);

                writer.Write(blue);
                writer.Write(green);
                writer.Write(red);
            }

            if (paddingPerRow > 0)
            {
                writer.Write(padding);
            }
        }
    }
}
