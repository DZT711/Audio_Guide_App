PRAGMA foreign_keys = ON;

-- Smart Tourism schema draft
-- Keep existing main table names to reduce code and migration impact.
-- SQLite-friendly for local/offline use and portable to SQL Server via EF Core.

CREATE TABLE Categories (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT
);

CREATE TABLE DashboardUsers (
    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    FullName TEXT,
    Role TEXT NOT NULL DEFAULT 'Owner'
        CHECK (Role IN ('Admin', 'Owner', 'Editor', 'DataAnalyst', 'Developer')),
    Email TEXT UNIQUE,
    Phone TEXT UNIQUE,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT
);

CREATE TABLE Languages (
    LanguageId INTEGER PRIMARY KEY AUTOINCREMENT,
    LangCode TEXT NOT NULL UNIQUE,
    LangName TEXT NOT NULL,
    NativeName TEXT,
    IsDefault INTEGER NOT NULL DEFAULT 0 CHECK (IsDefault IN (0, 1)),
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Voices (
    VoiceId INTEGER PRIMARY KEY AUTOINCREMENT,
    LanguageId INTEGER NOT NULL,
    VoiceCode TEXT NOT NULL UNIQUE,
    VoiceName TEXT NOT NULL,
    Gender TEXT CHECK (Gender IN ('Male', 'Female', 'Unknown')),
    Provider TEXT NOT NULL DEFAULT 'System'
        CHECK (Provider IN ('System', 'Azure', 'Google', 'Recorded', 'Other')),
    LocaleRegion TEXT,
    IsOfflineCapable INTEGER NOT NULL DEFAULT 0 CHECK (IsOfflineCapable IN (0, 1)),
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    FOREIGN KEY (LanguageId) REFERENCES Languages(LanguageId)
);

CREATE TABLE Tours (
    TourId INTEGER PRIMARY KEY AUTOINCREMENT,
    OwnerId INTEGER,
    Name TEXT NOT NULL,
    Description TEXT,
    EstimatedDurationMinutes INTEGER,
    CoverImageUrl TEXT,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);

CREATE TABLE Locations (
    LocationId INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER,
    OwnerId INTEGER,
    DefaultTourId INTEGER,
    Name TEXT NOT NULL,
    Description TEXT,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    Radius REAL NOT NULL DEFAULT 30.0,
    StandbyRadius REAL NOT NULL DEFAULT 12.0,
    Priority INTEGER NOT NULL DEFAULT 0,
    DebounceSeconds INTEGER NOT NULL DEFAULT 300,
    IsGpsTriggerEnabled INTEGER NOT NULL DEFAULT 1 CHECK (IsGpsTriggerEnabled IN (0, 1)),
    Address TEXT,
    Ward TEXT,
    District TEXT,
    City TEXT,
    ImageUrl TEXT,
    WebURL TEXT,
    Email TEXT,
    PhoneContact TEXT,
    EstablishedYear INTEGER,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId),
    FOREIGN KEY (DefaultTourId) REFERENCES Tours(TourId)
);

CREATE TABLE LocationImages (
    ImageId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER NOT NULL,
    ImageUrl TEXT NOT NULL,
    Description TEXT,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE
);

CREATE TABLE TourLocations (
    TourId INTEGER NOT NULL,
    LocationId INTEGER NOT NULL,
    SequenceOrder INTEGER NOT NULL,
    IsRequiredStop INTEGER NOT NULL DEFAULT 0 CHECK (IsRequiredStop IN (0, 1)),
    PRIMARY KEY (TourId, LocationId),
    FOREIGN KEY (TourId) REFERENCES Tours(TourId) ON DELETE CASCADE,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE
);

CREATE TABLE AudioContents (
    AudioId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER NOT NULL,
    LanguageId INTEGER NOT NULL,
    VoiceId INTEGER,
    Title TEXT NOT NULL,
    Description TEXT,
    SourceType TEXT NOT NULL
        CHECK (SourceType IN ('TTS', 'Recorded', 'Hybrid')),
    Script TEXT,
    FilePath TEXT,
    FileSizeBytes INTEGER,
    DurationSeconds INTEGER,
    Priority INTEGER NOT NULL DEFAULT 0,
    PlaybackMode TEXT NOT NULL DEFAULT 'Auto'
        CHECK (PlaybackMode IN ('Auto', 'Manual', 'QrOnly')),
    IsDownloadable INTEGER NOT NULL DEFAULT 1 CHECK (IsDownloadable IN (0, 1)),
    InterruptPolicy TEXT NOT NULL DEFAULT 'NotificationFirst'
        CHECK (InterruptPolicy IN ('NotificationFirst', 'NarrationFirst', 'Queue')),
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE,
    FOREIGN KEY (LanguageId) REFERENCES Languages(LanguageId),
    FOREIGN KEY (VoiceId) REFERENCES Voices(VoiceId),
    CONSTRAINT CK_Audio_SourcePayload CHECK (
        (SourceType = 'TTS' AND Script IS NOT NULL) OR
        (SourceType = 'Recorded' AND FilePath IS NOT NULL) OR
        (SourceType = 'Hybrid' AND Script IS NOT NULL AND FilePath IS NOT NULL)
    )
);

