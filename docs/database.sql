PRAGMA foreign_keys = ON;

-- Smart Tourism database plan
-- Goal:
-- 1. Keep existing main table names to reduce API/model rename cost
-- 2. Split tables by delivery phase based on your specification
-- 3. Explain the function of each table directly in comments
--
-- PHASE RULES
-- POC:
-- - Must support GPS tracking, geofence trigger, audio auto-play, cooldown, basic CMS content
-- - Must work for MAUI mobile + current web/API
-- Mobile Local Database:
-- - SQLite tables stored on device for offline behavior, queue control, local settings
-- MVP Scale-up:
-- - Tables for admin workflow, analytics, QR campaign, tours, sync, multilingual growth

-- =========================================================
-- POC IMPORTANT PART
-- =========================================================

-- Function:
-- Group Locations by type such as historical site, food, bus stop, landmark.
-- Needed in POC for filtering and basic CMS organization.
CREATE TABLE Categories (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description LONGTEXT,
    Icon TEXT,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME
);

-- Function:
-- Manage dashboard users for web CMS such as admin, owner, editor.
-- Needed in POC because POI owners manage content from web.
CREATE TABLE DashboardUsers (
    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    FullName TEXT,
    Role TEXT NOT NULL DEFAULT 'User'
        CHECK (Role IN ('Admin', 'User', 'Editor', 'DataAnalyst', 'Developer')),
    Email TEXT UNIQUE,
    Phone TEXT UNIQUE,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME
);

-- Function:
-- Main POI table.
-- Stores geofence coordinates, trigger radius, cooldown priority, and presentation metadata.
-- This is the most important POC table because your app revolves around GPS + POI detection.
CREATE TABLE Locations (
    LocationId INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER,
    OwnerId INTEGER,
    Name TEXT NOT NULL,
    Description LONGTEXT,
    Latitude REAL NOT NULL,
    Longitude REAL NOT NULL,
    Radius REAL NOT NULL DEFAULT 30.0,
    StandbyRadius REAL NOT NULL DEFAULT 12.0,
    Priority INTEGER NOT NULL DEFAULT 0,
    DebounceSeconds INTEGER NOT NULL DEFAULT 300,
    IsGpsTriggerEnabled INTEGER NOT NULL DEFAULT 1 CHECK (IsGpsTriggerEnabled IN (0, 1)),
    Address LONGTEXT,
    WebURL TEXT,
    Email TEXT,
    PhoneContact TEXT,
    EstablishedYear INTEGER,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);

-- Function:
-- Narration content for each location.
-- Supports both TTS script and recorded file so you can compare memory cost and quality.
-- Needed in POC for auto presentation and multilingual audio.
CREATE TABLE AudioContents (
    AudioId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER NOT NULL,
    LanguageCode TEXT NOT NULL DEFAULT 'vi-VN',
    Title TEXT NOT NULL,
    Description LONGTEXT,
    SourceType TEXT NOT NULL DEFAULT 'TTS'
        CHECK (SourceType IN ('TTS', 'Recorded', 'Hybrid')),
    Script TEXT,
    FilePath TEXT,
    FileSizeBytes INTEGER,
    DurationSeconds INTEGER,
    VoiceName TEXT,
    VoiceGender TEXT CHECK (VoiceGender IN ('Male', 'Female')),
    Priority INTEGER NOT NULL DEFAULT 0,
    PlaybackMode TEXT NOT NULL DEFAULT 'Auto'
        CHECK (PlaybackMode IN ('Auto', 'Manual', 'QrOnly')),
    InterruptPolicy TEXT NOT NULL DEFAULT 'NotificationFirst'
        CHECK (InterruptPolicy IN ('NotificationFirst', 'NarrationFirst', 'Queue')),
    IsDownloadable INTEGER NOT NULL DEFAULT 1 CHECK (IsDownloadable IN (0, 1)),
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE,
    CONSTRAINT CK_Audio_SourcePayload CHECK (
        (SourceType = 'TTS' AND Script IS NOT NULL) OR
        (SourceType = 'Recorded' AND FilePath IS NOT NULL) OR
        (SourceType = 'Hybrid' AND Script IS NOT NULL AND FilePath IS NOT NULL)
    )
);

-- Function:
-- Optional extra images for a location.
-- Useful in POC web CMS and map/place detail page.
CREATE TABLE LocationImages (
    ImageId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER NOT NULL,
    ImageUrl TEXT NOT NULL,
    Description LONGTEXT,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE
);

-- Function:
-- Logs actual playback behavior from the app.
-- Needed in POC to verify geofence works, detect spam prevention, and check listening duration.
CREATE TABLE PlaybackEvents (
    PlaybackEventId INTEGER PRIMARY KEY AUTOINCREMENT,
    DeviceId TEXT,
    LocationId INTEGER,
    AudioId INTEGER,
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
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId),
    FOREIGN KEY (AudioId) REFERENCES AudioContents(AudioId)
);

-- Function:
-- Raw GPS tracking history from the mobile app.
-- Needed in POC for foreground/background tracking validation and future heatmap analysis.
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
    CapturedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IX_Locations_CategoryId ON Locations(CategoryId);
