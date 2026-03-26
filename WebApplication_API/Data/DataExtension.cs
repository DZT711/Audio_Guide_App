using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Security;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Model;

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
                    Address = "1 Nguyen Tat Thanh, Ward 12",
                    Ward = "Ward 12",
                    City = "Ho Chi Minh City",
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
                    Address = "Le Loi Street",
                    Ward = "Ben Thanh",
                    City = "Ho Chi Minh City",
                    EstablishedYear = 2000,
                    Status = 1
                });

            await context.SaveChangesAsync();
        }

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
}
