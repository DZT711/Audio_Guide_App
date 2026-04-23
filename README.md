# 📱 Smart Tourism MAUI — Audio Guide App

**Audio Tour POI App for Vinh Khanh Food Street, Ho Chi Minh City**

> Multi-language offline-first audio guide system for tourists with geofencing-triggered playback and admin content management.

---

## 📋 Quick Reference

| Item | Details |
|------|---------|
| **Project Name** | Smart Tourism MAUI |
| **Current Branch** | `Mobile_AppPerformance` |
| **Default Branch** | `main` |
| **Repository** | DZT711/Smart-Tourism-MAUI |
| **Status** | 🟡 Active Development |
| **Last Updated** | April 23, 2026 |

---

## 📁 Project Structure

```
Smart-Tourism-MAUI/
├── 📱 MauiApp_Mobile/                    # .NET MAUI Mobile App (Android + iOS)
│   ├── App.xaml / App.xaml.cs            # App lifecycle, startup, cleanup
│   ├── MainPage.xaml / MainPage.xaml.cs  # Main UI (tabs, POI list, mini player)
│   ├── AppShell.xaml                     # Shell navigation, tab routes
│   ├── MauiProgram.cs                    # DI container, service registration
│   ├── Components/                       # XAML components (dialog, buttons)
│   ├── Models/                           # Data models
│   ├── Services/                         # Business logic services
│   │   ├── PlaybackCoordinatorService.cs    # Audio queue management
│   │   ├── AudioPlaybackService.cs          # Audio playback (TTS/MP3)
│   │   ├── AppSettingsService.cs            # User preferences, settings
│   │   ├── LocationTrackingService.cs       # GPS tracking (foreground/background)
│   │   ├── Geofencing/                      # Geofence detection engine
│   │   │   ├── GeofenceOrchestratorService.cs
│   │   │   ├── GeofenceOrchestratorService.Playback.cs
│   │   │   └── GeofenceOrchestratorService.Processing.cs
│   │   ├── AudioDownloadService.cs          # Audio file management
│   │   ├── HistoryService.cs                # Playback history logging
│   │   ├── MobileDatabaseService.cs         # SQLite ORM layer
│   │   └── (Platform-specific services)
│   ├── Views/                            # XAML Pages
│   │   ├── MapPage.xaml / MapPage.xaml.cs  # Interactive map (Leaflet)
│   │   ├── SettingsPage.xaml                # User preferences UI
│   │   ├── OfflinePage.xaml                 # Offline content management
│   │   └── (Other page views)
│   ├── Platforms/                        # Platform-specific code
│   │   ├── Android/                      # Android services, permissions
│   │   ├── iOS/                          # iOS services, geofencing
│   │   └── Windows/
│   ├── Resources/
│   │   ├── Raw/leaflet_map.html          # Web map (Leaflet + OpenStreetMap)
│   │   ├── Styles/
│   │   └── (Images, data files)
│   ├── ViewModels/                       # MVVM view models
│   └── MauiApp_Mobile.csproj
│
├── 🌐 WebApplication_API/                # ASP.NET Core Backend API
│   ├── Program.cs                        # Startup, middleware, DI
│   ├── appsettings.json                  # Configuration (DB, URLs, secrets)
│   ├── Controller/                       # REST API endpoints
│   │   ├── POIController.cs              # CRUD: /api/pois
│   │   ├── AudioController.cs            # Audio CRUD, upload
│   │   ├── UserController.cs             # Auth, user management
│   │   ├── SyncController.cs             # Mobile sync endpoints
│   │   └── (Other controllers)
│   ├── Data/                             # EF Core DbContext
│   │   └── AppDbContext.cs
│   ├── Model/                            # Domain entities
│   ├── DTO/                              # Data transfer objects
│   ├── Services/                         # Business logic
│   ├── Properties/                       # App config, launch settings
│   └── WebApplication_API.csproj
│
├── 🎨 BlazorApp_AdminWeb/                # Admin Dashboard (Blazor)
│   ├── App.razor                         # Main Blazor app
│   ├── Components/                       # Blazor components
│   │   ├── Pages/                        # Page components
│   │   │   ├── POIManagement.razor       # Create/Edit/Delete POI
│   │   │   ├── UserManagement.razor      # User & role management
│   │   │   ├── Analytics.razor           # Dashboard analytics
│   │   │   ├── OwnerPortal.razor         # Owner self-service
│   │   │   └── (Other pages)
│   │   ├── Shared/                       # Shared components (navbar, sidebar)
│   │   └── (Form components, dialogs)
│   ├── Services/                         # API client, data services
│   ├── Layout/                           # Blazor layouts
│   ├── wwwroot/                          # Static files (CSS, JS, images)
│   ├── appsettings.json                  # Blazor config
│   ├── Program.cs                        # Blazor startup
│   └── BlazorApp_AdminWeb.csproj
│
├── 📚 Project_SharedClassLibrary/        # Shared Code (.NET Standard)
│   ├── Contracts/                        # DTOs, interfaces shared between projects
│   │   ├── PublicAudioTrackDto.cs
│   │   ├── PoiDataTransferObject.cs
│   │   └── (Other DTOs)
│   ├── Constants/                        # Shared constants (API routes, etc)
│   ├── Geofencing/                       # Shared geofence models
│   │   ├── PoiGeofenceDefinition.cs
│   │   └── (Geofence DTOs)
│   ├── Security/                         # RBAC, auth helpers
│   ├── Validation/                       # Validation logic, attributes
│   ├── Storage/                          # Storage abstractions
│   └── Shared_ClassLibrary.csproj
│
├── 🔧 scripts/                           # PowerShell utility scripts
│   ├── run-android-clean.ps1             # Clean Android build
│   ├── start-smarttour-tunnel.ps1        # Start development tunnel
│   └── update-android-network-security-config.ps1
│
├── 📖 docs/                              # Documentation
│   ├── specification.md                  # Full feature specification
│   ├── task.md                           # Development task checklist
│   ├── DatabaseStructure/                # DB schema docs
│   │   ├── database.sql                  # SQL Server schema
│   │   ├── mobile-sqlite-migration.sql   # SQLite schema
│   │   ├── sample-data.sql               # Sample POI data
│   │   └── smarttour-mobile.db3          # SQLite template
│   ├── Diagram/                          # Architecture diagrams
│   └── PRD-DoAnC#-.docx                  # Product requirement doc
│
├── 📄 README.md                          # THIS FILE - Project overview
├── 📄 Smart-Tourism-MAUI.sln             # Visual Studio solution file
├── 📄 Directory.Build.props               # MSBuild common properties
└── 📄 .gitignore                         # Git ignore rules

```

