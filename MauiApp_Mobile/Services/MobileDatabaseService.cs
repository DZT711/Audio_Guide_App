using System.Globalization;
using System.Text.Json;
using Project_SharedClassLibrary.Contracts;
using SQLite;

namespace MauiApp_Mobile.Services;

public sealed class MobileDatabaseService
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;
    private bool _initialized;

    public static MobileDatabaseService Instance { get; } = new();

    public string DatabasePath { get; } = Path.Combine(FileSystem.Current.AppDataDirectory, "smarttour-mobile.db3");

    private MobileDatabaseService()
    {
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            _connection = new SQLiteAsyncConnection(DatabasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache);

            await ApplyMigrationsAsync();
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var row = await connection.Table<LocalSettingEntity>()
            .Where(item => item.SettingKey == key)
            .FirstOrDefaultAsync();
        return row?.SettingValue;
    }

    public async Task SetSettingAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.InsertOrReplaceAsync(new LocalSettingEntity
        {
            SettingKey = key,
            SettingValue = value,
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });
    }

    public async Task SaveCatalogSnapshotAsync(PublicCatalogSnapshotDto snapshot, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.RunInTransactionAsync(transaction =>
        {
            transaction.DeleteAll<CachedCategoryEntity>();
            transaction.DeleteAll<CachedLocationEntity>();
            transaction.DeleteAll<CachedAudioTrackEntity>();

            foreach (var category in snapshot.Categories)
            {
                transaction.InsertOrReplace(new CachedCategoryEntity
                {
                    Id = category.Id,
                    Name = category.Name,
                    Description = category.Description,
                    Status = category.Status,
                    CreatedAtUtc = category.CreatedAt,
                    UpdatedAtUtc = category.UpdatedAt
                });
            }

            foreach (var location in snapshot.Locations)
            {
                transaction.InsertOrReplace(new CachedLocationEntity
                {
                    Id = location.Id,
                    CategoryId = location.CategoryId,
                    Category = location.Category,
                    OwnerId = location.OwnerId,
                    OwnerName = location.OwnerName,
                    Name = location.Name,
                    Description = location.Description,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Radius = location.Radius,
                    StandbyRadius = location.StandbyRadius,
                    Priority = location.Priority,
                    DebounceSeconds = location.DebounceSeconds,
                    IsGpsTriggerEnabled = location.IsGpsTriggerEnabled,
                    Address = location.Address,
                    PreferenceImageUrl = location.PreferenceImageUrl,
                    CoverImageUrl = location.CoverImageUrl,
                    ImageUrlsJson = JsonSerializer.Serialize(location.ImageUrls, JsonOptions),
                    WebURL = location.WebURL,
                    Email = location.Email,
                    Phone = location.Phone,
                    EstablishedYear = location.EstablishedYear,
                    AudioCount = location.AudioCount,
                    AvailableVoiceGendersJson = JsonSerializer.Serialize(location.AvailableVoiceGenders, JsonOptions),
                    Status = location.Status,
                    CreatedAtUtc = location.CreatedAt,
                    UpdatedAtUtc = location.UpdatedAt
                });
            }

            foreach (var audioTrack in snapshot.AudioTracks)
            {
                transaction.InsertOrReplace(new CachedAudioTrackEntity
                {
                    Id = audioTrack.Id,
                    LocationId = audioTrack.LocationId,
                    LocationName = audioTrack.LocationName,
                    Language = audioTrack.Language,
                    LanguageName = audioTrack.LanguageName,
                    Title = audioTrack.Title,
                    Description = audioTrack.Description,
                    SourceType = audioTrack.SourceType,
                    Script = audioTrack.Script,
                    AudioURL = audioTrack.AudioURL,
                    Duration = audioTrack.Duration,
                    VoiceName = audioTrack.VoiceName,
                    VoiceGender = audioTrack.VoiceGender,
                    Priority = audioTrack.Priority,
                    IsDefault = audioTrack.IsDefault
                });
            }
        });

        await SetSyncStateAsync(
            "catalog",
            snapshot.RefreshedAtUtc == default ? DateTimeOffset.UtcNow : new DateTimeOffset(snapshot.RefreshedAtUtc, TimeSpan.Zero),
            snapshot.RefreshedAtUtc == default ? DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) : snapshot.RefreshedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            cancellationToken);
    }

    public async Task<PublicCatalogSnapshotDto> LoadCatalogSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var categories = await connection.Table<CachedCategoryEntity>().OrderBy(item => item.Name).ToListAsync();
        var locations = await connection.Table<CachedLocationEntity>().OrderBy(item => item.Name).ToListAsync();
        var audioTracks = (await connection.Table<CachedAudioTrackEntity>().ToListAsync())
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Id)
            .ToList();

        var syncState = await connection.Table<DeviceSyncStateEntity>()
            .Where(item => item.SyncScope == "catalog")
            .FirstOrDefaultAsync();

        return new PublicCatalogSnapshotDto
        {
            RefreshedAtUtc = ParseDateTime(syncState?.LastFullSyncAtUtc) ?? DateTime.UtcNow,
            Categories = categories.Select(item => new CategoryDto
            {
                Id = item.Id,
                Name = item.Name ?? string.Empty,
                Description = item.Description,
                Status = item.Status,
                CreatedAt = item.CreatedAtUtc,
                UpdatedAt = item.UpdatedAtUtc
            }).ToList(),
            Locations = locations.Select(item => new LocationDto
            {
                Id = item.Id,
                CategoryId = item.CategoryId,
                Category = item.Category ?? string.Empty,
                OwnerId = item.OwnerId,
                OwnerName = item.OwnerName,
                Name = item.Name ?? string.Empty,
                Description = item.Description,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Radius = item.Radius,
                StandbyRadius = item.StandbyRadius,
                Priority = item.Priority,
                DebounceSeconds = item.DebounceSeconds,
                IsGpsTriggerEnabled = item.IsGpsTriggerEnabled,
                Address = item.Address,
                PreferenceImageUrl = item.PreferenceImageUrl,
                CoverImageUrl = item.CoverImageUrl,
                ImageUrls = DeserializeStringList(item.ImageUrlsJson),
                WebURL = item.WebURL,
                Email = item.Email,
                Phone = item.Phone,
                EstablishedYear = item.EstablishedYear,
                AudioCount = item.AudioCount,
                AvailableVoiceGenders = DeserializeStringList(item.AvailableVoiceGendersJson),
                Status = item.Status,
                CreatedAt = item.CreatedAtUtc,
                UpdatedAt = item.UpdatedAtUtc
            }).ToList(),
            AudioTracks = audioTracks.Select(item => new PublicAudioTrackDto
            {
                Id = item.Id,
                LocationId = item.LocationId,
                LocationName = item.LocationName ?? string.Empty,
                Language = item.Language ?? "vi-VN",
                LanguageName = item.LanguageName,
                Title = item.Title ?? string.Empty,
                Description = item.Description,
                SourceType = item.SourceType ?? "TTS",
                Script = item.Script,
                AudioURL = item.AudioURL,
                Duration = item.Duration,
                VoiceName = item.VoiceName,
                VoiceGender = item.VoiceGender,
                Priority = item.Priority,
                IsDefault = item.IsDefault
            }).ToList()
        };
    }

    public async Task LogTrackingEventAsync(LocationTrackingRecord record, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.InsertAsync(new LocationTrackingEventEntity
        {
            DeviceId = record.DeviceId,
            SessionId = record.SessionId,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            AccuracyMeters = record.AccuracyMeters,
            SpeedMetersPerSecond = record.SpeedMetersPerSecond,
            BatteryPercent = record.BatteryPercent,
            IsForeground = record.IsForeground,
            CapturedAtUtc = record.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture)
        });
    }

    public async Task SetPlaybackCooldownAsync(string deviceId, int locationId, DateTimeOffset lastPlayedAtUtc, DateTimeOffset cooldownUntilUtc, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            """
            INSERT OR REPLACE INTO PlaybackCooldown(DeviceId, LocationId, LastPlayedAtUtc, CooldownUntilUtc, PlayCount)
            VALUES (?, ?, ?, ?, COALESCE((SELECT PlayCount + 1 FROM PlaybackCooldown WHERE DeviceId = ? AND LocationId = ?), 1));
            """,
            deviceId,
            locationId,
            lastPlayedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            cooldownUntilUtc.ToString("O", CultureInfo.InvariantCulture),
            deviceId,
            locationId);
    }

    public async Task<DateTimeOffset?> GetPlaybackCooldownUntilAsync(string deviceId, int locationId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var value = await connection.ExecuteScalarAsync<string?>(
            "SELECT CooldownUntilUtc FROM PlaybackCooldown WHERE DeviceId = ? AND LocationId = ? LIMIT 1;",
            deviceId,
            locationId);
        return ParseDateTimeOffset(value);
    }

    public async Task<IReadOnlyDictionary<int, DateTimeOffset>> GetPlaybackCooldownsAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PlaybackCooldownRow>(
            "SELECT LocationId, CooldownUntilUtc FROM PlaybackCooldown WHERE DeviceId = ?;",
            deviceId);

        return rows
            .Select(row => new
            {
                row.LocationId,
                CooldownUntilUtc = ParseDateTimeOffset(row.CooldownUntilUtc)
            })
            .Where(item => item.CooldownUntilUtc.HasValue)
            .ToDictionary(
                item => item.LocationId,
                item => item.CooldownUntilUtc!.Value);
    }

    public async Task SetSyncStateAsync(string scope, DateTimeOffset lastFullSyncAtUtc, string? deltaToken, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.InsertOrReplaceAsync(new DeviceSyncStateEntity
        {
            SyncScope = scope,
            LastFullSyncAtUtc = lastFullSyncAtUtc.ToString("O", CultureInfo.InvariantCulture),
            LastDeltaToken = deltaToken
        });
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return _connection!;
    }

    private async Task ApplyMigrationsAsync()
    {
        var connection = _connection!;
        var version = await connection.ExecuteScalarAsync<int>("PRAGMA user_version;");
        if (version >= CurrentSchemaVersion)
        {
            return;
        }

        if (version < 1)
        {
            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS LocalSettings (
                    SettingKey TEXT PRIMARY KEY NOT NULL,
                    SettingValue TEXT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS CachedCategories (
                    Id INTEGER PRIMARY KEY NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    Status INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS CachedLocations (
                    Id INTEGER PRIMARY KEY NOT NULL,
                    CategoryId INTEGER NOT NULL,
                    Category TEXT NULL,
                    OwnerId INTEGER NULL,
                    OwnerName TEXT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    Radius REAL NOT NULL,
                    StandbyRadius REAL NOT NULL,
                    Priority INTEGER NOT NULL,
                    DebounceSeconds INTEGER NOT NULL,
                    IsGpsTriggerEnabled INTEGER NOT NULL,
                    Address TEXT NULL,
                    PreferenceImageUrl TEXT NULL,
                    CoverImageUrl TEXT NULL,
                    ImageUrlsJson TEXT NULL,
                    WebURL TEXT NULL,
                    Email TEXT NULL,
                    Phone TEXT NULL,
                    EstablishedYear INTEGER NOT NULL,
                    AudioCount INTEGER NOT NULL,
                    AvailableVoiceGendersJson TEXT NULL,
                    Status INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS CachedAudioTracks (
                    Id INTEGER PRIMARY KEY NOT NULL,
                    LocationId INTEGER NOT NULL,
                    LocationName TEXT NOT NULL,
                    Language TEXT NOT NULL,
                    LanguageName TEXT NULL,
                    Title TEXT NOT NULL,
                    Description TEXT NULL,
                    SourceType TEXT NOT NULL,
                    Script TEXT NULL,
                    AudioURL TEXT NULL,
                    Duration INTEGER NOT NULL,
                    VoiceName TEXT NULL,
                    VoiceGender TEXT NULL,
                    Priority INTEGER NOT NULL,
                    IsDefault INTEGER NOT NULL
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS DeviceSyncStates (
                    SyncScope TEXT PRIMARY KEY NOT NULL,
                    LastFullSyncAtUtc TEXT NULL,
                    LastDeltaToken TEXT NULL
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS LocationTrackingEvents (
                    TrackingEventId INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId TEXT NOT NULL,
                    SessionId TEXT NULL,
                    Latitude REAL NOT NULL,
                    Longitude REAL NOT NULL,
                    AccuracyMeters REAL NULL,
                    SpeedMetersPerSecond REAL NULL,
                    BatteryPercent INTEGER NULL,
                    IsForeground INTEGER NOT NULL,
                    CapturedAtUtc TEXT NOT NULL
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS PlaybackCooldown (
                    DeviceId TEXT NOT NULL,
                    LocationId INTEGER NOT NULL,
                    LastPlayedAtUtc TEXT NOT NULL,
                    CooldownUntilUtc TEXT NOT NULL,
                    PlayCount INTEGER NOT NULL DEFAULT 1,
                    PRIMARY KEY (DeviceId, LocationId)
                );
                """);

            await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS PlaybackHistory (
                    HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                    PlaceId TEXT NOT NULL,
                    PlacePayloadJson TEXT NOT NULL,
                    PlayedAtUtc TEXT NOT NULL
                );
                """);
        }

        await connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion};");
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
    }

    private static DateTime? ParseDateTime(string? value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    public async Task SavePlaybackHistoryAsync(string placeId, string placePayloadJson, DateTimeOffset playedAtUtc, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM PlaybackHistory WHERE PlaceId = ?;", placeId);
        await connection.ExecuteAsync(
            "INSERT INTO PlaybackHistory(PlaceId, PlacePayloadJson, PlayedAtUtc) VALUES (?, ?, ?);",
            placeId,
            placePayloadJson,
            playedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await connection.ExecuteAsync(
            """
            DELETE FROM PlaybackHistory
            WHERE HistoryId NOT IN (
                SELECT HistoryId FROM PlaybackHistory ORDER BY PlayedAtUtc DESC LIMIT 40
            );
            """);
    }

    public async Task<IReadOnlyList<PlaybackHistoryRecord>> LoadPlaybackHistoryAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PlaybackHistoryRow>(
            "SELECT HistoryId, PlaceId, PlacePayloadJson, PlayedAtUtc FROM PlaybackHistory ORDER BY PlayedAtUtc DESC;");

        return rows
            .Select(row => new PlaybackHistoryRecord(
                row.HistoryId,
                row.PlaceId,
                row.PlacePayloadJson,
                ParseDateTimeOffset(row.PlayedAtUtc) ?? DateTimeOffset.UtcNow))
            .ToList();
    }

    public async Task DeletePlaybackHistoryAsync(string placeId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM PlaybackHistory WHERE PlaceId = ?;", placeId);
    }

    public async Task ClearPlaybackHistoryAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync("DELETE FROM PlaybackHistory;");
    }

    [Table("LocalSettings")]
    private sealed class LocalSettingEntity
    {
        [PrimaryKey]
        public string SettingKey { get; set; } = string.Empty;
        public string? SettingValue { get; set; }
        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    [Table("CachedCategories")]
    private sealed class CachedCategoryEntity
    {
        [PrimaryKey]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    [Table("CachedLocations")]
    private sealed class CachedLocationEntity
    {
        [PrimaryKey]
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string? Category { get; set; }
        public int? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; }
        public double StandbyRadius { get; set; }
        public int Priority { get; set; }
        public int DebounceSeconds { get; set; }
        public bool IsGpsTriggerEnabled { get; set; }
        public string? Address { get; set; }
        public string? PreferenceImageUrl { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? ImageUrlsJson { get; set; }
        public string? WebURL { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int EstablishedYear { get; set; }
        public int AudioCount { get; set; }
        public string? AvailableVoiceGendersJson { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    [Table("CachedAudioTracks")]
    private sealed class CachedAudioTrackEntity
    {
        [PrimaryKey]
        public int Id { get; set; }
        public int LocationId { get; set; }
        public string? LocationName { get; set; }
        public string? Language { get; set; }
        public string? LanguageName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? SourceType { get; set; }
        public string? Script { get; set; }
        public string? AudioURL { get; set; }
        public int Duration { get; set; }
        public string? VoiceName { get; set; }
        public string? VoiceGender { get; set; }
        public int Priority { get; set; }
        public bool IsDefault { get; set; }
    }

    [Table("DeviceSyncStates")]
    private sealed class DeviceSyncStateEntity
    {
        [PrimaryKey]
        public string SyncScope { get; set; } = string.Empty;
        public string? LastFullSyncAtUtc { get; set; }
        public string? LastDeltaToken { get; set; }
    }

    [Table("LocationTrackingEvents")]
    private sealed class LocationTrackingEventEntity
    {
        [PrimaryKey, AutoIncrement]
        public int TrackingEventId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public double? SpeedMetersPerSecond { get; set; }
        public int? BatteryPercent { get; set; }
        public bool IsForeground { get; set; }
        public string CapturedAtUtc { get; set; } = string.Empty;
    }

    private sealed class PlaybackHistoryRow
    {
        public int HistoryId { get; set; }
        public string PlaceId { get; set; } = string.Empty;
        public string PlacePayloadJson { get; set; } = string.Empty;
        public string PlayedAtUtc { get; set; } = string.Empty;
    }

    private sealed class PlaybackCooldownRow
    {
        public int LocationId { get; set; }
        public string CooldownUntilUtc { get; set; } = string.Empty;
    }

}

public sealed record LocationTrackingRecord(
    string DeviceId,
    string SessionId,
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    double? SpeedMetersPerSecond,
    int? BatteryPercent,
    bool IsForeground,
    DateTimeOffset CapturedAtUtc);

public sealed record PlaybackHistoryRecord(
    int HistoryId,
    string PlaceId,
    string PlacePayloadJson,
    DateTimeOffset PlayedAtUtc);