CREATE TABLE QRProfiles (
    QrProfileId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER,
    TourId INTEGER,
    AudioId INTEGER,
    CodeValue TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    TriggerType TEXT NOT NULL
        CHECK (TriggerType IN ('DirectAudio', 'LocationLanding', 'TourLanding')),
    AllowBypassGps INTEGER NOT NULL DEFAULT 1 CHECK (AllowBypassGps IN (0, 1)),
    StartAt TEXT,
    EndAt TEXT,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId),
    FOREIGN KEY (TourId) REFERENCES Tours(TourId),
    FOREIGN KEY (AudioId) REFERENCES AudioContents(AudioId)
);

CREATE TABLE AppDevices (
    DeviceId TEXT PRIMARY KEY,
    Platform TEXT NOT NULL CHECK (Platform IN ('Android', 'iOS')),
    DeviceModel TEXT,
    AppVersion TEXT,
    PreferredLanguageId INTEGER,
    LastKnownCountryCode TEXT,
    LastSeenAt TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (PreferredLanguageId) REFERENCES Languages(LanguageId)
);

CREATE TABLE DeviceSyncStates (
    DeviceId TEXT NOT NULL,
    SyncScope TEXT NOT NULL,
    LastFullSyncAt TEXT,
    LastDeltaToken TEXT,
    PRIMARY KEY (DeviceId, SyncScope),
    FOREIGN KEY (DeviceId) REFERENCES AppDevices(DeviceId) ON DELETE CASCADE
);

CREATE TABLE PlaybackEvents (
    PlaybackEventId INTEGER PRIMARY KEY AUTOINCREMENT,
    DeviceId TEXT,
    LocationId INTEGER,
    AudioId INTEGER,
    QrProfileId INTEGER,
    TriggerSource TEXT NOT NULL
        CHECK (TriggerSource IN ('GeofenceEnter', 'Nearby', 'QrScan', 'Manual')),
    EventType TEXT NOT NULL
        CHECK (EventType IN ('Queued', 'Started', 'Completed', 'Skipped', 'Stopped', 'CooldownBlocked')),
    EventAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ListeningSeconds INTEGER,
    QueuePosition INTEGER,
    BatteryPercent INTEGER,
    NetworkType TEXT,
    SessionId TEXT,
    FOREIGN KEY (DeviceId) REFERENCES AppDevices(DeviceId),
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId),
    FOREIGN KEY (AudioId) REFERENCES AudioContents(AudioId),
    FOREIGN KEY (QrProfileId) REFERENCES QRProfiles(QrProfileId)
);

CREATE TABLE LocationTrackingEvents (
    TrackingEventId INTEGER PRIMARY KEY AUTOINCREMENT,
    DeviceId TEXT NOT NULL,
    SessionId TEXT,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    AccuracyMeters REAL,
    SpeedMetersPerSecond REAL,
    BatteryPercent INTEGER,
    IsForeground INTEGER NOT NULL DEFAULT 1 CHECK (IsForeground IN (0, 1)),
    CapturedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DeviceId) REFERENCES AppDevices(DeviceId) ON DELETE CASCADE
);

CREATE TABLE ChangeRequests (
    RequestId INTEGER PRIMARY KEY AUTOINCREMENT,
    TargetTable TEXT NOT NULL,
    TargetId INTEGER,
    OwnerId INTEGER NOT NULL,
    RequestType TEXT NOT NULL CHECK (RequestType IN ('CREATE', 'UPDATE', 'DELETE')),
    NewDataJson TEXT NOT NULL,
    Reason TEXT,
    AdminNote TEXT,
    Status TEXT NOT NULL DEFAULT 'Pending'
        CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);

-- Mobile SQLite local tables
CREATE TABLE PlaybackCooldown (
    DeviceId TEXT NOT NULL,
    LocationId INTEGER NOT NULL,
    LastPlayedAt TEXT NOT NULL,
    CooldownUntil TEXT,
    PlayCount INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (DeviceId, LocationId),
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId)
);

CREATE TABLE LocalSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IX_Locations_CategoryId ON Locations(CategoryId);
CREATE INDEX IX_Locations_OwnerId ON Locations(OwnerId);
CREATE INDEX IX_Locations_Status ON Locations(Status);
CREATE INDEX IX_Locations_LatLng ON Locations(Latitude, Longitude);
CREATE INDEX IX_AudioContents_LocationId_LanguageId ON AudioContents(LocationId, LanguageId);
CREATE INDEX IX_AudioContents_Status ON AudioContents(Status);
CREATE INDEX IX_QRProfiles_CodeValue ON QRProfiles(CodeValue);
CREATE INDEX IX_PlaybackEvents_LocationId_EventAt ON PlaybackEvents(LocationId, EventAt);
CREATE INDEX IX_PlaybackEvents_AudioId_EventAt ON PlaybackEvents(AudioId, EventAt);
CREATE INDEX IX_PlaybackEvents_DeviceId_EventAt ON PlaybackEvents(DeviceId, EventAt);
CREATE INDEX IX_LocationTrackingEvents_DeviceId_CapturedAt ON LocationTrackingEvents(DeviceId, CapturedAt);

INSERT INTO Languages (LangCode, LangName, NativeName, IsDefault, Status)
VALUES
    ('vi-VN', 'Vietnamese', 'Tiếng Việt', 1, 1),
    ('en-US', 'English', 'English', 0, 1);
