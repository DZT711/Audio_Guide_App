-- 1. Bảng danh mục (Ví dụ: Di tích lịch sử, Bảo tàng, Công viên...)
CREATE TABLE Categories (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description LONGTEXT,
    NumOfLocation INTEGER DEFAULT 0,
    Status Integer NOT NULL DEFAULT 1 CHECK( Status IN(0,1))
);

-- 2. Bảng địa điểm du lịch
CREATE TABLE Locations (
    LocationId INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER,
    Name TEXT NOT NULL,
    Description LONGTEXT,
    Latitude REAL NOT NULL,   -- Vĩ độ (Dùng REAL để tính khoảng cách)
    Longitude REAL NOT NULL,  -- Kinh độ
    Address TEXT,
    ImageUrl TEXT,            -- Ảnh minh họa địa điểm
    OwnerName TEXT,
    WebURL TEXT,
    Email TEXT,
    PhoneContact TEXT,
    NumOfAudio INTEGER DEFAULT 0,
    NumOfImg INTEGER DEFAULT 0,
    NumOfPeopleVisited INTEGER DEFAULT 0,
    EstablishedYear INTEGER DEFAULT 2026,
    Status Integer NOT NULL DEFAULT 1 CHECK( Status IN(0,1)),
    FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId)
);

-- 3. Bảng chứa nội dung âm thanh
CREATE TABLE AudioContents (
    AudioId INTEGER PRIMARY KEY AUTOINCREMENT,
    LocationId INTEGER NOT NULL,
    Title TEXT NOT NULL,      -- Tiêu đề bản thuyết minh
    FilePath TEXT NOT NULL,   -- Đường dẫn file trên server (vd: /uploads/audio/sgu_vi.mp3)
    Language TEXT CHECK(Language IN('vn', 'en', 'jp')) DEFAULT 'vn', -- vn, en, jp...
    Duration INTEGER,         -- Thời lượng (giây)
    Description LONGTEXT,
    NumOfPeoplePlayed INTEGER DEFAULT 0,
    VoiceGender TEXT CHECK(VoiceGender IN('Male', 'Female')) DEFAULT 'Female',
    Status Integer NOT NULL DEFAULT 1 CHECK( Status IN(0,1)),
    FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE
);
-- CREATE TABLE AdminAccount(
--     AdminId INTEGER PRIMARY KEY AUTOINCREMENT,
--     Username TEXT NOT NULL UNIQUE,
--     Password TEXT NOT NULL
--     Status Integer NOT NULL DEFAULT 1 CHECK( Status IN(0,1))
-- );
-- CREATE TABLE Account(
--     AccountId INTEGER PRIMARY KEY AUTOINCREMENT,
--     Username TEXT NOT NULL UNIQUE,
--     Password TEXT NOT NULL,
--     Email TEXT NOT NULL,
--     Phone TEXT NOT NULL UNIQUE,
--     Status Integer NOT NULL DEFAULT 1 CHECK( Status IN(0,1))

-- );
-- CREATE TABLE LocationImage(
--     ImageId INTEGER PRIMARY KEY AUTOINCREMENT,
--     LocationId INTEGER,
--     ImageUrl TEXT NOT NULL,
--     Description TEXT,
--     FOREIGN KEY (LocationId) REFERENCES Locations(LocationId) ON DELETE CASCADE

-- );