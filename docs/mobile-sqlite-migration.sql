PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS LocalSettings (
    SettingKey TEXT PRIMARY KEY NOT NULL,
    SettingValue TEXT NULL,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS CachedCategories (
    Id INTEGER PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Status INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);

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

CREATE TABLE IF NOT EXISTS DeviceSyncStates (
    SyncScope TEXT PRIMARY KEY NOT NULL,
    LastFullSyncAtUtc TEXT NULL,
    LastDeltaToken TEXT NULL
);

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

CREATE TABLE IF NOT EXISTS PlaybackCooldown (
    DeviceId TEXT NOT NULL,
    LocationId INTEGER NOT NULL,
    LastPlayedAtUtc TEXT NOT NULL,
    CooldownUntilUtc TEXT NOT NULL,
    PlayCount INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (DeviceId, LocationId)
);

PRAGMA user_version = 1;