CREATE INDEX IX_Locations_OwnerId ON Locations(OwnerId);
CREATE INDEX IX_Locations_Status ON Locations(Status);
CREATE INDEX IX_Locations_LatLng ON Locations(Latitude, Longitude);
CREATE INDEX IX_AudioContents_LocationId ON AudioContents(LocationId);
CREATE INDEX IX_PlaybackEvents_LocationId_EventAt ON PlaybackEvents(LocationId, EventAt);
CREATE INDEX IX_LocationTrackingEvents_DeviceId_CapturedAt ON LocationTrackingEvents(DeviceId, CapturedAt);

-- =========================================================
-- MOBILE DATABASE PART
-- =========================================================

-- Function:
-- Local device identity stored in mobile SQLite.
-- Helps isolate cooldown, sync, analytics events, and future multi-device support.
CREATE TABLE AppDevices (
    DeviceId TEXT PRIMARY KEY,
    Platform TEXT NOT NULL CHECK (Platform IN ('Android', 'iOS')),
    DeviceModel TEXT,
    AppVersion TEXT,
    PreferredLanguageCode TEXT,
    LastKnownCountryCode TEXT,
    LastSeenAt TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Function:
-- Local anti-spam control.
-- Prevents a location from replaying too often when user stays in the same radius.
-- This is mobile-local and very important for POC.
CREATE TABLE PlaybackCooldown (
    DeviceId TEXT NOT NULL,
    LocationId INTEGER NOT NULL,
    LastPlayedAt TEXT NOT NULL,
    CooldownUntil TEXT,
    PlayCount INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (DeviceId, LocationId),
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId)
);

-- Function:
-- Stores device settings such as preferred language, GPS sensitivity, offline mode, voice choice.
-- This belongs in local SQLite because it should still work without network.
CREATE TABLE LocalSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Function:
-- Track what datasets the app already synced.
-- Needed when device can work offline and later reconnect to API.
-- You may postpone implementation to late POC or early MVP, but the table belongs to mobile storage.
CREATE TABLE DeviceSyncStates (
    DeviceId TEXT NOT NULL,
    SyncScope TEXT NOT NULL,
    LastFullSyncAt TEXT,
    LastDeltaToken TEXT,
    PRIMARY KEY (DeviceId, SyncScope),
    FOREIGN KEY (DeviceId) REFERENCES AppDevices(DeviceId) ON DELETE CASCADE
);

CREATE INDEX IX_PlaybackCooldown_LocationId ON PlaybackCooldown(LocationId);
CREATE INDEX IX_DeviceSyncStates_DeviceId ON DeviceSyncStates(DeviceId);

-- =========================================================
-- MVP PART WILL SCALE UP AFTER POC
-- =========================================================

-- Function:
-- Language catalog for dropdown, app setting, and default-country language decision.
-- In POC you can use LanguageCode directly in AudioContents, then normalize with this table in MVP.
CREATE TABLE Languages (
    LanguageId INTEGER PRIMARY KEY AUTOINCREMENT,
    LangCode TEXT NOT NULL UNIQUE,
    LangName TEXT NOT NULL,
    NativeName TEXT,
    PreferNativeVoice INTEGER NOT NULL DEFAULT 1 CHECK (PreferNativeVoice IN (0, 1)),
    IsDefault INTEGER NOT NULL DEFAULT 0 CHECK (IsDefault IN (0, 1)),
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Function:
-- Voice catalog for advanced TTS providers and downloadable voice choices.
-- More useful at MVP when you support Azure/System voice selection and offline packs.
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

-- Function:
-- Tour/grouping table for curated routes.
-- MVP feature for tour management in CMS and user route experience.
CREATE TABLE Tours (
    TourId INTEGER PRIMARY KEY AUTOINCREMENT,
    OwnerId INTEGER,
    Name TEXT NOT NULL,
    Description LONGTEXT,
    EstimatedDurationMinutes INTEGER,
    CoverImageUrl TEXT,
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (0, 1)),
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);

-- Function:
-- Join table between Tours and Locations.
-- Controls the order of POIs in a tour.
CREATE TABLE TourLocations (
    TourId INTEGER NOT NULL,
    LocationId INTEGER NOT NULL,
    SequenceOrder INTEGER NOT NULL,
    IsRequiredStop INTEGER NOT NULL DEFAULT 0 CHECK (IsRequiredStop IN (0, 1)),
    PRIMARY KEY (TourId, LocationId),
    FOREIGN KEY (TourId) REFERENCES Tours(TourId) ON DELETE CASCADE,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE
);

-- Function:
-- QR campaign table.
-- Allows user at bus stop or poster to scan and listen directly without GPS.
-- MVP because your POC core can already work from GPS auto-play alone.
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
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId),
    FOREIGN KEY (TourId) REFERENCES Tours(TourId),
    FOREIGN KEY (AudioId) REFERENCES AudioContents(AudioId)
);

-- Function:
-- Governance workflow for POI owners who submit changes and admin approves.
-- MVP because POC can start with direct admin editing.
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
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);

CREATE INDEX IX_TourLocations_LocationId ON TourLocations(LocationId);
CREATE INDEX IX_QRProfiles_CodeValue ON QRProfiles(CodeValue);
CREATE INDEX IX_ChangeRequests_TargetTable_TargetId ON ChangeRequests(TargetTable, TargetId);

-- =========================================================
-- OPTIONAL SEED DATA
-- =========================================================

INSERT INTO Languages (LangCode, LangName, NativeName, PreferNativeVoice, IsDefault, Status)
VALUES
    ('vi-VN', 'Vietnamese', 'Tiếng Việt', 1, 1, 1),
    ('en-US', 'English', 'English', 1, 0, 1);
