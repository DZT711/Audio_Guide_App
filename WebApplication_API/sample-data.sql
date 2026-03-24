-- Smart Tourism sample seed data for SQLite
-- Target database file: App.db
-- Run from the WebApplication_API folder, for example:
-- sqlite3 App.db ".read sample-data.sql"

PRAGMA foreign_keys = OFF;

BEGIN TRANSACTION;

DELETE FROM AudioContents;
DELETE FROM Locations;
DELETE FROM Categories;
DELETE FROM sqlite_sequence
WHERE name IN ('AudioContents', 'Locations', 'Categories');

INSERT INTO Categories (Id, Name, Description, Status) VALUES
    (1, 'Historical Sites', 'Important heritage destinations and landmarks.', 1),
    (2, 'Museums', 'Cultural venues with curated collections and exhibitions.', 1),
    (3, 'Nature & Parks', 'Outdoor attractions, lakes, gardens, and scenic spaces.', 1),
    (4, 'Food & Craft Villages', 'Local culinary areas and traditional craft communities.', 1),
    (5, 'Archived Samples', 'Example inactive category for dashboard testing.', 0);

INSERT INTO Locations (
    Id,
    CategoryId,
    Name,
    Description,
    EstablishedYear,
    Latitude,
    Longitude,
    Address,
    ImgURL,
    OwnerName,
    WebURL,
    Phone,
    Email,
    Status
) VALUES
    (
        1,
        1,
        'Temple of Literature',
        'A well-known historical complex and one of Hanoi''s most visited cultural landmarks.',
        1070,
        21.028511,
        105.835556,
        '58 Quoc Tu Giam, Dong Da, Hanoi, Vietnam',
        'https://images.unsplash.com/photo-1528127269322-539801943592',
        'Hanoi Department of Culture',
        'https://vanmieu.gov.vn',
        '+84 24 3733 9731',
        'info@vanmieu.gov.vn',
        1
    ),
    (
        2,
        2,
        'Vietnam National Museum of Fine Arts',
        'A museum featuring Vietnamese fine arts from ancient to modern periods.',
        1966,
        21.031704,
        105.835159,
        '66 Nguyen Thai Hoc, Ba Dinh, Hanoi, Vietnam',
        'https://images.unsplash.com/photo-1518998053901-5348d3961a04',
        'National Museum of Fine Arts',
        'https://vnfam.vn',
        '+84 24 3823 3084',
        'contact@vnfam.vn',
        1
    ),
    (
        3,
        3,
        'Hoan Kiem Lake',
        'A central lake area popular for walking, sightseeing, and cultural activities.',
        1428,
        21.028775,
        105.852353,
        'Hoan Kiem District, Hanoi, Vietnam',
        'https://images.unsplash.com/photo-1506744038136-46273834b3fb',
        'Hanoi Urban Management',
        'https://hanoi.gov.vn',
        '+84 24 3825 3536',
        'support@hanoi.gov.vn',
        1
    ),
    (
        4,
        4,
        'Bat Trang Pottery Village',
        'A traditional craft village known for ceramics, workshops, and local shopping.',
        1352,
        20.995148,
        105.911807,
        'Bat Trang, Gia Lam, Hanoi, Vietnam',
        'https://images.unsplash.com/photo-1460661419201-fd4cecdf8a8b',
        'Bat Trang Craft Association',
        'https://battrang.com.vn',
        '+84 24 3675 0338',
        'hello@battrang.com.vn',
        1
    ),
    (
        5,
        5,
        'Old Riverside Market',
        'Inactive sample location kept for soft-delete and status highlighting tests.',
        1998,
        21.035420,
        105.861210,
        'Riverside Ward, Hanoi, Vietnam',
        'https://images.unsplash.com/photo-1488459716781-31db52582fe9',
        'City Market Board',
        'https://example.com/old-riverside-market',
        '+84 24 3000 0000',
        'archived@example.com',
        0
    );

INSERT INTO AudioContents (
    Id,
    LocationId,
    Title,
    FilePath,
    Language,
    Duration,
    Description,
    Script,
    VoiceGender,
    Status
) VALUES
    (
        1,
        1,
        'Temple Overview',
        '/audio/temple-of-literature-overview.mp3',
        'en-US',
        175,
        'Intro narration for first-time visitors.',
        'Welcome to the Temple of Literature, one of Vietnam''s most treasured historical landmarks.',
        'Female',
        1
    ),
    (
        2,
        1,
        'Lich Su Van Mieu',
        '/audio/van-mieu-history-vi.mp3',
        'vi-VN',
        210,
        'Vietnamese storytelling version for local visitors.',
        'Chao mung ban den Van Mieu Quoc Tu Giam, noi luu giu tinh hoa hoc thuat va lich su Viet Nam.',
        'Male',
        1
    ),
    (
        3,
        2,
        'Museum Highlights',
        '/audio/fine-arts-highlights.mp3',
        'en-GB',
        240,
        'Highlights tour of signature museum galleries.',
        'This gallery introduces major works that represent the evolution of Vietnamese fine arts.',
        'Female',
        1
    ),
    (
        4,
        3,
        'Lake Walking Guide',
        '/audio/hoan-kiem-walk.mp3',
        'en-US',
        145,
        'Short walking guide around Hoan Kiem Lake.',
        'Take a slow walk around the lake and enjoy the blend of history, greenery, and daily city life.',
        'Male',
        1
    ),
    (
        5,
        4,
        'Bat Trang Experience',
        '/audio/bat-trang-experience.mp3',
        'vi-VN',
        195,
        'Guide to pottery workshops and shopping areas.',
        'Hay thu trai nghiem tu tay nan gom va kham pha nhung gian hang thu cong truyen thong tai Bat Trang.',
        'Female',
        1
    ),
    (
        6,
        5,
        'Archived Market Audio',
        '/audio/archived-market.mp3',
        'en-US',
        120,
        'Inactive audio sample used for archive-state testing.',
        'This is an inactive sample audio entry for dashboard and management list verification.',
        'Male',
        0
    );

COMMIT;

PRAGMA foreign_keys = ON;
