# 📁 Project Structure & Organization

Complete guide to understanding the Smart Tourism MAUI codebase organization.

---

## Table of Contents

1. [Root Level Files](#root-level-files)
2. [Mobile App Structure](#mobile-app-structure)
3. [Backend API Structure](#backend-api-structure)
4. [Admin Web Structure](#admin-web-structure)
5. [Shared Library](#shared-library)
6. [Documentation & Scripts](#documentation--scripts)
7. [Architecture Patterns](#architecture-patterns)

---

## Root Level Files

```
Smart-Tourism-MAUI/
├── Smart-Tourism-MAUI.sln         # Visual Studio solution file
│                                  # - Contains all 4 projects
│                                  # - Configure build order & dependencies
│
├── Directory.Build.props            # MSBuild common properties
│                                  # - Shared assembly version
│                                  # - Common compiler settings
│                                  # - LangVersion, Nullable config
│
├── README.md                        # Project overview (START HERE)
├── SETUP.md                         # Development environment setup guide
├── STRUCTURE.md                     # This file
│
├── .gitignore                       # Git ignore rules
├── .git/                            # Git repository (local)
├── .env                             # Local environment variables (ignored)
│
└── [Documentation & Folders Below]
```

---

## Mobile App Structure

**Project:** `src/MauiApp_Mobile/MauiApp_Mobile.csproj`  
**Framework:** .NET MAUI (Multi-platform App UI)  
**Target Platforms:** Android, iOS, Windows (optional)

### Directory Layout

```
src/MauiApp_Mobile/
│
├── ┌─ APP LIFECYCLE & CONFIGURATION ─┐
│   ├── App.xaml                    # App-level XAML (styles, themes)
│   ├── App.xaml.cs                # App.cs - startup, cleanup, OnStopping
│   ├── AppShell.xaml              # Shell navigation, route definitions
│   ├── MauiProgram.cs             # DI container configuration
│   │                              # - Service registration
│   │                              # - Theme setup
│   │                              # - MAUI features configuration
│   │
│   └── Properties/
│       └── launchSettings.json     # Debug launch profiles
│
├── ┌─ MAIN USER INTERFACE ─┐
│   ├── MainPage.xaml              # Main UI layout (tabs, mini player, POI list)
│   │                              # - 4 tabs: Map, POI List, Offline, Settings
│   │                              # - Mini player at bottom
│   ├── MainPage.xaml.cs           # Code-behind: event handlers, UI logic
│   │
│   └── AppShell.xaml              # App-level navigation shell
│
├── ┌─ PAGES (Views) ─┐
│   └── Views/
│       ├── MapPage.xaml           # Interactive map with Leaflet
│       ├── MapPage.xaml.cs        # Map interactivity, geofence visualization
│       │
│       ├── SettingsPage.xaml      # User preferences (GPS, audio, language)
│       ├── SettingsPage.xaml.cs   # Settings CRUD
│       │
│       ├── OfflinePage.xaml       # Offline content download & management
│       ├── OfflinePage.xaml.cs    # Download queue, cache management
│       │
│       └── [Other Pages]          # Additional modal/dialog pages
│
├── ┌─ SERVICES (Business Logic) ─┐
│   └── Services/
│       │
│       ├── PlaybackCoordinatorService.cs
│       │   └─ Audio queue management (play, pause, next, previous)
│       │   └─ Queue state (current item, index, count)
│       │   └─ Properties: IsPlaying, IsPaused, CurrentTrack, Queue
│       │
│       ├── AudioPlaybackService.cs
│       │   └─ Low-level audio playback (TTS, MP3)
│       │   └─ Cross-platform (Android TTS, iOS AVSpeechSynthesizer, MediaPlayer)
│       │   └─ Methods: PlayAsync, PauseAsync, StopAsync, SeekAsync
│       │   └─ Events: PlaybackStateChanged, PlaybackProgressChanged
│       │
│       ├── AppSettingsService.cs
│       │   └─ User preferences persistence (SQLite)
│       │   └─ Properties: AutoPlayEnabled, LanguageCode, VolumePercent, etc.
│       │   └─ Methods: InitializeAsync, SaveAsync, ApplySnapshot
│       │
│       ├── LocationTrackingService.cs
│       │   └─ GPS location tracking (foreground + background)
│       │   └─ Methods: StartForegroundTrackingAsync, StartBackgroundTrackingAsync
│       │   └─ Location updates every 5 seconds (throttled)
│       │
│       ├── Geofencing/
│       │   ├── GeofenceOrchestratorService.cs
│       │   │   └─ Orchestrates geofence detection & audio trigger
│       │   │   └─ Main entry: HandleTriggerAsync
│       │   │   └─ Manages cooldowns, history logging
│       │   │
│       │   ├── GeofenceOrchestratorService.Playback.cs
│       │   │   └─ Audio playback decision logic
│       │   │   └─ Priority-based POI selection
│       │   │   └─ Methods: EnterPlaybackSource, NearPlaybackSource
│       │   │
│       │   ├── GeofenceOrchestratorService.Processing.cs
│       │   │   └─ Geofence processing loop
│       │   │   └─ Haversine distance calculation
│       │   │   └─ Debounce & reconciliation logic
│       │   │
│       │   └── [Other geofence-related services]
│       │
│       ├── AudioDownloadService.cs
│       │   └─ Audio file management (download, cache, verify)
│       │   └─ 4-Tier fallback resolution
│       │   └─ Methods: ResolvePlayableTrackAsync, DownloadAsync
│       │
│       ├── HistoryService.cs
│       │   └─ Playback history logging
│       │   └─ Methods: AddToHistory, GetHistoryAsync
│       │   └─ Used for analytics
│       │
│       ├── MobileDatabaseService.cs
│       │   └─ SQLite ORM wrapper (EF Core)
│       │   └─ Methods: GetSettingAsync, SetSettingAsync, QueryPoisAsync
│       │
│       ├── LocalizationService.cs
│       │   └─ Multi-language support
│       │   └─ Language switching
│       │
│       ├── PlaceCatalogService.cs
│       │   └─ POI list cache & sync
│       │
│       ├── BackgroundSyncService.cs
│       │   └─ Sync offline changes with server
│       │
│       ├── AppNotificationService.cs
│       │   └─ Local notifications
│       │
│       └── ThemeService.cs
│           └─ App theme management (light/dark/custom)
│
├── ┌─ MODELS ─┐
│   └── Models/
│       ├── AdminManagementModels.cs
│       ├── CategoryVisuals.cs
│       └── [Other DTO/View Models]
│
├── ┌─ CONVERTERS ─┐
│   └── Converters/
│       └─ XAML value converters (BoolToVisibility, etc)
│
├── ┌─ PLATFORM-SPECIFIC CODE ─┐
│   └── Platforms/
│       ├── Android/
│       │   ├── Services/
│       │   │   └─ Geofencing/
│       │   │       └─ GeofencePlatformMonitor.android.cs
│       │   │   └─ AndroidAudioPlaybackNotificationManager.cs
│       │   │   └─ NotificationCleanupService.cs
│       │   │
│       │   ├── MainApplication.cs
│       │   └── AndroidManifest.xml
│       │
│       └── iOS/
│           ├── Services/
│           │   └─ Geofencing/
│           │       └─ GeofencePlatformMonitor.ios.cs
│           │
│           ├── Info.plist
│           └── [iOS specific files]
│
├── ┌─ RESOURCES ─┐
│   └── Resources/
│       ├── Raw/
│       │   └─ leaflet_map.html   # Embedded Leaflet map
│       │      └─ OpenStreetMap integration
│       │      └─ PMTiles offline support
│       │      └─ POI marker rendering
│       │      └─ window.applyMapBehavior() JS function
│       │      └─ window.focusPlaceById() JS function
│       │
│       ├── Styles/
│       │   └─ Colors.xaml
│       │   └─ Styles.xaml
│       │
│       ├── Fonts/
│       │   └─ [Custom fonts if any]
│       │
│       └── Images/
│           └─ [App icons, logo, etc]
│
├── ┌─ VIEWS (Alternative UI) ─┐
│   └── ViewModels/
│       └─ MVVM ViewModels (if not using MVVM)
│
├── bin/                         # Build output directory
│   ├── Debug/
│   │   ├── net8.0-android/     # Android build artifacts
│   │   ├── net8.0-ios/         # iOS build artifacts
│   │   └── net8.0-windows/
│   │
│   └── verify-admin/            # Verification output
│
├── obj/                         # Intermediate build objects
│   └── Debug/
│       └─ [Platform-specific obj]
│
├── MauiApp_Mobile.csproj       # Project file
│   └─ NuGet dependencies
│   └─ Platform references
│   └─ Build configuration
│
└── MauiApp_Mobile.csproj.lscache
    └─ Language service cache (VS internal)
```

### Key Services Interaction

```
┌──────────────────────────────────────────────────────────────────┐
│                      UI LAYER (Pages, Controls)                  │
│  (MainPage.xaml, MapPage, SettingsPage, etc.)                   │
└────────────────────────┬─────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
    ┌─────────────┐  ┌──────────────┐  ┌─────────────┐
    │ Playback    │  │ Location     │  │ AppSettings │
    │ Coordinator │  │ Tracking     │  │ Service     │
    │ Service     │  │ Service      │  │             │
    └─────────────┘  └──────────────┘  └─────────────┘
        │                │
        │                ▼
        │         ┌──────────────────┐
        │         │ Geofence         │
        │         │ Orchestrator     │
        │         │ Service          │
        │         └──────────────────┘
        │                │
        ▼                ▼
    ┌────────────────────────────────┐
    │ Audio Playback Service          │
    │ (Low-level playback)            │
    └────────────────────────────────┘
        │
        ▼
    ┌──────────────────────────────────┐
    │ Native TTS / MediaPlayer          │
    │ (Android/iOS platform APIs)       │
    └──────────────────────────────────┘
```

---

## Backend API Structure

**Project:** `src/WebApplication_API/WebApplication_API.csproj`  
**Framework:** ASP.NET Core 8  
**Database:** SQL Server, Entity Framework Core

### Directory Layout

```
src/WebApplication_API/
│
├── ┌─ STARTUP & CONFIGURATION ─┐
│   ├── Program.cs              # Startup, middleware, DI registration
│   │                           # - CORS setup
│   │                           # - Authentication/Authorization
│   │                           # - Swagger/OpenAPI
│   │                           # - Database initialization
│   │
│   ├── appsettings.json        # Configuration (DB, logging, JWT, API keys)
│   └── appsettings.Development.json
│
├── ┌─ CONTROLLERS (API Endpoints) ─┐
│   └── Controller/
│       ├── POIController.cs     # GET /api/pois, POST /api/pois, etc
│       │                        # CRUD operations for POI (Points of Interest)
│       │
│       ├── AudioController.cs   # Audio file upload/download/management
│       │
│       ├── UserController.cs    # User management, role assignment
│       │
│       ├── AuthController.cs    # Login, register, JWT token generation
│       │
│       ├── SyncController.cs    # Mobile delta sync endpoints
│       │
│       ├── AnalyticsController.cs  # Playback stats, heatmap data
│       │
│       └── [Other Controllers]
│
├── ┌─ DATA ACCESS (EF Core) ─┐
│   └── Data/
│       └── AppDbContext.cs     # Entity Framework DbContext
│                               # - Defines entities
│                               # - Configures relationships
│                               # - Migration history
│
├── ┌─ DOMAIN MODELS ─┐
│   └── Model/
│       ├── User.cs
│       ├── POI.cs              # Point of Interest entity
│       ├── AudioTrack.cs
│       ├── PlaybackHistory.cs
│       ├── Category.cs
│       └── [Other entities]
│
├── ┌─ DATA TRANSFER OBJECTS ─┐
│   └── DTO/
│       ├── UserDTO.cs
│       ├── POI_DTO.cs
│       ├── AudioTrackDTO.cs
│       ├── CreatePOIRequest.cs
│       ├── UpdatePOIRequest.cs
│       └── [Other DTOs]
│
├── ┌─ BUSINESS LOGIC ─┐
│   └── Services/
│       ├── POIService.cs       # POI CRUD business logic
│       ├── AudioService.cs     # Audio processing, TTS generation
│       ├── AuthService.cs      # Authentication logic
│       ├── UserService.cs      # User management
│       ├── SyncService.cs      # Mobile sync logic
│       ├── AnalyticsService.cs # Playback analytics
│       └── [Other services]
│
├── ┌─ VALIDATION ─┐
│   └── ModelBinding/
│       └─ Custom model binding, validation filters
│
├── ┌─ AUTHENTICATION ─┐
│   └── Security/
│       ├── JwtTokenGenerator.cs
│       └─ RBAC (Role-Based Access Control) logic
│
├── ┌─ BUILD OUTPUT ─┐
│   ├── bin/
│   │   ├── Debug/
│   │   │   └─ WebApplication_API.dll
│   │   └─ Release/
│   │
│   └── obj/
│       └─ [Intermediate build objects]
│
├── Properties/
│   ├── launchSettings.json     # Launch profile (port 5000, HTTPS, etc)
│   └── AssemblyInfo.cs
│
├── WebApplication_API.csproj   # Project file
│   └─ NuGet dependencies (EF Core, JWT, Swagger, etc)
│
└── WebApplication_API.http     # REST Client requests (VS Code extension)
    └─ Quick API testing
```

### REST API Structure

```
/api/
├── /pois                      # POI Management
│   ├── GET       /            # List all POIs
│   ├── GET       /{id}        # Get POI detail
│   ├── POST      /            # Create POI
│   ├── PUT       /{id}        # Update POI
│   ├── DELETE    /{id}        # Delete POI
│   └── POST      /{id}/audio  # Add audio file to POI
│
├── /audio                     # Audio Management
│   ├── GET       /{id}        # Download audio file
│   ├── POST      /            # Upload audio file
│   ├── DELETE    /{id}        # Delete audio file
│   └── POST      /tts         # Generate TTS
│
├── /users                     # User Management
│   ├── GET       /            # List users
│   ├── GET       /{id}        # Get user
│   ├── POST      /            # Create user
│   ├── PUT       /{id}        # Update user
│   └── DELETE    /{id}        # Delete user
│
├── /auth                      # Authentication
│   ├── POST      /login       # Login
│   ├── POST      /register    # Register
│   ├── POST      /refresh     # Refresh JWT token
│   └── POST      /logout      # Logout
│
├── /sync                      # Mobile Sync
│   ├── POST      /delta       # Delta sync (download changes)
│   └── POST      /push        # Push changes (upload)
│
└── /analytics                 # Analytics
    ├── GET       /top-pois    # Top POIs by play count
    ├── GET       /heatmap     # User location heatmap
    └── GET       /stats       # Overall statistics
```

---

## Admin Web Structure

**Project:** `src/BlazorApp_AdminWeb/BlazorApp_AdminWeb.csproj`  
**Framework:** Blazor Server  
**UI Framework:** Bootstrap 5

### Directory Layout

```
src/BlazorApp_AdminWeb/
│
├── ┌─ STARTUP & CONFIGURATION ─┐
│   ├── Program.cs              # Blazor startup, service registration
│   ├── App.razor               # Root Blazor app component
│   └── appsettings.json        # Configuration
│
├── ┌─ BLAZOR COMPONENTS ─┐
│   └── Components/
│       │
│       ├── Pages/              # Routable components (@page)
│       │   ├── POIManagement.razor
│       │   │   └─ Create/Edit/Delete POI
│       │   │   └─ Route: /poi-management
│       │   │
│       │   ├── UserManagement.razor
│       │   │   └─ User CRUD, role assignment
│       │   │   └─ Route: /user-management
│       │   │
│       │   ├── Dashboard.razor
│       │   │   └─ Analytics dashboard
│       │   │   └─ Route: /dashboard
│       │   │
│       │   ├── OwnerPortal.razor
│       │   │   └─ Owner self-service POI editing
│       │   │   └─ Route: /owner-portal
│       │   │
│       │   ├── Analytics.razor
│       │   │   └─ Playback stats, heatmap
│       │   │   └─ Route: /analytics
│       │   │
│       │   └── [Other Page components]
│       │
│       ├── Shared/             # Layout & shared components
│       │   ├── MainLayout.razor
│       │   │   └─ Main layout with navbar & sidebar
│       │   │
│       │   ├── NavMenu.razor
│       │   │   └─ Navigation sidebar
│       │   │
│       │   ├── Navbar.razor
│       │   │   └─ Top navigation bar
│       │   │
│       │   └── [Other shared components]
│       │
│       └── [Other components]  # Reusable Blazor components
│           ├── POIForm.razor   # POI create/edit form
│           ├── UserForm.razor
│           ├── ConfirmDialog.razor
│           └── [Other components]
│
├── ┌─ STATIC FILES ─┐
│   └── wwwroot/
│       ├── css/
│       │   ├── bootstrap.min.css
│       │   ├── app.css         # Custom styles
│       │   └── [Other CSS files]
│       │
│       ├── js/
│       │   ├── bootstrap.bundle.min.js
│       │   ├── app.js          # Custom scripts
│       │   └── [Other JS files]
│       │
│       └── images/
│           └─ [App logos, icons]
│
├── ┌─ SERVICES (API Client) ─┐
│   └── Services/
│       ├── ApiClient.cs        # HTTP client for backend API
│       │                       # - Base URL configuration
│       │                       # - Auth token handling
│       │
│       ├── POIService.cs       # POI API calls
│       ├── UserService.cs      # User API calls
│       ├── AuthService.cs      # Auth API calls
│       ├── AnalyticsService.cs # Analytics API calls
│       └── [Other API services]
│
├── ┌─ LAYOUT ─┐
│   └── Layouts/
│       └─ [Blazor layouts]
│
├── ┌─ BUILD OUTPUT ─┐
│   ├── bin/
│   │   ├── Debug/
│   │   │   └─ BlazorApp_AdminWeb.dll
│   │   └─ Release/
│   │
│   └── obj/
│       └─ [Intermediate objects]
│
├── Properties/
│   ├── launchSettings.json     # Launch profile (port 7000, https, etc)
│   └── AssemblyInfo.cs
│
├── BlazorApp_AdminWeb.csproj   # Project file
│   └─ NuGet dependencies (Blazor, Bootstrap, HttpClient, etc)
│
└── BlazorApp_AdminWeb.csproj.lscache
    └─ VS language service cache
```

### Blazor Page Routes

```
/                           # Dashboard (default)
/poi-management             # POI CRUD
/user-management            # User management
/owner-portal               # Owner self-service
/analytics                  # Analytics & reports
/settings                   # Admin settings
/login                      # Authentication page
```

---

## Shared Library

**Project:** `src/Project_SharedClassLibrary/Shared_ClassLibrary.csproj`  
**Framework:** .NET Standard 2.1  
**Purpose:** Shared contracts, DTOs, validators between Mobile, API, Admin Web

### Directory Layout

```
src/Project_SharedClassLibrary/
│
├── ┌─ DATA TRANSFER OBJECTS ─┐
│   └── Contracts/
│       ├── PublicAudioTrackDto.cs
│       ├── PoiDataTransferObject.cs
│       ├── UserDTO.cs
│       ├── CategoryDTO.cs
│       ├── PlaybackHistoryDTO.cs
│       ├── SyncRequestDTO.cs
│       ├── SyncResponseDTO.cs
│       └── [Other DTOs]
│
├── ┌─ CONSTANTS ─┐
│   └── Constants/
│       ├── ApiRoutes.cs        # API endpoint constants
│       ├── ValidationRules.cs  # Validation rule constants
│       ├── LanguageCodes.cs    # Supported language codes
│       └── [Other constants]
│
├── ┌─ GEOFENCING ─┐
│   └── Geofencing/
│       ├── PoiGeofenceDefinition.cs
│       ├── GeofenceTriggeredEvent.cs
│       ├── NativeGeofenceRegistrationResult.cs
│       └── [Other geofence models]
│
├── ┌─ SECURITY & AUTHENTICATION ─┐
│   └── Security/
│       ├── RoleConstants.cs    # RBAC role constants
│       ├── PermissionHelper.cs # Permission checking
│       ├── TokenValidator.cs   # JWT token validation
│       └── [Other security helpers]
│
├── ┌─ VALIDATION ─┐
│   └── Validation/
│       ├── ValidationAttributes.cs  # Custom validation attributes
│       ├── ValidationRules.cs
│       └── [Other validators]
│
├── ┌─ STORAGE ─┐
│   └── Storage/
│       ├── IStorageService.cs  # Storage abstraction
│       └── [Storage implementations]
│
├── bin/
│   ├── Debug/
│   │   └─ Shared_ClassLibrary.dll
│   └── Release/
│
├── obj/
│   └─ [Intermediate objects]
│
└── Shared_ClassLibrary.csproj
    └─ No NuGet dependencies (pure .NET Standard)
```

---

## Documentation & Scripts

### Documentation Folder

```
docs/
│
├── specification.md            # Complete feature specification
│                              # - Use stories, acceptance criteria
│                              # - Feature breakdown by component
│                              # - Technical requirements
│
├── task.md                    # Development task checklist
│                              # - Completed tasks (✅)
│                              # - In-progress tasks
│                              # - TODO items
│
├── DatabaseStructure/
│   ├── database.sql           # SQL Server schema
│   ├── mobile-sqlite-migration.sql  # SQLite schema
│   ├── sample-data.sql        # Sample POI data for testing
│   └── smarttour-mobile.db3   # SQLite template database
│
├── Diagram/                   # Architecture diagrams
│   └─ [Visio, PNG, or SVG files]
│
└── PRD-DoAnC#-.docx           # Product Requirement Document
                               # (Vietnamese - legacy)
```

### Scripts Folder

```
scripts/
│
├── run-android-clean.ps1
│   └─ PowerShell script to clean and rebuild Android app
│   └─ Usage: .\run-android-clean.ps1
│
├── start-smarttour-tunnel.ps1
│   └─ Setup local development tunnel for external access
│   └─ Usage: .\start-smarttour-tunnel.ps1
│
└── update-android-network-security-config.ps1
    └─ Update Android network security config for non-HTTPS APIs
    └─ Usage: .\update-android-network-security-config.ps1
```

---

## Architecture Patterns

### MVVM Pattern (Mobile)

```
View (XAML)  ←→  ViewModel  ←→  Model  ←→  Service
                 (Bindings)                (Data)
                
Example:
MapPage.xaml  ←→  MapViewModel  ←→  POI  ←→  LocationTrackingService
(UI)            (State, Commands)    (Data)   (GPS updates)
```

### MVC-Like Pattern (Backend API)

```
Request  →  Controller  →  Service  →  Repository  →  Database
           (Route)      (Logic)      (EF Core)    (SQL)
                          ↓
                       Response
```

### Blazor Component Pattern (Admin Web)

```
@page "/route"              # Routable component
@inherits ComponentBase     # Base class
@inject ApiClient api       # Inject services

<Component>
  @for (var item in items)  # Data binding
  {
    <Item @key="item.Id" Item="item" />
  }
</Component>

@code {
  protected override async Task OnInitializedAsync()
  {
    items = await api.GetItemsAsync();
  }
}
```

### Async/Await Pattern (All Projects)

```csharp
// Everywhere for async operations
public async Task<PoiData> GetPoiAsync(int id)
{
    var response = await _httpClient.GetAsync($"/api/pois/{id}");
    return await response.Content.ReadAsAsync<PoiData>();
}

// In MAUI Services
await PlaybackCoordinatorService.Instance.PlayQueueAsync(items, index);

// In Blazor components
items = await _poiService.GetAllAsync();
StateHasChanged();
```

---

## Dependency Injection (DI)

### Mobile App (MauiProgram.cs)

```csharp
builder.Services
    .AddSingleton<PlaybackCoordinatorService>()
    .AddSingleton<AudioPlaybackService>()
    .AddScoped<LocationTrackingService>()
    .AddScoped<GeofenceOrchestratorService>()
    // ... etc
```

### Backend API (Program.cs)

```csharp
builder.Services
    .AddScoped<DbContext>()
    .AddScoped<IPoiService, PoiService>()
    .AddScoped<IAuthService, AuthService>()
    // ... etc
```

### Admin Web (Program.cs)

```csharp
builder.Services
    .AddScoped<HttpClient>()
    .AddScoped<ApiClient>()
    .AddScoped<IPoiService, PoiService>()
    // ... etc
```

---

## File Naming Conventions

| Item | Convention | Example |
|------|-----------|---------|
| XAML Page | `PageName.xaml` | `MapPage.xaml` |
| Code-behind | `PageName.xaml.cs` | `MapPage.xaml.cs` |
| Service | `ServiceName.cs` (or with domain) | `PlaybackCoordinatorService.cs` |
| DTO/Model | `ClassNameDTO.cs` or `ClassName.cs` | `PoiDataTransferObject.cs` |
| Interface | `IServiceName.cs` | `IPoiService.cs` |
| Blazor Component | `ComponentName.razor` | `POIForm.razor` |
| Test | `ClassName.Tests.cs` | `PlaybackCoordinator.Tests.cs` |

---

## Key Design Principles

1. **Separation of Concerns**: UI, Business Logic, Data Access separated
2. **Offline-First**: Mobile works without internet, syncs when available
3. **MVVM/MVC**: Clear architecture pattern in each project
4. **DI/IoC**: All services injected, not instantiated directly
5. **Async/Await**: All I/O operations are async
6. **Error Handling**: Try-catch with meaningful logging
7. **Validation**: Input validated at entry point
8. **Single Responsibility**: Each service has one clear purpose

---

## Next Steps

- Read [README.md](../README.md) for project overview
- Follow [SETUP.md](./SETUP.md) to configure development environment
- Explore source code starting from entry points (App.xaml.cs, Program.cs, etc.)
- Review [specification.md](./specification.md) for feature details
- Check [task.md](./task.md) for current development status

