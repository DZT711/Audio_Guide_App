# 📋 Project Task List — Audio Tour POI App

---

## I. Mobile App (Client)

### 🔧 Setup & Infrastructure

- [ ] Generate QR code for app installation link (deep link / store link)
- [✅] Configure app for online/offline mode detection
  - [ ] Online → connect to SQL Server
  - [✅] Online → connect to sqlite asp server
  - [✅] Offline → use local SQLite database
- [✅] Set up `.NET MAUI` project (Android + iOS targets)
- [ ] Configure dependency injection and platform services

---

### 📍 GPS Tracking (PoC)

- [ ] Implement foreground GPS tracking
- [ ] Implement background GPS tracking
  - Android: `FusedLocationProviderClient` via Foreground Service
  - iOS: `CLLocationManager` with `Always` permission
- [✅] Optimize for battery efficiency (adaptive polling interval)
- [] Improve location accuracy as much as possible
- [ ] Handle location permission requests (foreground & background)

---

### 🗺️ Geofencing & Auto Audio Playback (PoC)

- [✅] Define POI data model:
  - [✅] Coordinate (Longitude, Latitude)
  - [✅] Activation Radius
  - [✅] Priority level
- [ ] Implement geofence engine:
  - [ ] Native geofencing API (Android / iOS region monitoring), **OR**
  - [ ] Manual distance calculation using **Haversine formula**
- [ ] Trigger audio playback when user **enters** a POI radius
- [ ] Trigger audio playback when user **stands near** a POI point
- [ ] Implement **debounce / cooldown** mechanism to prevent audio spam

---

### 🔊 Narration Engine (PoC)

- [✅] Implement **Text-to-Speech (TTS)**:
  - [✅] Android: Android TTS engine
  - [ ] iOS: `AVSpeechSynthesizer`
  <!-- - [ ] Optional: Azure Cognitive Services (natural voice, online/offline notice) -->
  - [ ] Auto-detect device locale → default Vietnamese if region is VN
  - [✅] Support multiple languages (flexible, low memory footprint)
- [✅] Implement **Recorded Audio playback**:
  - [✅] Professional/natural voice quality
  - Note: higher memory usage vs TTS
- [ ] Build **Audio Queue Manager**:
  - [ ] Multi-process scheduling
  - [ ] No audio repetition logic
  - [ ] Auto-stop / pause when OS notification arrives
- [✅] Decide per-POI: use TTS script or pre-recorded audio file

---

### 🗂️ POI Management (PoC)

- [✅] Build POI list screen:
  - [✅] Place name & description
  - [✅] Illustration / image of the place
  - [✅] Link to map
  - [✅] Audio file or TTS script field
- [✅] Suggest Figma AI for UI/UX design of POI screens

---

### 🗺️ Map View (PoC)

- [✅] Display current user location on map
- [✅] Show all POIs on map
- [✅] Highlight nearest POI
- [✅] Choose map library: Leaflet-OpenStreetMap
  <!-- - [ ] `Microsoft.Maui.Controls.Maps` (basic)
  - [ ] Google Maps SDK / MapKit binding (advanced)
  - [ ] Mapbox or HERE SDK (offline map caching) -->

---

### 📦 Offline Data & Sync (PoC)

- [✅] Set up **SQLite** database (via EF Core or sqlite-net)
- [✅] Store POI data locally for offline use
- [✅] Pre-load audio files for offline playback
- [] Implement background sync from SQL Server when connection is available
- [✅] Implement background sync from sqlite asp server when connection is available
- [✅] Handle conflict resolution (server wins / last-write-wins policy)

---

### 🔗 QR Code Direct Play (PoC)

- [ ] Implement QR code scanning in-app
- [ ] Link QR code to specific POI audio (bypass GPS requirement)
- [ ] Target use case: bus stops in **Khánh Hội, Xóm Chiều, Vĩnh Hội** (Hồ Chí Minh City)

---

## II. CMS / Admin Web Dashboard

### 🛠️ Core Admin Features (MVP)

- [✅] **POI Management**
  - [✅] Create / Edit / Delete POI
  - [✅] Set coordinates, radius, priority
- [✅] **Audio Management**
  - [✅] Upload pre-recorded audio files
  - [✅] Manage TTS scripts
- [✅] **Translation Management**
  - [✅] Add/edit multilingual content per POI
- [✅] **Tour Management**
  - [✅] Group POIs into tours
  - [✅] Set tour order / sequence
- [✅] **Usage History**
  - [✅] View playback logs per POI

---

### 📊 Data Analytics (MVP)

- [ ] Save anonymous user route history
- [ ] Report: Top POIs by audio play count
- [ ] Report: Average listening time per POI
- [ ] Build **heatmap** of user positions
- [ ] Dashboard charts & filters (date range, tour, ward)

---

## III. Architecture Tasks

### 🏗️ Backend / Server

- [ ] Set up **SQL Server** (online data source)
- [✅] Design schema: POIs, audio files, tours, playback history, translations
- [] Build REST API endpoints for:
  - [✅] POI CRUD
  - [✅] Audio file upload/download
  - [✅] Sync endpoint for mobile client
  - [ ] Analytics ingestion
- [ ] Implement anonymous telemetry collection (route history, heatmap data)

---

### 📐 Recommended Architecture Layers

| Layer | Responsibility | Tech |
|---|---|---|
| **GPS & Location** | Track position, foreground/background | FusedLocationProvider / CLLocationManager |
| **Geofence Engine** | Detect POI entry, trigger events | Native API / Haversine |
| **Narration Engine** | Choose TTS or audio, queue management, anti-repeat | Android TTS / AVSpeech / Azure |
| **Content Layer** | POI data offline + server sync | SQLite + SQL Server |
| **UI/UX Layer** | Map, POI list, settings | .NET MAUI + Figma AI |
| **CMS / Backend** | Admin dashboard, analytics | Web (to be decided) |

---

## IV. Extended Features (MVP, Post-PoC)

- [✅] Settings screen:
  - [✅] GPS / radius sensitivity tuning
  - [✅] Choose TTS voice
  - [✅] Download offline language pack
- [✅] Multi-language UI (Vietnamese default)
- [ ] Notification integration (auto-stop audio on incoming call/notification)
- [✅] POI owner web portal (non-admin role, manages own POI content)
- [ ] App distribution via QR code (install link)

---

## ✅ Milestone Summary

| Phase | Deliverable |
|---|---|
| **PoC** | GPS tracking, geofence trigger, audio playback, offline POI data, QR scan |
| **MVP** | CMS dashboard, analytics, tour management, multilingual TTS, offline sync |
| **Extended** | Heatmap, route history, voice pack download, POI owner portal |
