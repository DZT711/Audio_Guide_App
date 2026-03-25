--- GIỮ NGUYÊN CÁC BẢNG CŨ CỦA BẠN ---

CREATE TABLE Categories (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description LONGTEXT,
    Status INTEGER NOT NULL DEFAULT 1 CHECK(Status IN(0,1))
);

CREATE TABLE DashboardUsers (
    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL ,
    Password TEXT NOT NULL,
    Role TEXT NOT NULL CHECK(Role IN('Admin', 'Owner', 'Editor','Data Analyst','Developer')) DEFAULT 'Owner',
    Email TEXT,
    Phone TEXT NOT NULL UNIQUE,
    Status INTEGER NOT NULL DEFAULT 1 CHECK(Status IN(0,1))
);

CREATE TABLE Locations (
    LocationId INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER,
    OwnerId INTEGER,
    Name TEXT NOT NULL,
    Description LONGTEXT,
    Latitude REAL NOT NULL,   
    Longitude REAL NOT NULL,  
    Radius REAL DEFAULT 20.0,
    Address TEXT,
    ImageUrl TEXT,            
    WebURL TEXT,
    Email TEXT,
    PhoneContact TEXT,
    EstablishedYear INTEGER DEFAULT 2026,
    Status INTEGER NOT NULL DEFAULT 1 CHECK(Status IN(0,1)),
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);

-- Bảng này giữ nguyên nhưng chúng ta sẽ liên kết nó với bảng Language mới bên dưới
CREATE TABLE AudioContents (
    AudioId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER NOT NULL,
    LanguageId INTEGER,       -- CỘT MỚI: Liên kết tới bảng Languages
    Title TEXT NOT NULL,      
    FilePath TEXT,            
    -- Language TEXT CHECK(Language IN('Vietnamese', 'English', 'Japanese','Korean','Spanish','Russian','French','German')) DEFAULT 'Vietnamese', -- Giữ lại để không lỗi code cũ --co the mo rong lam MVP
    Duration INTEGER,         
    Script LONGTEXT,          
    VoiceId INTEGER,          -- CỘT MỚI: Liên kết tới bảng Voices (Giọng đọc)
    VoiceGender TEXT CHECK(VoiceGender IN('Male', 'Female')) DEFAULT 'Female',
    Status INTEGER NOT NULL DEFAULT 1 CHECK(Status IN(0,1)),
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE,
    FOREIGN KEY (LanguageId) REFERENCES Languages(LanguageId),
    FOREIGN KEY (VoiceId) REFERENCES Voices(VoiceId)
);

-- 6. Bảng danh mục Ngôn ngữ (Dùng cho Dropdown trên Web và Settings trên App)
CREATE TABLE Languages (
    LanguageId INTEGER PRIMARY KEY AUTOINCREMENT,
    LangCode TEXT NOT NULL UNIQUE, -- vd: 'vi-VN', 'en-US', 'ja-JP'
    LangName TEXT NOT NULL,        -- vd: 'Tiếng Việt', 'English'
    FlagIcon TEXT,                 -- Url icon lá cờ
    IsDefault INTEGER DEFAULT 0,   -- 1 là mặc định
    Status INTEGER NOT NULL DEFAULT 1 CHECK(Status IN(0,1))
);