---

## 🏗️ Architecture Overview

### **Three-Tier Architecture**

```
┌─────────────────────────────────────────────────────────┐
│  Presentation Layer                                     │
├──────────────────────┬──────────────────┐────────────────┤
│   Mobile             │   Admin Web      │  Owner Portal  │
│  (MAUI Android/iOS)  │  (Blazor)        │  (Blazor)      │
└──────────────────────┴──────────────────┴────────────────┘
           ↓                    ↓                 ↓
┌─────────────────────────────────────────────────────────┐
│  Business Logic Layer                                   │
├─ Services (Playback, Geofencing, Audio, Settings, etc)─┤
│  - Offline-first logic                                 │
│  - Queue management                                    │
│  - Geofence detection                                  │
│  - Audio decision logic (4-Tier Hybrid)                │
└─────────────────────────────────────────────────────────┘
           ↓                    ↓
┌─────────────────────────────────────────────────────────┐
│  Data Access Layer                                      │
├─ SQLite (Mobile)  │  SQL Server (Backend)  │  Cache     ─┤
│ - Local POI data  │  - Users, POI, Audio   │  - Audio   │
│ - Offline sync    │  - Analytics, history  │  - Images  │
└─────────────────────────────────────────────────────────┘
```

### **Technology Stack**

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Mobile** | .NET MAUI (C#) | Cross-platform mobile (Android/iOS) |
| **Admin Web** | Blazor Server | Real-time web dashboard |
| **Backend** | ASP.NET Core 8 | REST API, business logic |
| **Database** | SQL Server (prod), SQLite (mobile) | Data persistence |
| **Audio** | Android TTS, iOS AVSpeechSynthesizer | Text-to-speech |
| **Map** | Leaflet + OpenStreetMap | Interactive map, offline support (PMTiles) |
| **GPS/Geofence** | Native iOS CLLocationManager, Android FusedLocationProviderClient | Location tracking |
| **Shared Lib** | .NET Standard 2.1 | DTOs, contracts, validation |

---

## 🚀 Getting Started

### **Prerequisites**

- **Visual Studio 2022** (or VS Code + .NET CLI)
- **.NET 8 SDK** (or later)
- **Android SDK** (API 26+) for mobile development
- **Git**

### **Clone & Setup**

```bash
# Clone repository
git clone https://github.com/DZT711/Smart-Tourism-MAUI.git
cd Smart-Tourism-MAUI

# Restore NuGet packages
dotnet restore Smart-Tourism-MAUI.sln

# Verify solution loads
dotnet build Smart-Tourism-MAUI.sln
```

---

## 📱 Mobile App (MauiApp_Mobile)

### **Build & Run**

**Android:**
```bash
cd MauiApp_Mobile

# Debug build for Android
dotnet build -f net8.0-android -c Debug

# Run on Android emulator/device
dotnet run -f net8.0-android

# Or via script
.\scripts\run-android-clean.ps1
```

**iOS:**
```bash
# Debug build for iOS
dotnet build -f net8.0-ios -c Debug

# Run on iOS simulator
dotnet run -f net8.0-ios
```

### **Key Features**

✅ **GPS Tracking**
- Foreground: Always-on location updates (5s throttle)
- Background: Foreground service (Android 8+)

✅ **Geofencing & Auto-Playback**
- Haversine distance detection
- Configurable radius (default 30m)
- Cooldown mechanism (5 min)
- Priority-based POI selection

✅ **Audio Playback**
- 4-Tier Hybrid (Cache → Translate+TTS → Cloud TTS → Device TTS)
- Queue management (play, pause, next, previous)
- Multiple audio files per POI

✅ **Offline Support**
- SQLite database for POI data
- Pre-cached audio files
- Offline map (PMTiles)
- Delta sync when connected

✅ **Multi-Language**
- Vietnamese (VI), English (EN), Chinese (ZH), Japanese (JA), Korean (KO)
- TTS voice per language
- Settings page language selector

---

## 🌐 Backend API (WebApplication_API)

### **Build & Run**

```bash
cd WebApplication_API

# Build
dotnet build -c Debug

# Run
dotnet run

# API will be available at: http://localhost:5000 (or port in launchSettings.json)
```

### **API Endpoints**

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/pois` | GET | List all POIs |
| `/api/pois/{id}` | GET | POI detail |
| `/api/pois` | POST | Create POI |
| `/api/pois/{id}` | PUT | Update POI |
| `/api/pois/{id}` | DELETE | Delete POI |
| `/api/audio` | POST | Upload audio file |
| `/api/audio/{id}` | GET | Download audio |
| `/api/sync/delta` | POST | Mobile delta sync |
| `/api/users` | GET/POST | User management |
| `/api/auth/login` | POST | Authentication |

See [specification.md](docs/specification.md) for full API documentation.

---

## 🎨 Admin Dashboard (BlazorApp_AdminWeb)

### **Build & Run**

```bash
cd BlazorApp_AdminWeb

# Debug build
dotnet build -c Debug

# Run
dotnet run

# Access at: http://localhost:7000 (or configured port)
```

### **Features**

✅ **POI Management**
- Create/Edit/Delete POI
- Multiple images per POI
- Audio file management
- Language support per POI

✅ **User Management**
- Admin role assignment
- Shop owner verification
- Permission control (RBAC)

✅ **Analytics Dashboard**
- Top POIs by playback count
- Average listening time per POI
- User heatmap
- Date range filtering

✅ **Owner Portal**
- Self-service POI editing
- Audio upload
- Approval workflow

---

## 💾 Database

### **Database Setup**

**SQL Server (Production):**
```bash
# Execute database.sql to create schema
sqlcmd -S your_server -U sa -P your_password -i docs/DatabaseStructure/database.sql
```

**SQLite (Mobile):**
- Schema in `mobile-sqlite-migration.sql`
- Template DB: `smarttour-mobile.db3`
- Auto-created on first app launch

**Sample Data:**
```bash
# Populate sample POI data
sqlcmd -S your_server -U sa -P your_password -i docs/DatabaseStructure/sample-data.sql
```

---

## 📖 Documentation

| Document | Purpose |
|----------|---------|
| [specification.md](docs/specification.md) | Full feature specification & user stories |
| [task.md](docs/task.md) | Development task checklist & completed items |
| [DatabaseStructure/](docs/DatabaseStructure/) | DB schema, migrations, sample data |
| [Diagram/](docs/Diagram/) | Architecture & flow diagrams |

---

## 🔄 Development Workflow

### **Branch Strategy**

- `main` — Stable, production-ready code
- `Mobile_AppPerformance` — Current development branch
- `develop` — Integration branch for features

### **Commit Convention**

```
[FEATURE|FIX|DOCS|TEST]: Brief description

- Detailed change explanation
- Related task: #123
```

### **Build & Test**

```bash
# Full solution build (all projects)
dotnet build Smart-Tourism-MAUI.sln -c Debug

# Run tests (if test project exists)
dotnet test Smart-Tourism-MAUI.sln

# Clean build
dotnet clean Smart-Tourism-MAUI.sln
dotnet build Smart-Tourism-MAUI.sln -c Release
```

---

## 🛠️ Scripts

| Script | Purpose |
|--------|---------|
| `run-android-clean.ps1` | Clean Android build & run |
| `start-smarttour-tunnel.ps1` | Start local dev tunnel |
| `update-android-network-security-config.ps1` | Update Android security config |

**Usage:**
```powershell
cd scripts
.\run-android-clean.ps1
```

---

## 🧪 Testing

### **Mobile Testing Checklist**

- [ ] GPS tracking updates every 5 seconds
- [ ] Geofence triggers within 30m radius
- [ ] Audio plays on trigger (3 sec debounce)
- [ ] Queue management: add/remove/next/previous
- [ ] Offline mode: map loads, audio plays from cache
- [ ] Language switch applies to new POIs
- [ ] Settings persist after app restart

### **Admin Dashboard Testing**

- [ ] Create/Edit/Delete POI works
- [ ] Upload audio file works
- [ ] Analytics charts load correctly
- [ ] User roles applied correctly
- [ ] Owner portal shows only own POIs

---

## 📊 Project Status

### **Completed ✅**

- [x] GPS tracking (foreground + background)
- [x] Geofencing engine (Haversine formula)
- [x] 4-Tier audio playback (TTS + pre-recorded)
- [x] Queue management system
- [x] Multi-language support (5 languages)
- [x] Offline sync (delta sync)
- [x] SQLite mobile database
- [x] Admin POI CRUD
- [x] User authentication & RBAC
- [x] Admin dashboard
- [x] Owner portal
- [x] Analytics (playback logs, heatmap)

### **In Progress 🟡**

- Audio queue auto-insertion on trigger (not waiting for other POIs)
- Notification cleanup on app kill
- Performance optimization (Mobile_AppPerformance branch)

### **Planned 📋**

- [ ] Push notifications
- [ ] Route optimization (OSRM integration)
- [ ] Real-time analytics
- [ ] Advanced geofencing (zones)

---

## 🐛 Common Issues & Solutions

### **Android Build Fails**

**Error:** `Platform version Android 13 (API 33) not found`

**Solution:**
```bash
# Install required Android SDK
dotnet workload install android
```

### **Mobile Database Locked**

**Error:** `SQLite database is locked`

**Solution:**
- Close other processes accessing the DB
- Restart the app
- Clear app data

### **Audio Not Playing**

**Checklist:**
- [ ] Audio file exists in cache
- [ ] TTS is enabled in settings
- [ ] Volume not muted
- [ ] Check app logs for errors

### **GPS Not Updating**

**Checklist:**
- [ ] Location permission granted
- [ ] GPS enabled on device
- [ ] Foreground service running (Android)
- [ ] Check location_tracking_enabled setting

---

## 👥 Contributing

1. Create feature branch: `git checkout -b feature/your-feature`
2. Commit changes: `git commit -m "[FEATURE]: Description"`
3. Push to branch: `git push origin feature/your-feature`
4. Open Pull Request to `main`

---

## 📞 Support & Contact

**Questions or Issues?**
- Create GitHub Issue: [DZT711/Smart-Tourism-MAUI/issues](https://github.com/DZT711/Smart-Tourism-MAUI/issues)
- Contact: Nguyễn Sĩ Huy (Product Owner)

---

## 📜 License

Proprietary — Smart Tourism MAUI Project

---

## 🙏 Acknowledgments

- **Vinh Khanh Food Street** — Project location & stakeholder
- **Microsoft Edge-TTS** — Free text-to-speech service
- **OpenStreetMap** — Map data provider
- **Leaflet** — Interactive map library
- **PMTiles** — Offline tile storage

---

### E.Cách chạy dự án

1. **Backend API:**
   - Mở `WebApplication_API` trong Visual Studio.
   - Cấu hình chuỗi kết nối SQL Server trong `appsettings.json`.
   - Chạy migrations để tạo database.
   - Chạy ứng dụng (F5) → API sẽ chạy trên `https://localhost:5123`.

   ```cmd
    dotnet run --project WebApplication_API/WebApplication_API.csproj --urls "https://0.0.0.0:5123"
    ngrok http 5123 --host-header="localhost:5123"
   ```

2. **Admin Web:**
   - Mở `BlazorApp_AdminWeb` trong Visual Studio.
   - Cấu hình `appsettings.json` để trỏ đến API. Mặc định project đang dùng `https://localhost:5123/`.
   - Chạy ứng dụng → Đăng nhập bằng tài khoản admin đã seed sẵn.

   ```cmd
    dotnet run --project BlazorApp_AdminWeb/BlazorApp_AdminWeb.csproj 
   ```

   - Tài khoản thử nghiệm : username:admin/ password:admin

3. **Mobile App:**
   - Mở `MauiApp_Mobile` trong Visual Studio/VSCode.
   - Cấu hình `ApiEndpoints.cs` để trỏ đến API.
   - Chạy ứng dụng trên Android Emulator hoặc thiết bị thật.

    Cách 1: chạy trên windows :

    ```cmd
        dotnet run --project MauiApp_Mobile/MauiApp_Mobile.csproj -f net10.0-windows10.0.19041.0
    ```

    Cách 2: chạy trên Android cắm cáp usb vào máy chủ
    - Cho phép máy tính debug trên android(bật dev mode trong setting)

   ```cmd
        adb devices (đảm bảo thiết bị ở trạng thái mở "device")
        adb reverse tcp:5123 tcp:5123 (để chuyển tiếp cổng từ máy chủ đến thiết bị)
        dotnet run --project MauiApp_Mobile/MauiApp_Mobile.csproj -f net10.0-android
   ```

    - Thử gọi api: "[http://127.0.0.1:5123/location/public/catalog](http://127.0.0.1:5123/location/public/catalog)"
    Cách 3: chạy thông qua wifi
    - Bật Wifi Debug trên android : lấy thông tin ip và cổng kết nối
    - Chỉnh mạng máy sever thanh private
    - Đảm bảo sử dụng chung 1 mạng
    - Thêm ip sever vào file cấu hình mạng của android trong MauiApp_Mobile/Platforms/Android/Resources/xml/network_security_config.xml

    ```xml
            <domain includeSubdomains="false">yourSeverIP</domain>
    ```

    ```cmd
        adb connect ip:port (kết nối qua wifi, ví dụ adb connect 192.168.x.x:44444)
        adb devices (đảm bảo thiết bị ở trạng thái mở "device")
        ipconfig(lấy ipv4 của máy sever ipsever )
        dotnet run --project MauiApp_Mobile/MauiApp_Mobile.csproj -f net10.0-android
    ```

   -Thử gọi api: "[http://ipsever:5123/location/public/catalog](http://ipsever:5123/location/public/catalog)"
    Lệnh pull db từ điện thoại kêt nối
    
    ```cmd
        adb exec-out run-as com.companyname.mauiapp_mobile cat files/smarttour-mobile.db3 > docs\smarttour-mobile.db
    ```

*© 2026 — Nguyễn Sĩ Huy (3123411122) & Nguyễn Văn Cường (3123411045)*
*Dự Án Thuyết Minh Phố Ẩm Thực Vĩnh Khánh — Khoa Công nghệ Thông tin*
**Last Updated:** April 23, 2026  
**Current Branch:** `Mobile_AppPerformance`  
**Status:** 🟡 Active Development

