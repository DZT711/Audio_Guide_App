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

        if (!await context.LocationImages.AnyAsync())
        {
            var locations = await context.Locations
                .OrderBy(item => item.LocationId)
                .Take(2)
                .ToListAsync();

            for (var index = 0; index < locations.Count; index++)
            {
                var location = locations[index];
                var imagePath = await EnsureSeedImageAsync(contentRootPath, location.Name, index + 1);
                context.LocationImages.Add(new LocationImage
                {
                    LocationId = location.LocationId,
                    ImageUrl = imagePath,
                    SortOrder = 1,
                    CreatedAt = DateTime.UtcNow
                });
                location.PreferenceImageUrl = imagePath;
            }

            await context.SaveChangesAsync();
        }

        await EnsureLocationPreferenceImagesAsync(context);

        if (!await context.AudioContents.AnyAsync())
        {
            var firstLocation = await context.Locations.OrderBy(item => item.LocationId).FirstAsync();
            var audioDirectory = SharedStoragePaths.GetAudioDirectory(contentRootPath);
            Directory.CreateDirectory(audioDirectory);
            var seededAudioFile = Directory.EnumerateFiles(audioDirectory, "*.*", SearchOption.TopDirectoryOnly).FirstOrDefault();

            context.AudioContents.Add(new Audio
            {
                LocationId = firstLocation.LocationId,
                Title = "Ben Nha Rong Introduction",
                Description = "Recorded introduction for the museum arrival experience.",
                LanguageCode = "vi-VN",
                SourceType = seededAudioFile is null ? "TTS" : "Hybrid",
                Script = "Chao mung ban den voi Ben Nha Rong, mot dia diem lich su quan trong tai Thanh pho Ho Chi Minh.",
                FilePath = seededAudioFile is null ? null : SharedStoragePaths.ToPublicAudioPath(Path.GetFileName(seededAudioFile)),
                FileSizeBytes = seededAudioFile is null ? null : (int?)new FileInfo(seededAudioFile).Length,
                DurationSeconds = 96,
                VoiceName = "Local Guide",
                VoiceGender = "Female",
                Priority = 10,
                PlaybackMode = "Auto",
                InterruptPolicy = "NotificationFirst",
                IsDownloadable = true,
                Status = 1
            });

            await context.SaveChangesAsync();
        }

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

        await EnsureAnalyticsSamplesAsync(context);
    }

    private static async Task EnsureAnalyticsSamplesAsync(DBContext context)
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

    private static async Task<string> EnsureSeedImageAsync(string contentRootPath, string locationName, int seedIndex)
    {
        var imageDirectory = SharedStoragePaths.GetImageDirectory(contentRootPath);
        Directory.CreateDirectory(imageDirectory);

        var fileName = $"seed-location-{seedIndex:D2}.svg";
        var fullPath = Path.Combine(imageDirectory, fileName);
        if (!File.Exists(fullPath))
        {
            var escapedTitle = System.Security.SecurityElement.Escape(locationName) ?? "Location";
            var svg = $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="800" viewBox="0 0 1200 800">
                    <defs>
                        <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
                            <stop offset="0%" stop-color="#0f766e"/>
                            <stop offset="100%" stop-color="#38bdf8"/>
                        </linearGradient>
                    </defs>
                    <rect width="1200" height="800" fill="url(#bg)"/>
                    <circle cx="980" cy="170" r="120" fill="rgba(255,255,255,0.22)"/>
                    <circle cx="220" cy="660" r="170" fill="rgba(255,255,255,0.14)"/>
                    <text x="90" y="320" fill="#ffffff" font-size="72" font-family="Segoe UI, Arial, sans-serif" font-weight="700">Smart Tourism</text>
                    <text x="90" y="410" fill="#e2e8f0" font-size="42" font-family="Segoe UI, Arial, sans-serif">{{escapedTitle}}</text>
                    <text x="90" y="490" fill="#ccfbf1" font-size="28" font-family="Segoe UI, Arial, sans-serif">Seed preview image stored in SharedLibraries</text>
                </svg>
                """;

            await File.WriteAllTextAsync(fullPath, svg);
        }

        return SharedStoragePaths.ToPublicImagePath(fileName);
    }
}
