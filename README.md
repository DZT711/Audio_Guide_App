# 📱 Smart Tourism MAUI — Audio Guide App

**Ứng dụng thuyết minh âm thanh tự động cho Phố Ẩm Thực Vĩnh Khánh, TP. Hồ Chí Minh**

> A geofencing-triggered, offline-first, multi-language audio tour system for tourists — paired with a Blazor admin dashboard for full content management.

<br/>

[![Status](https://img.shields.io/badge/Status-Active%20Development-yellow?style=for-the-badge)](https://github.com/DZT711/Smart-Tourism-MAUI)
[![License](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey?style=for-the-badge)](https://creativecommons.org/licenses/by-nc-sa/4.0/)
[![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows-blue?style=for-the-badge)](https://github.com/DZT711/Smart-Tourism-MAUI)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ngrok](https://img.shields.io/badge/ngrok-deploy%20%2F%20tunnel-1F1E37?style=for-the-badge&logo=ngrok&logoColor=white)](https://ngrok.com/)

---

## 🧰 Tech Stack & Languages

<p align="left">
  <img src="https://skillicons.dev/icons?i=dotnet,cs,html,css,js,ps,sqlite,android,visualstudio" alt="Tech stack icons" />
</p>

| Layer | Badge | Version |
|---|---|---|
| **Mobile App** | [![MAUI](https://img.shields.io/badge/.NET_MAUI-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/maui/) | .NET 10 |
| **Backend API** | [![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/aspnet/core/) | .NET 10 |
| **Admin Dashboard** | [![Blazor](https://img.shields.io/badge/Blazor_Server-512BD4?style=flat-square&logo=blazor&logoColor=white)](https://learn.microsoft.com/en-us/aspnet/core/blazor/) | .NET 10 |
| **Shared Library** | [![.NET](https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/) | .NET 10 |
| **Language** | [![C#](https://img.shields.io/badge/C%23-239120?style=flat-square&logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/) | primary (61.5%) |
| **Database** | [![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite&logoColor=white)](https://www.sqlite.org/) | sqlite-net-pcl |
| **ORM** | [![EF Core](https://img.shields.io/badge/EF_Core-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/ef/core/) | EF Core SQLite |
| **Map** | [![Leaflet](https://img.shields.io/badge/Leaflet-199900?style=flat-square&logo=leaflet&logoColor=white)](https://leafletjs.com/) [![OpenStreetMap](https://img.shields.io/badge/OpenStreetMap-7EBC6F?style=flat-square&logo=openstreetmap&logoColor=white)](https://www.openstreetmap.org/) | — |
| **Web (Map/UI)** | [![HTML](https://img.shields.io/badge/HTML-E34F26?style=flat-square&logo=html5&logoColor=white)](https://developer.mozilla.org/en-US/docs/Web/HTML) [![CSS](https://img.shields.io/badge/CSS-1572B6?style=flat-square&logo=css3&logoColor=white)](https://developer.mozilla.org/en-US/docs/Web/CSS) [![JavaScript](https://img.shields.io/badge/JavaScript-F7DF1E?style=flat-square&logo=javascript&logoColor=black)](https://developer.mozilla.org/en-US/docs/Web/JavaScript) | 28.7% / 4.6% / 2.7% |
| **Scripts** | [![PowerShell](https://img.shields.io/badge/PowerShell-5391FE?style=flat-square&logo=powershell&logoColor=white)](https://learn.microsoft.com/en-us/powershell/) | 2.5% |
| **Tunnel / Deploy** | [![ngrok](https://img.shields.io/badge/ngrok-1F1E37?style=flat-square&logo=ngrok&logoColor=white)](https://ngrok.com/) | API tunnel on :5123 |
| **App Targets** | [![Android](https://img.shields.io/badge/Android-34A853?style=flat-square&logo=android&logoColor=white)](https://developer.android.com/) [![Windows](https://img.shields.io/badge/Windows-0078D4?style=flat-square&logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/windows/apps/) | Android API 24+ / Windows 10.0.19041+ |

---

## ⚙️ Engine & Framework

The project is built on **.NET MAUI** (Multi-platform App UI), targeting **Android** and **Windows** from a single C# codebase. The backend runs **ASP.NET Core** on Kestrel; the admin panel uses **Blazor Server**.

| Component | Engine | Badge |
|---|---|---|
| Mobile runtime | .NET MAUI (.NET 10) | [![MAUI](https://img.shields.io/badge/.NET_MAUI-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/maui/) |
| Admin web runtime | Blazor Server (.NET 10) | [![Blazor](https://img.shields.io/badge/Blazor_Server-10-512BD4?style=flat-square&logo=blazor&logoColor=white)](https://learn.microsoft.com/en-us/aspnet/core/blazor/) |
| REST API runtime | ASP.NET Core (.NET 10) — Kestrel | [![ASP.NET](https://img.shields.io/badge/ASP.NET_Core-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/aspnet/core/) |
| ORM — server | EF Core SQLite | [![EF Core](https://img.shields.io/badge/EF_Core-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/ef/core/) |
| ORM — mobile | sqlite-net-pcl | [![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite&logoColor=white)](https://www.sqlite.org/) |
| Audio TTS — Android | Android `TextToSpeech` engine | [![Android](https://img.shields.io/badge/Android_TTS-34A853?style=flat-square&logo=android&logoColor=white)](https://developer.android.com/reference/android/speech/tts/TextToSpeech) |
| GPS — Android | MAUI Geolocation + Android foreground service | [![Android](https://img.shields.io/badge/Android_Geolocation-34A853?style=flat-square&logo=android&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation) |
| Map engine | Leaflet.js inside MAUI WebView | [![Leaflet](https://img.shields.io/badge/Leaflet.js-199900?style=flat-square&logo=leaflet&logoColor=white)](https://leafletjs.com/) |
| API tunnel | ngrok on port 5123 | [![ngrok](https://img.shields.io/badge/ngrok-1F1E37?style=flat-square&logo=ngrok&logoColor=white)](https://ngrok.com/) |

---

## 📁 Project Structure

```
Smart-Tourism-MAUI/
├── MauiApp_Mobile/              # .NET MAUI App (Android + Windows)
│   ├── Services/
│   │   ├── PlaybackCoordinatorService.cs   # Audio queue management
│   │   ├── AudioPlaybackService.cs         # TTS + MP3 playback
│   │   ├── LocationTrackingService.cs      # GPS (foreground + background)
│   │   ├── Geofencing/                     # Haversine geofence engine
│   │   ├── AudioDownloadService.cs         # Audio file caching
│   │   ├── MobileDatabaseService.cs        # SQLite ORM layer
│   │   └── AppSettingsService.cs           # User preferences
│   ├── Views/                              # XAML Pages (Map, Settings, Offline)
│   ├── ViewModels/                         # MVVM ViewModels
│   ├── Platforms/Android|Windows/          # Platform-specific services
│   └── Resources/Raw/leaflet_map.html      # Leaflet map (WebView)
│
├── WebApplication_API/          # ASP.NET Core REST API
│   ├── Controller/              # REST endpoints (Location, Audio, User, Telemetry, Auth)
│   ├── Data/DBContext.cs        # EF Core DbContext
│   ├── Model/                   # Domain entities
│   ├── DTO/                     # Data Transfer Objects
│   └── Services/                # Business logic
│
├── BlazorApp_AdminWeb/          # Admin Dashboard (Blazor Server)
│   ├── Components/Pages/
│   │   ├── POIList.razor        # Create / Edit / Delete POI
│   │   ├── UserList.razor       # User & role management
│   │   ├── Statistics.razor     # Dashboard analytics
│   │   └── ModerationList.razor # Owner change-request review
│   └── Services/                # API client, data services
│
├── Project_SharedClassLibrary/  # Shared DTOs, contracts, validation (.NET 10)
│   ├── Contracts/               # DTOs shared across all projects
│   ├── Geofencing/              # Shared geofence models
│   ├── Security/                # RBAC helpers
│   └── Validation/              # Shared validation logic
│
├── docs/
│   ├── specification.md         # Feature specification & user stories
│   ├── DatabaseStructure/       # SQLite schema, migrations, sample data
│   └── Diagram/                 # Architecture diagrams
│
└── scripts/                     # PowerShell dev utilities
```

---

## 🔄 App / Web Flow

### Mobile App Flow

```
[App Launch]
     │
     ├─► Load local SQLite (POI data, audio cache)
     ├─► Check network → if online: refresh public catalog from API
     │
     ▼
[Location Service starts]
     │  GPS polling interval follows the selected accuracy mode / foreground service on Android
     ▼
[Geofence Engine — Haversine formula]
     │  Distance check against all POIs (default radius: 30m)
     │  Cooldown: 5 min per POI to prevent audio spam
     ▼
[Audio Decision — Hybrid playback]
     │
     ├─ Tier 1: Cached MP3 on device          → play immediately (offline)
     ├─ Tier 2: TTS script stored locally      → Device TTS
     └─ Tier 3: Optional Gemini speech service → server-side translation/TTS preview
     │
     ▼
[Playback Queue Manager]
     │  Play → Pause → Next → Previous
     │  Auto-pause on OS notification / incoming call
     ▼
[History logged → sync to server when online]
```

### QR Code Direct-Play Flow

```
[Tourist scans QR at venue]
     │
     ▼
[Deep link → opens app or web landing page]
     │
     ▼
[POI ID resolved → audio plays immediately — no GPS required]
```

### Admin Web Flow

```
[Admin logs in — session-token auth]
     │
     ├─► POI Management     → Create / Edit / Delete → saved to SQLite via EF Core
     ├─► Audio Management   → Upload MP3 → stored on server → served to mobile
     ├─► Translation Mgmt   → Add multilingual TTS scripts per POI
     ├─► Tour Management    → Group POIs into ordered tours
     ├─► Analytics          → Playback logs, heatmaps, top POIs
     └─► Owner Portal       → Shop owners manage own POI content (restricted RBAC)
```

---

## 🌐 Internal REST API

Base URL: `http://<server>:5123` (or `https://localhost:7284` when using the HTTPS launch profile)

### POI — Points of Interest

| Method | Endpoint | Description |
|---|---|---|
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Location` | List all POIs |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Location/{id}` | Get POI detail |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Location/public/catalog` | Public POI catalog (mobile, no auth) |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Location/category/{categoryId}` | List POIs by category |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Location` | Create POI |
| ![PUT](https://img.shields.io/badge/PUT-FCA130?style=flat-square&logoColor=white) | `/Location/{id}` | Update POI |
| ![DELETE](https://img.shields.io/badge/DELETE-F93E3E?style=flat-square&logoColor=white) | `/Location/{id}` | Delete POI |

### Audio

| Method | Endpoint | Description |
|---|---|---|
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Audio` | List audio records |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Audio/{id}` | Get audio detail |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Audio/public/location/{locationId}` | Public audio tracks for one POI |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Audio/public/location/{locationId}/default` | Default public audio track |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Audio` | Create or upload audio content |
| ![PUT](https://img.shields.io/badge/PUT-FCA130?style=flat-square&logoColor=white) | `/Audio/{id}` | Update audio content |
| ![DELETE](https://img.shields.io/badge/DELETE-F93E3E?style=flat-square&logoColor=white) | `/Audio/{id}` | Delete audio content |

### Authentication & Users

| Method | Endpoint | Description |
|---|---|---|
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Auth/login` | Login — returns an admin session token |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Auth/me` | Current admin session |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Auth/logout` | End admin session |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/DashboardUser` | List users |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/DashboardUser` | Create user |
| ![PUT](https://img.shields.io/badge/PUT-FCA130?style=flat-square&logoColor=white) | `/DashboardUser/{id}` | Update user / role |

### Telemetry & Analytics

| Method | Endpoint | Description |
|---|---|---|
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Telemetry/v1/route-history` | Ingest mobile route telemetry |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Telemetry/v1/audio-play-events` | Ingest playback events |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/Telemetry/v1/heatmap-events` | Ingest heatmap events |
| ![POST](https://img.shields.io/badge/POST-49CC90?style=flat-square&logoColor=white) | `/api/v1/analytics/events` | Ingest usage analytics event |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Statistics/top-pois` | Top POIs by play count |
| ![GET](https://img.shields.io/badge/GET-61AFFE?style=flat-square&logoColor=white) | `/Statistics/heatmap` | User position heatmap data |

---

## 🔗 External APIs & Services

| Service | Badge | Purpose |
|---|---|---|
| OpenStreetMap | [![OpenStreetMap](https://img.shields.io/badge/OpenStreetMap-7EBC6F?style=flat-square&logo=openstreetmap&logoColor=white)](https://www.openstreetmap.org/) | Base map tile data |
| OSRM demo routing | [![OSRM](https://img.shields.io/badge/OSRM-routing-44A833?style=flat-square)](https://project-osrm.org/) | Walking route planning through `routing.openstreetmap.de` |
| Gemini Speech | [![Gemini](https://img.shields.io/badge/Gemini-optional-4285F4?style=flat-square&logo=googlegemini&logoColor=white)](https://ai.google.dev/) | Optional server-side translation and TTS preview |
| ngrok | [![ngrok](https://img.shields.io/badge/ngrok-tunnel%20%3A5123-1F1E37?style=flat-square&logo=ngrok&logoColor=white)](https://ngrok.com/) | Public HTTPS tunnel for the API — device & field testing |
| MAUI Geolocation / Android foreground service | [![Android](https://img.shields.io/badge/Android_Geolocation-34A853?style=flat-square&logo=android&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation) | GPS polling and background tracking on Android |
| Android TextToSpeech | [![Android](https://img.shields.io/badge/Android_TTS-34A853?style=flat-square&logo=android&logoColor=white)](https://developer.android.com/reference/android/speech/tts/TextToSpeech) | On-device TTS fallback |

> Gemini speech is disabled by default in `appsettings.json`; enable it only when you provide a Gemini API key.

---

## ✨ Features

### 📱 Mobile App

| Feature | Details |
|---|---|
| **GPS Tracking** | Configurable foreground polling + Android foreground service |
| **Geofencing** | Haversine formula · configurable radius (default 30m) · 5-min cooldown |
| **Hybrid Audio Playback** | Cached MP3 / downloaded audio → local TTS script → optional Gemini speech |
| **Audio Queue** | Play, pause, next, previous · auto-pause on notification / incoming call |
| **Multi-language** | 🇻🇳 Vietnamese · 🇬🇧 English · 🇨🇳 Chinese · 🇯🇵 Japanese · 🇰🇷 Korean |
| **Offline Mode** | Local SQLite catalog cache + downloaded/cached audio |
| **Telemetry Sync** | Queued route, playback, listening-session, heatmap, and usage events sync when connectivity is restored |
| **QR Code Scan** | Scan QR at a venue → plays that POI's audio directly, bypassing GPS |
| **Interactive Map** | Leaflet + OpenStreetMap in WebView · all POIs + current location |
| **Settings** | Language selector · GPS sensitivity · TTS voice · offline pack download |

### 🎛️ Admin Dashboard

| Feature | Details |
|---|---|
| **POI CRUD** | Create, edit, delete POIs with coordinates, radius, priority, images |
| **Audio Management** | Upload pre-recorded MP3 files · manage TTS scripts per language |
| **Translation Management** | Add/edit multilingual content per POI |
| **Tour Management** | Group POIs into ordered tours |
| **User & Role Management** | Admin assignment · shop owner verification · RBAC |
| **Analytics Dashboard** | Top POIs by play count · average listen time · date range filtering |
| **Heatmap** | Visual heatmap of user positions across the food street |
| **Owner Portal** | Self-service: shop owners edit their own POI and upload audio |

### 🗄️ Database

**[![SQLite](https://img.shields.io/badge/SQLite-both_server_%26_mobile-003B57?style=flat-square&logo=sqlite&logoColor=white)](https://www.sqlite.org/)**

| Table | Used by | Purpose |
|---|---|---|
| `Locations` / `CachedLocations` | Server + Mobile | Points of interest: name, coords, radius, priority, category |
| `AudioContents` / `CachedAudioTracks` | Server + Mobile | Audio records, scripts, language, source type, priority |
| `Categories` / `CachedCategories` | Server + Mobile | POI category metadata |
| `Languages` | Server | Managed language records |
| `Tours`, `TourLocations` | Server | Grouped POI tours with ordering and route data |
| `DashboardUsers` | Server | Admin/owner/user accounts and roles |
| `PlaybackEvents`, `AudioListeningSessions` | Server | Playback and listening analytics |
| `LocationTrackingEvents`, `HeatmapEvents`, `UsageEvents` | Server + Mobile queue | Telemetry and usage analytics |
| `ChangeRequests`, `InboxMessages`, `ActivityLogs` | Server | Owner moderation workflow, notifications, and audit trail |
| `LocalSettings`, `DeviceSyncStates`, `PlaybackHistory` | Mobile | User preferences, catalog sync state, and local history |

---

## 🚀 Getting Started

### Prerequisites

[![VS 2022](https://img.shields.io/badge/Visual_Studio-2022-5C2D91?style=flat-square&logo=visualstudio&logoColor=white)](https://visualstudio.microsoft.com/)
[![.NET](https://img.shields.io/badge/.NET_SDK-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download)
[![Android SDK](https://img.shields.io/badge/Android_SDK-API_24%2B-34A853?style=flat-square&logo=android&logoColor=white)](https://developer.android.com/studio)
[![SQLite](https://img.shields.io/badge/SQLite-included-003B57?style=flat-square&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![ngrok](https://img.shields.io/badge/ngrok-optional-1F1E37?style=flat-square&logo=ngrok&logoColor=white)](https://ngrok.com/download)

### Clone & Restore

```bash
git clone https://github.com/DZT711/Smart-Tourism-MAUI.git
cd Smart-Tourism-MAUI
dotnet restore Smart-Tourism-MAUI.sln
```

### 1 — Backend API

```bash
# Uses WebApplication_API/appsettings.json (default SQLite: Data Source=App.db)
dotnet run --project WebApplication_API/WebApplication_API.csproj --launch-profile http

# Expose for device testing (optional)
ngrok http 5123 --host-header="localhost:5123"
```

### 2 — Admin Dashboard

```bash
# appsettings.json defaults to http://localhost:5123/
dotnet run --project BlazorApp_AdminWeb/BlazorApp_AdminWeb.csproj
# Login with a seeded DashboardUser account
```

### 3 — Mobile App

**Windows (quick test):**
```bash
dotnet run --project MauiApp_Mobile/MauiApp_Mobile.csproj -f net10.0-windows10.0.19041.0
```

**Android — USB cable:**
```bash
adb devices                        # confirm device shows as "device"
adb reverse tcp:5123 tcp:5123      # forward API port to device
dotnet run --project MauiApp_Mobile/MauiApp_Mobile.csproj -f net10.0-android
```

**Android — Wi-Fi:**
```bash
adb connect <device-ip>:<port>
adb devices
# Server URLs are configured in MauiApp_Mobile/Resources/Raw/mobile-api.json.
# Debug Android builds also update network_security_config.xml before build.
dotnet run --project MauiApp_Mobile/MauiApp_Mobile.csproj -f net10.0-android
```

**Pull SQLite DB from device:**
```bash
adb exec-out run-as com.companyname.mauiapp_mobile cat files/smarttour-mobile.db3 > docs/smarttour-mobile.db
```

### 4 — Database Setup

SQLite databases are created automatically on first run — no manual setup needed.

The API applies EF Core migrations and seeds baseline admin users, POIs, audio, tours, and analytics samples at startup.

```bash
# Mobile SQLite (applied automatically on app launch)
# Schema: docs/DatabaseStructure/mobile-sqlite-migration.sql

# Server SQLite — optional manual migration command
cd WebApplication_API
dotnet ef database update

# Optional SQL sample data lives in docs/DatabaseStructure/sample-data.sql
```

---

## 🌐 Publishing & Deployment with ngrok

[![ngrok](https://img.shields.io/badge/ngrok-1F1E37?style=for-the-badge&logo=ngrok&logoColor=white)](https://ngrok.com/)

ngrok creates a secure public HTTPS tunnel to the local ASP.NET Core API running on port **5123**, making it reachable from physical Android devices and external testers without configuring firewalls, port forwarding, or a cloud server.

### Why ngrok?

| Scenario | Without ngrok | With ngrok |
|---|---|---|
| Android device on same Wi-Fi | Needs LAN IP (`192.168.x.x`) — may still fail on restricted networks | Single stable HTTPS URL |
| Android device on mobile data | ❌ Unreachable | ✅ Works from anywhere |
| Share API with a team member | ❌ Not possible without VPN | ✅ Send them the tunnel URL |
| Test push-style telemetry sync | Requires static IP or cloud VM | ✅ ngrok URL + inspect dashboard |
| Admin dashboard from a phone | Must be on the same LAN | ✅ Any browser, any network |

### Installation

**Windows (winget):**
```bash
winget install ngrok.ngrok
```

**Windows (Chocolatey):**
```bash
choco install ngrok
```

**macOS (Homebrew):**
```bash
brew install ngrok/ngrok/ngrok
```

**Linux:**
```bash
curl -sSL https://ngrok-agent.s3.amazonaws.com/ngrok.asc \
  | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc >/dev/null \
  && echo "deb https://ngrok-agent.s3.amazonaws.com buster main" \
  | sudo tee /etc/apt/sources.list.d/ngrok.list \
  && sudo apt update && sudo apt install ngrok
```

Or download the binary directly from [ngrok.com/download](https://ngrok.com/download).

### Account & Auth Token (one-time setup)

1. Sign up for a free account at [dashboard.ngrok.com](https://dashboard.ngrok.com).
2. Copy your **Authtoken** from the dashboard.
3. Register it locally:

```bash
ngrok config add-authtoken <YOUR_AUTHTOKEN>
```

### Start the Tunnel

Run the API first, then open the tunnel in a separate terminal:

```bash
# Terminal 1 — start the API
dotnet run --project WebApplication_API/WebApplication_API.csproj --launch-profile http
# Kestrel listens on http://localhost:5123

# Terminal 2 — open the ngrok tunnel
ngrok http 5123 --host-header="localhost:5123"
```

ngrok will print output like:

```
Forwarding  https://a1b2-103-xxx-xxx-xxx.ngrok-free.app -> http://localhost:5123
```

The `https://` URL is your public API endpoint. Copy it.

### Configure the Mobile App to Use the Tunnel URL

Open `MauiApp_Mobile/Resources/Raw/mobile-api.json` and replace the base URL:

```json
{
  "ApiBaseUrl": "https://a1b2-103-xxx-xxx-xxx.ngrok-free.app",
  "PublicCatalogEndpoint": "/Location/public/catalog",
  "AudioEndpoint": "/Audio/public/location"
}
```

> **Note:** The free ngrok tier generates a new random URL every time you restart the tunnel. Paste the new URL into `mobile-api.json` and redeploy, or upgrade to a paid ngrok plan to get a **static subdomain** (e.g., `https://smart-tourism.ngrok.app`).

### Configure the Admin Dashboard to Use the Tunnel URL

Open `BlazorApp_AdminWeb/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://a1b2-103-xxx-xxx-xxx.ngrok-free.app"
  }
}
```

### ASP.NET Core — Allow the ngrok Host Header

Add the following to `WebApplication_API/appsettings.json` so Kestrel accepts requests forwarded by ngrok:

```json
{
  "AllowedHosts": "*"
}
```

Or configure forwarded headers in `Program.cs` to preserve the original scheme and host:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

### ngrok Inspection Dashboard

While the tunnel is running, open [http://127.0.0.1:4040](http://127.0.0.1:4040) in your browser to see every HTTP request in real time — headers, payloads, status codes, and timing. This is especially useful for debugging telemetry sync from the mobile app.

### ngrok Configuration File (optional)

For a one-command startup of both the API tunnel and the admin dashboard tunnel, create `~/.config/ngrok/ngrok.yml` (or `%HOMEPATH%\AppData\Local/ngrok/ngrok.yml` on Windows):

```yaml
version: "3"
agent:
  authtoken: <YOUR_AUTHTOKEN>

tunnels:
  api:
    proto: http
    addr: 5123
    host_header: "localhost:5123"
    # schemes: [https]          # force HTTPS only (paid plans)
    # domain: smart-tourism.ngrok.app  # static domain (paid plans)

  admin:
    proto: http
    addr: 5223
    host_header: "localhost:5223"
```

Start all tunnels at once:

```bash
ngrok start --all
```

### Free vs Paid ngrok Plans

| Feature | Free | Paid |
|---|---|---|
| HTTPS tunnel | ✅ | ✅ |
| Random URL (changes on restart) | ✅ | ✅ |
| Static / custom subdomain | ❌ | ✅ |
| Simultaneous tunnels | 1 | Multiple |
| Requests per minute | Rate-limited | Higher limits |
| TCP tunnels | ❌ | ✅ |
| IP restrictions | ❌ | ✅ |

For academic/demo use the **free tier** is sufficient. For a persistent field deployment at Phố Ẩm Thực Vĩnh Khánh, a static domain on a paid plan or a proper cloud VM (e.g., Azure App Service, Railway, Fly.io) is recommended.

### Tunnel Architecture Diagram

```
[Android Device / Tester Browser]
          │  HTTPS :443
          ▼
  ┌───────────────────────┐
  │  ngrok Edge (Cloud)   │   https://xxxx.ngrok-free.app
  └───────────┬───────────┘
              │  encrypted tunnel
              ▼
  ┌───────────────────────┐
  │  ngrok Agent (local)  │   running on dev machine
  └───────────┬───────────┘
              │  HTTP
              ▼
  ┌───────────────────────┐
  │  ASP.NET Core Kestrel │   http://localhost:5123
  │  WebApplication_API   │
  └───────────────────────┘
```

---

## 📖 Documentation

| Document | Purpose |
|---|---|
| [`docs/specification.md`](docs/specification.md) | Full feature specification & user stories |
| [`docs/DatabaseStructure/`](docs/DatabaseStructure) | SQLite schema, migrations, sample data |
| [`docs/Diagram/`](docs/Diagram) | Architecture & flow diagrams |

---

## 📜 License

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey?style=for-the-badge)](https://creativecommons.org/licenses/by-nc-sa/4.0/)

**Creative Commons Attribution – NonCommercial – ShareAlike 4.0 International**

Copyright © 2026 Nguyễn Sĩ Huy (3123411122) & Nguyễn Văn Cường (3123411045)
*Khoa Công nghệ Thông tin — Dự Án Thuyết Minh Phố Ẩm Thực Vĩnh Khánh*

You are free to:
- **Share** — copy and redistribute this material in any medium or format
- **Adapt** — remix, transform, and build upon the material

Under the following terms:
- **Attribution** — Give appropriate credit and link to this repository.
- **NonCommercial** — You may not use the material for commercial purposes.
- **ShareAlike** — Derivatives must be distributed under the same license.

Full license: https://creativecommons.org/licenses/by-nc-sa/4.0/

> This project was built as an academic capstone at Ho Chi Minh City University of Technology and Education (UTE) and is intended for non-commercial, educational, and cultural heritage use only.

---

## 🙏 Acknowledgments

[![OpenStreetMap](https://img.shields.io/badge/OpenStreetMap-map_data-7EBC6F?style=flat-square&logo=openstreetmap&logoColor=white)](https://www.openstreetmap.org/)
[![Leaflet](https://img.shields.io/badge/Leaflet.js-interactive_map-199900?style=flat-square&logo=leaflet&logoColor=white)](https://leafletjs.com/)
[![OSRM](https://img.shields.io/badge/OSRM-routing-44A833?style=flat-square)](https://project-osrm.org/)
[![ngrok](https://img.shields.io/badge/ngrok-tunnel_%26_testing-1F1E37?style=flat-square&logo=ngrok&logoColor=white)](https://ngrok.com/)

Special thanks to **Vinh Khanh Food Street** (Phố Ẩm Thực Vĩnh Khánh) as the project location and primary stakeholder.

---

*Last Updated: May 2026 · Status:* 🟡 *Active Development*
