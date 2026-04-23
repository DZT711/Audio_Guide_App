# 📋 Project Task List — Audio Tour POI App

---

## I. Mobile App (Client)

### 🔧 Setup & Infrastructure

- [x] Generate QR code for app installation link (deep link / store link)
- [x] Configure app for online/offline mode detection
  - Online → connect to SQL Server
  - [x] Online → connect to sqlite asp server
  - [x] Offline → use local SQLite database
- [x] Set up `.NET MAUI` project (Android + iOS targets)
- [x] Configure dependency injection and platform services

---

### 📍 GPS Tracking (PoC)

- [x] Implement foreground GPS tracking
- [x] Implement background GPS tracking
  - Android: `FusedLocationProviderClient` via Foreground Service
  - iOS: `CLLocationManager` with `Always` permission
- [x] Optimize for battery efficiency (adaptive polling interval)
- [x] Improve location accuracy as much as possible
- [x] Handle location permission requests (foreground & background)

---

### 🗺️ Geofencing & Auto Audio Playback (PoC)

- [x] Define POI data model:
  - [x] Coordinate (Longitude, Latitude)
  - [x] Activation Radius
  - [x] Priority level
- [x] Implement geofence engine:
  - [x] Native geofencing API (Android / iOS region monitoring), **OR**
  - [x] Manual distance calculation using **Haversine formula**
- [x] Trigger audio playback when user **enters** a POI radius
- [x] Trigger audio playback when user **stands near** a POI point
- [x] Implement **debounce / cooldown** mechanism to prevent audio spam

---

### 🔊 Narration Engine (PoC)

- [x] Implement **Text-to-Speech (TTS)**:
  - Android: Android TTS engine
  - iOS: `AVSpeechSynthesizer`
  <!-- - [ ] Optional: Azure Cognitive Services (natural voice, online/offline notice) -->
  - [x] Auto-detect device locale → default Vietnamese if region is VN  (LocalizationService.cs-CultureInfo.CurrentUICulture,RegionInfo.CurrentRegion.TwoLetterISORegionName)
  - [x] Support multiple languages (flexible, low memory footprint)
- [x] Implement **Recorded Audio playback**:
  - [x] Professional/natural voice quality
  - Note: higher memory usage vs TTS
- [x] Build **Audio Queue Manager**:
  - [x] Multi-process scheduling
  - [x] No audio repetition logic
  - [x] Auto-stop / pause when OS notification arrives
- [x] Decide per-POI: use TTS script or pre-recorded audio file

---

### 🗂️ POI Management (PoC)

- [x] Build POI list screen:
  - [x] Place name & description
  - [x] Illustration / image of the place
  - [x] Link to map
  - [x] Audio file or TTS script field
- [x] Suggest Figma AI for UI/UX design of POI screens

---

### 🗺️ Map View (PoC)

- [x] Display current user location on map
- [x] Show all POIs on map
- [x] Highlight nearest POI
- [x] Choose map library: Leaflet-OpenStreetMap
  <!-- - [ ] `Microsoft.Maui.Controls.Maps` (basic)
  - [ ] Google Maps SDK / MapKit binding (advanced)
  - [ ] Mapbox or HERE SDK (offline map caching) -->

---

### 📦 Offline Data & Sync (PoC)

- [x] Set up **SQLite** database (via EF Core or sqlite-net)
- [x] Store POI data locally for offline use
- [x] Pre-load audio files for offline playback
- Implement background sync from SQL Server when connection is available
- [x] Implement background sync from sqlite asp server when connection is available
- [x] Handle conflict resolution (server wins / last-write-wins policy)

---

### 🔗 QR Code Direct Play (PoC)

- [x] Implement QR code scanning in-app
- [x] Link QR code to specific POI audio (bypass GPS requirement)
- Target use case: bus stops in **Khánh Hội, Xóm Chiều, Vĩnh Hội** (Hồ Chí Minh City)

---

## II. CMS / Admin Web Dashboard

### 🛠️ Core Admin Features (MVP)

- [x] **POI Management**
  - [x] Create / Edit / Delete POI
  - [x] Set coordinates, radius, priority
- [x] **Audio Management**
  - [x] Upload pre-recorded audio files
  - [x] Manage TTS scripts
- [x] **Translation Management**
  - [x] Add/edit multilingual content per POI
- [x] **Tour Management**
  - [x] Group POIs into tours
  - [x] Set tour order / sequence
- [x] **Usage History**
  - [x] View playback logs per POI

---

### 📊 Data Analytics (MVP)

- [x] Save anonymous user route history
- [x] Report: Top POIs by audio play count
- [x] Report: Average listening time per POI
- [x] Build **heatmap** of user positions
- [x] Dashboard charts & filters (date range, tour, ward)

---

## III. Architecture Tasks

### 🏗️ Backend / Server

- Set up **SQL Server** (online data source)
- [x] Set up **ASP.NET Core Server** (online data source)
- [x] Design schema: POIs, audio files, tours, playback history, translations
- [] Build REST API endpoints for:
  - [x] POI CRUD
  - [x] Audio file upload/download
  - [x] Sync endpoint for mobile client
  - [x] Analytics ingestion
- [x] Implement anonymous telemetry collection (route history, heatmap data)

---

### 📐 Recommended Architecture Layers

| Layer | Responsibility | Tech |
| --- |--- | --- |
| **GPS & Location** | Track position, foreground/background | FusedLocationProvider / CLLocationManager |
| **Geofence Engine** | Detect POI entry, trigger events | Native API / Haversine |
| **Narration Engine** | Choose TTS or audio, queue management, anti-repeat | Android TTS / AVSpeech / Azure |
| **Content Layer** | POI data offline + server sync | SQLite + SQL Server |
| **UI/UX Layer** | Map, POI list, settings | .NET MAUI + Figma AI |
| **CMS / Backend** | Admin dashboard, analytics | Web (to be decided) |

---

## IV. Extended Features (MVP, Post-PoC)

- [x] Settings screen:
  - [x] GPS / radius sensitivity tuning
  - [x] Choose TTS voice
  - [x] Download offline language pack
- [x] Multi-language UI (Vietnamese default)
- [x] Notification integration (auto-stop audio on incoming call/notification)
- [x] POI owner web portal (non-admin role, manages own POI content)
- [x] App distribution via QR code (install link)

---

## x Milestone Summary

| Phase | Deliverable |
| --- | --- |
| **PoC** | GPS tracking, geofence trigger, audio playback, offline POI data, QR scan |
| **MVP** | CMS dashboard, analytics, tour management, multilingual TTS, offline sync |
| **Extended** | Heatmap, route history, voice pack download, POI owner portal |
