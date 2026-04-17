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
        await EnsureSeedCategoriesAsync(context);

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
            var categories = await context.Categories.ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase);
            var owner = await context.DashboardUsers.FirstAsync(item => item.Username == "owner");
            var defaultCategoryId = await context.Categories
                .OrderBy(item => item.CategoryId)
                .Select(item => item.CategoryId)
                .FirstAsync();
            var foodCategoryId = ResolveCategoryId(categories, defaultCategoryId, "Food", "Markets & Food Halls");
            var landmarkCategoryId = ResolveCategoryId(categories, foodCategoryId, "Landmark", "Historical Site");

            context.Locations.AddRange(
                new Location
                {
                    Name = "Vinh Khanh Food Street",
                    CategoryId = foodCategoryId,
                    OwnerId = owner.UserId,
                    Description = "Night food corridor in Vinh Khanh used to demo clustered POIs, walking refresh, and media playback.",
                    Latitude = 10.759148,
                    Longitude = 106.704543,
                    Radius = 40,
                    StandbyRadius = 15,
                    Priority = 10,
                    DebounceSeconds = 240,
                    IsGpsTriggerEnabled = true,
                    Address = "Vinh Khanh Street, Ward 8, District 4, Ho Chi Minh City",
                    WebURL = "https://example.com/vinh-khanh-food-street",
                    Email = "owner@smarttour.local",
                    PhoneContact = "0900000005",
                    EstablishedYear = 2005,
                    Status = 1
                },
                new Location
                {
                    Name = "Oc Dao Vinh Khanh",
                    CategoryId = foodCategoryId,
                    OwnerId = owner.UserId,
                    Description = "Seafood-focused stop used to test food-category filtering and multi-audio playback.",
                    Latitude = 10.759576,
                    Longitude = 106.703987,
                    Radius = 35,
                    StandbyRadius = 12,
                    Priority = 8,
                    DebounceSeconds = 180,
                    IsGpsTriggerEnabled = true,
                    Address = "212 Vinh Khanh Street, Ward 8, District 4, Ho Chi Minh City",
                    EstablishedYear = 1999,
                    Status = 1
                },
                new Location
                {
                    Name = "Xom Chieu Market Gate",
                    CategoryId = landmarkCategoryId,
                    OwnerId = owner.UserId,
                    Description = "Busy local market gateway for QR scan, nearby trigger, and anonymous route analytics.",
                    Latitude = 10.761205,
                    Longitude = 106.702841,
                    Radius = 38,
                    StandbyRadius = 14,
                    Priority = 7,
                    DebounceSeconds = 180,
                    IsGpsTriggerEnabled = true,
                    Address = "Xom Chieu Market, Ward 14, District 4, Ho Chi Minh City",
                    EstablishedYear = 1985,
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
                .Take(3)
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
                    Name = "Vinh Khanh Starter Tour",
                    Description = "A short curated walk connecting the seeded Vinh Khanh food and market stops.",
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
        await NormalizeManagedMediaPathsAsync(context);
    }

    private static async Task EnsureSeedCategoriesAsync(DBContext context)
    {
        var existingCategories = await context.Categories
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase);

        var requiredCategories = new[]
        {
            new Category { Name = "Historical Site", Description = "Places with cultural and historical value." },
            new Category { Name = "Food", Description = "Restaurants, cafes, and local specialties." },
            new Category { Name = "Drinks", Description = "Bustling markets and curated food halls." },
            new Category { Name = "Markets & Food Halls", Description = "Bustling markets and curated food halls." },
            new Category { Name = "Landmark", Description = "High-priority tourist landmarks and city icons." }
        };

        var hasChanges = false;
        foreach (var requiredCategory in requiredCategories)
        {
            if (existingCategories.TryGetValue(requiredCategory.Name, out var existingCategory))
            {
                if (!string.Equals(existingCategory.Description, requiredCategory.Description, StringComparison.Ordinal))
                {
                    existingCategory.Description = requiredCategory.Description;
                    hasChanges = true;
                }

                continue;
            }

            context.Categories.Add(requiredCategory);
            existingCategories[requiredCategory.Name] = requiredCategory;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
        }
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
        var foodCategoryId = ResolveCategoryId(
            categories,
            historicalCategoryId,
            "Food",
            "Markets & Food Halls");
        var landmarkCategoryId = categories.TryGetValue("Landmark", out var landmarkCategory)
            ? landmarkCategory.CategoryId
            : historicalCategoryId;

        var vinhKhanhFoodStreet = await EnsureAnalyticsLocationAsync(
            context,
            "Vinh Khanh Food Street",
            ownerUser.UserId,
            foodCategoryId,
            "Night food corridor in Vinh Khanh used to demo clustered POIs, walking refresh, and media playback.",
            10.759148,
            106.704543,
            "Vinh Khanh Street, Ward 8, District 4, Ho Chi Minh City",
            2005,
            "Ben Nha Rong");

        var ocDaoVinhKhanh = await EnsureAnalyticsLocationAsync(
            context,
            "Oc Dao Vinh Khanh",
            ownerUser.UserId,
            foodCategoryId,
            "Seafood-focused stop used to test food-category filtering and multi-audio playback.",
            10.759576,
            106.703987,
            "212 Vinh Khanh Street, Ward 8, District 4, Ho Chi Minh City",
            1999,
            "Ben Thanh Bus Stop");

        var xomChieuMarketGate = await EnsureAnalyticsLocationAsync(
            context,
            "Xom Chieu Market Gate",
            ownerUser.UserId,
            landmarkCategoryId,
            "Busy local market gateway for QR scan, nearby trigger, and anonymous route analytics.",
            10.761205,
            106.702841,
            "Xom Chieu Market, Ward 14, District 4, Ho Chi Minh City",
            1985,
            "Khanh Hoi Riverside Pier");

        var khanhHoiCanalViewpoint = await EnsureAnalyticsLocationAsync(
            context,
            "Khanh Hoi Canal Viewpoint",
            adminUser.UserId,
            landmarkCategoryId,
            "Riverfront viewpoint near Khanh Hoi used to compare owner and admin sample content.",
            10.757843,
            106.707071,
            "Hoang Dieu riverside, Ward 9, District 4, Ho Chi Minh City",
            2018,
            "Opera House Square");

        var audioByLocationId = new Dictionary<int, Audio>
        {
            [vinhKhanhFoodStreet.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                vinhKhanhFoodStreet.LocationId,
                "Vinh Khanh Street Welcome",
                "Overview audio for the Vinh Khanh night food corridor.",
                "Chao mung ban den pho am thuc Vinh Khanh, diem mau de kiem tra tai lai POI, anh minh hoa va audio cong khai tren dien thoai.",
                96,
                "Local Guide",
                "Female",
                "vi-VN",
                "Ben Nha Rong Introduction"),
            [ocDaoVinhKhanh.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                ocDaoVinhKhanh.LocationId,
                "Oc Dao Arrival Guide",
                "Seafood stop narration for the Vinh Khanh sample set.",
                "This sample stop focuses on food discovery, category filtering, and multiple playable audio variants in the same district.",
                88,
                "City Host Minh",
                "Male",
                "en-US",
                "Ben Thanh Arrival Guide"),
            [xomChieuMarketGate.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                xomChieuMarketGate.LocationId,
                "Xom Chieu Market Stories",
                "Market-area narration for QR and nearby trigger analytics.",
                "Xom Chieu Market gives the sample dataset a busier local context for route history and repeated refresh testing.",
                92,
                "River Guide Lan",
                "Female",
                "vi-VN",
                "Khanh Hoi Riverside Route"),
            [khanhHoiCanalViewpoint.LocationId] = await EnsureAnalyticsAudioAsync(
                context,
                khanhHoiCanalViewpoint.LocationId,
                "Khanh Hoi Canal View",
                "Riverfront narration for the Khanh Hoi sample viewpoint.",
                "This canal-side sample helps compare owner and admin content while staying inside the District 4 demo area.",
                104,
                "Culture Guide Alex",
                "Male",
                "en-US",
                "Opera House Square Highlights")
        };

        await EnsureAnalyticsRecordedAudioAsync(
            context,
            contentRootPath,
            vinhKhanhFoodStreet.LocationId,
            "Vinh Khanh Recorded Demo",
            "Synthetic recorded clip used to verify Android file playback on the Vinh Khanh sample stop.",
            null,
            4,
            "Recorded Demo Lan",
            "Female",
            "vi-VN",
            "Recorded",
            "demo-vinh-khanh-recorded.wav",
            392,
            554,
            "Ben Nha Rong Recorded Demo");

        await EnsureAnalyticsRecordedAudioAsync(
            context,
            contentRootPath,
            ocDaoVinhKhanh.LocationId,
            "Oc Dao Hybrid Demo",
            "Hybrid sample that keeps both a script and a stored file for fallback testing in Vinh Khanh.",
            "This hybrid sample lets the mobile app switch between stored audio and on-device speech while exploring the food street.",
            4,
            "Recorded Demo Minh",
            "Male",
            "en-US",
            "Hybrid",
            "demo-oc-dao-hybrid.wav",
            523,
            659,
            "Ben Thanh Hybrid Demo");

        await EnsureAnalyticsTourAsync(
            context,
            "Vinh Khanh Night Food Walk",
            ownerUser.UserId,
            "Owner-facing sample tour that connects the Vinh Khanh food corridor and nearby market stop.",
            [vinhKhanhFoodStreet, ocDaoVinhKhanh, xomChieuMarketGate],
            "District 4 River Stories");

        await EnsureAnalyticsTourAsync(
            context,
            "Khanh Hoi Riverside Loop",
            adminUser.UserId,
            "Admin-facing sample loop that stays inside the Khanh Hoi and Xom Chieu area.",
            [xomChieuMarketGate, khanhHoiCanalViewpoint],
            "Downtown Culture Sprint");

        var hasAnalyticsPlayback = await context.PlaybackEvents
            .AnyAsync(item => item.DeviceId != null && item.DeviceId.StartsWith("analytics-demo-"));

        if (hasAnalyticsPlayback)
        {
            return;
        }

        var seededLocations = new[] { vinhKhanhFoodStreet, ocDaoVinhKhanh, xomChieuMarketGate, khanhHoiCanalViewpoint };
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

    private static async Task NormalizeManagedMediaPathsAsync(DBContext context)
    {
        var hasChanges = false;

        var locations = await context.Locations
            .Include(item => item.Images)
            .ToListAsync();

        foreach (var location in locations)
        {
            var normalizedPreferenceImageUrl = SharedStoragePaths.NormalizePublicImagePath(location.PreferenceImageUrl);
            if (!string.Equals(location.PreferenceImageUrl, normalizedPreferenceImageUrl, StringComparison.Ordinal))
            {
                location.PreferenceImageUrl = normalizedPreferenceImageUrl;
                hasChanges = true;
            }

            foreach (var image in location.Images)
            {
                var normalizedImageUrl = SharedStoragePaths.NormalizePublicImagePath(image.ImageUrl) ?? image.ImageUrl;
                if (!string.Equals(image.ImageUrl, normalizedImageUrl, StringComparison.Ordinal))
                {
                    image.ImageUrl = normalizedImageUrl;
                    hasChanges = true;
                }
            }
        }

        var audioItems = await context.AudioContents
            .Where(item => item.FilePath != null)
            .ToListAsync();

        foreach (var audio in audioItems)
        {
            var normalizedAudioPath = SharedStoragePaths.NormalizePublicAudioPath(audio.FilePath);
            if (!string.Equals(audio.FilePath, normalizedAudioPath, StringComparison.Ordinal))
            {
                audio.FilePath = normalizedAudioPath;
                hasChanges = true;
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
        int establishedYear,
        params string[] aliases)
    {
        var candidateNames = aliases
            .Append(name)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingLocation = await context.Locations
            .FirstOrDefaultAsync(item => candidateNames.Contains(item.Name));
        if (existingLocation is not null)
        {
            existingLocation.Name = name;
            existingLocation.OwnerId = ownerId;
            existingLocation.CategoryId = categoryId;
            existingLocation.Description = description;
            existingLocation.Latitude = latitude;
            existingLocation.Longitude = longitude;
            existingLocation.Radius = 36;
            existingLocation.StandbyRadius = 12;
            existingLocation.Priority = 7;
            existingLocation.DebounceSeconds = 180;
            existingLocation.IsGpsTriggerEnabled = true;
            existingLocation.Address = address;
            existingLocation.EstablishedYear = establishedYear;
            existingLocation.Status = 1;
            existingLocation.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
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
        string languageCode,
        params string[] aliases)
    {
        var candidateTitles = aliases
            .Append(title)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingAudio = await context.AudioContents
            .FirstOrDefaultAsync(item => item.LocationId == locationId && candidateTitles.Contains(item.Title))
            ?? await context.AudioContents.FirstOrDefaultAsync(item => candidateTitles.Contains(item.Title));
        if (existingAudio is not null)
        {
            existingAudio.LocationId = locationId;
            existingAudio.Title = title;
            existingAudio.Description = description;
            existingAudio.LanguageCode = languageCode;
            existingAudio.SourceType = "TTS";
            existingAudio.Script = script;
            existingAudio.DurationSeconds = durationSeconds;
            existingAudio.VoiceName = voiceName;
            existingAudio.VoiceGender = voiceGender;
            existingAudio.Priority = 6;
            existingAudio.PlaybackMode = "Auto";
            existingAudio.InterruptPolicy = "NotificationFirst";
            existingAudio.IsDownloadable = true;
            existingAudio.Status = 1;
            existingAudio.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
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
        int endFrequencyHz,
        params string[] aliases)
    {
        var audioFile = await EnsureSeedAudioClipAsync(
            contentRootPath,
            fileName,
            durationSeconds,
            startFrequencyHz,
            endFrequencyHz);

        var candidateTitles = aliases
            .Append(title)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingAudio = await context.AudioContents
            .FirstOrDefaultAsync(item => item.LocationId == locationId && candidateTitles.Contains(item.Title))
            ?? await context.AudioContents.FirstOrDefaultAsync(item => candidateTitles.Contains(item.Title));
        if (existingAudio is not null)
        {
            existingAudio.LocationId = locationId;
            existingAudio.Title = title;
            existingAudio.Description = description;
            existingAudio.LanguageCode = languageCode;
            existingAudio.SourceType = sourceType;
            existingAudio.Script = script;
            existingAudio.FilePath = SharedStoragePaths.ToPublicAudioPath(audioFile.Name);
            existingAudio.FileSizeBytes = checked((int)audioFile.Length);
            existingAudio.DurationSeconds = durationSeconds;
            existingAudio.VoiceName = voiceName;
            existingAudio.VoiceGender = voiceGender;
            existingAudio.Priority = 7;
            existingAudio.PlaybackMode = "Auto";
            existingAudio.InterruptPolicy = "NotificationFirst";
            existingAudio.IsDownloadable = true;
            existingAudio.Status = 1;
            existingAudio.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
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
        IReadOnlyList<Location> stops,
        params string[] aliases)
    {
        var candidateNames = aliases
            .Append(name)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTour = await context.Tours
            .Include(item => item.Stops)
            .FirstOrDefaultAsync(item => candidateNames.Contains(item.Name));

        var metrics = TourPlanningService.CalculateMetrics(
            stops,
            TourDefaults.DefaultWalkingSpeedKph,
            TourDefaults.DefaultStartTime);

        var tour = existingTour ?? new Tour
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

        tour.OwnerId = ownerId;
        tour.Name = name;
        tour.Description = description;
        tour.TotalDistanceKm = metrics.TotalDistanceKm;
        tour.EstimatedDurationMinutes = metrics.EstimatedDurationMinutes;
        tour.WalkingSpeedKph = TourDefaults.DefaultWalkingSpeedKph;
        tour.StartTime = metrics.StartTime;
        tour.Status = 1;
        tour.UpdatedAt = DateTime.UtcNow;

        if (existingTour is null)
        {
            context.Tours.Add(tour);
        }

        if (tour.Stops.Count > 0)
        {
            context.TourLocations.RemoveRange(tour.Stops);
            tour.Stops.Clear();
        }

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