-- 7. Bảng danh mục Giọng đọc (Dành cho cấu hình TTS nâng cao như Azure/Google)
CREATE TABLE Voices (
    VoiceId INTEGER PRIMARY KEY AUTOINCREMENT,
    LanguageId INTEGER,
    VoiceName TEXT NOT NULL,       -- vd: 'HieuMinh (Neural)', 'HoaiMy (Neural)'
    Gender TEXT CHECK(Gender IN('Male', 'Female')),
    VoiceAccent TEXT ,--chất giọng đặc trưng của một địa phương, vd: 'Northern', 'Southern', 'Central'
    Engine TEXT DEFAULT 'System',  -- 'System' (máy), 'Azure', 'Google'
    Status INTEGER NOT NULL DEFAULT 1 CHECK(Status IN(0,1)),
    FOREIGN KEY (LanguageId) REFERENCES Languages(LanguageId)
);
-- 9. Bảng lưu lịch sử thực tế từ App (Để làm Heatmap/Top POI)
CREATE TABLE ChangeRequests (
    RequestId INTEGER PRIMARY KEY AUTOINCREMENT,
    TargetId INTEGER,                  -- ID của dòng muốn sửa (Nếu thêm mới thì để NULL)
    OwnerId INTEGER NOT NULL,          -- Ai gửi yêu cầu
    TargetTable TEXT NOT NULL,         -- Bảng muốn sửa (vd: 'Locations', 'AudioContents')
    RequestType TEXT CHECK(RequestType IN('CREATE', 'UPDATE', 'DELETE')),
    NewDataJson LONGTEXT NOT NULL,     -- Toàn bộ dữ liệu mới dưới dạng chuỗi JSON
    Reason TEXT,                       -- Lý do sửa đổi (Owner ghi chú cho Admin)
    AdminNote TEXT,                    -- Phản hồi từ Admin
    Status TEXT DEFAULT 'Pending' CHECK(Status IN('Pending', 'Approved', 'Rejected')),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (OwnerId) REFERENCES DashboardUsers(UserId)
);
-- 10. Bảng thông tin Tour
CREATE TABLE Tours (
    TourId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT,
    EstimatedTime TEXT, -- vd: "2 hours"
    Status INTEGER DEFAULT 1 CHECK(Status IN(0,1))
);

-- 11. Bảng trung gian nối Tour và POI (Sắp xếp thứ tự)
CREATE TABLE TourLocations (
    TourId INTEGER,
    LocationId INTEGER,
    SequenceOrder INTEGER, -- Thứ tự POI trong Tour
    PRIMARY KEY (TourId, LocationId),
    FOREIGN KEY (TourId) REFERENCES Tours(TourId),
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId)
);
-- -- 12. Bảng lưu trạng thái Cooldown (Lưu ở SQLite của điện thoại)
-- CREATE TABLE PlaybackCooldown (
--     LocationId INTEGER PRIMARY KEY,
--     LastPlayedAt DATETIME,
--     PlayCount INTEGER DEFAULT 1,
--     FOREIGN KEY (LocationId) REFERENCES Locations(LocationId)
-- );
--MVP
-- 8. Bảng Lịch sử nghe (Dành cho Data Analytics trong đặc tả của bạn)
-- CREATE TABLE PlaybackHistory (
--     HistoryId INTEGER PRIMARY KEY AUTOINCREMENT,
--     LocationId INTEGER,
--     AudioId INTEGER,
--     PlayedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
--     DevicePlatform TEXT,           -- 'Android' hoặc 'iOS'
--     DurationListened INTEGER,      -- Thời gian thực tế khách đã nghe (giây)
--     FOREIGN KEY (LocationId) REFERENCES Locations(LocationId),
--     FOREIGN KEY (AudioId) REFERENCES AudioContents(AudioId)
-- );
-- CREATE TABLE ClientAccount(
    -- AccountId INTEGER PRIMARY KEY AUTOINCREMENT,
    -- Username TEXT NOT NULL UNIQUE,
    -- Password TEXT NOT NULL,
    -- Email TEXT NOT NULL,
    -- Phone TEXT NOT NULL UNIQUE,
    -- Status Integer NOT NULL DEFAULT 1 CHECK( Status IN(0,1))
-- );       
-- CREATE TABLE LocationImages (
--     ImageId INTEGER PRIMARY KEY AUTOINCREMENT,
--     LocationId INTEGER NOT NULL,
--     ImageUrl TEXT NOT NULL,
--     Description TEXT,
--     FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE
-- );
-- 13. Bảng lưu vết GPS ẩn danh
-- CREATE TABLE UserGPSLogs (
--     LogId INTEGER PRIMARY KEY AUTOINCREMENT,
--     DeviceId TEXT,           -- ID định danh thiết bị (ẩn danh)
--     Latitude REAL,
--     Longitude REAL,
--     Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
--     BatteryLevel INTEGER,    -- Để theo dõi "Less consume battery" trong đặc tả
--     Speed REAL               -- Tốc độ (để biết họ đang đi bộ hay đi xe)
-- );
-- -- 14. Quản lý phiên bản dữ liệu
-- CREATE TABLE SyncMetadata (
--     TableName TEXT PRIMARY KEY,
--     LastSyncTimestamp DATETIME
-- );
