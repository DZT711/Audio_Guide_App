# 📝 Project Setup Guide

Complete step-by-step guide for setting up Smart Tourism MAUI development environment.

---

## Table of Contents

1. [System Requirements](#system-requirements)
2. [Development Tools Setup](#development-tools-setup)
3. [Repository Setup](#repository-setup)
4. [Project Configuration](#project-configuration)
5. [Database Setup](#database-setup)
6. [Running Each Component](#running-each-component)
7. [Troubleshooting](#troubleshooting)

---

## System Requirements

### Minimum Specs

- **OS:** Windows 10/11 or macOS 12+
- **RAM:** 8 GB (16 GB recommended for simultaneous builds)
- **Storage:** 50 GB free (for SDKs, NuGet packages, builds)
- **Processor:** Intel i5/Ryzen 5 or better

### Required Software

- **.NET 10 SDK** or later ([download](https://dotnet.microsoft.com/download/dotnet))
- **Visual Studio 2022 Community/Pro** (or VS Code + .NET CLI)
- **Git** 2.25+
- **Android SDK** (for mobile development)

---

## Development Tools Setup

### 1. Install .NET 8 SDK

**Windows:**
```powershell
# Check if .NET 8 is installed
dotnet --version

# If not, download from: https://dotnet.microsoft.com/download/dotnet/8.0
```

**Verify Installation:**
```bash
dotnet --version
# Should output: 8.x.x (or later)

dotnet workload list
# Verify MAUI workload is available
```

### 2. Install Visual Studio 2022

**Windows:**

1. Download [Visual Studio 2022 Community](https://visualstudio.microsoft.com/downloads/)

2. Run installer, select workloads:

   - ✅ `.NET MAUI`
   - ✅ `ASP.NET Core development`
   - ✅ `Desktop development with C++` (for Android NDK)
   - ✅ `Mobile development with .NET`

**VS Code Alternative:**

```bash
# Install .NET CLI tools
dotnet workload install maui
dotnet workload install android
```

### 3. Install Android SDK

**Windows with Visual Studio:**

- Already included in ".NET MAUI" workload
- Location: `%LOCALAPPDATA%\Android\sdk`

**Manual Installation (VS Code):**

```bash
dotnet workload install android
# Installs to: ~/.dotnet/android

# Verify
%LOCALAPPDATA%\Android\sdk\emulator\emulator -version
```

**Set Environment Variables:**

```powershell
# PowerShell (Admin)
[Environment]::SetEnvironmentVariable("ANDROID_HOME", "$env:LOCALAPPDATA\Android\sdk", "User")
[Environment]::SetEnvironmentVariable("Path", "$env:Path;$env:LOCALAPPDATA\Android\sdk\platform-tools", "User")
```

### 4. Setup Android Emulator

```bash
# List available emulator images
%LOCALAPPDATA%\Android\sdk\tools\bin\sdkmanager --list

# Create new emulator (API 33, Pixel 5)
%LOCALAPPDATA%\Android\sdk\tools\bin\avdmanager create avd -n "Pixel5-API33" -k "system-images;android-33;default;x86_64" -d "pixel_5"

# Start emulator
%LOCALAPPDATA%\Android\sdk\emulator\emulator -avd Pixel5-API33
```

### 5. Install Git

**Windows:**
- Download [Git for Windows](https://git-scm.com/download/win)
- Install with default options

**Verify:**
```bash
git --version
# Should output: git version 2.x.x
```

---

## Repository Setup

### 1. Clone Repository

```bash
# Clone with HTTPS
git clone https://github.com/DZT711/Smart-Tourism-MAUI.git
cd Smart-Tourism-MAUI

# Or with SSH (if configured)
git clone git@github.com:DZT711/Smart-Tourism-MAUI.git
cd Smart-Tourism-MAUI

# Verify remote
git remote -v
```

### 2. Create Local Branch

```bash
# Check out development branch
git checkout Mobile_AppPerformance

# Or create local feature branch
git checkout -b feature/your-feature-name
```

### 3. Restore NuGet Packages

```bash
# Restore all projects
dotnet restore Smart-Tourism-MAUI.sln

# Or per project
cd MauiApp_Mobile && dotnet restore
cd ../WebApplication_API && dotnet restore
cd ../BlazorApp_AdminWeb && dotnet restore
```

---

## Project Configuration

### 1. Update appsettings.json

**Backend API** (`WebApplication_API/appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SmartTourism_Dev;User Id=sa;Password=YourPassword123;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Jwt": {
    "Key": "your-secret-key-32-characters-long-minimum",
    "Issuer": "http://localhost:5000",
    "Audience": "http://localhost:5000"
  }
}
```

**Admin Web** (`BlazorApp_AdminWeb/appsettings.json`):
```json
{
  "ApiBaseUrl": "http://localhost:5000",
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### 2. Verify Mobile Configuration

**MauiApp_Mobile/MauiProgram.cs** - Check API base URL:
```csharp
// Default configuration
var apiBaseUrl = "http://10.0.2.2:5000"; // Android emulator to host
// Or
var apiBaseUrl = "http://192.168.1.x:5000"; // Your machine IP
```

---

## Database Setup

### Option 1: SQL Server (Production)

**Prerequisites:**
- SQL Server 2019+ installed
- SQL Server Management Studio (optional)

**Setup:**
```bash
# Navigate to docs folder
cd docs/DatabaseStructure

# Create database and schema
sqlcmd -S localhost\SQLEXPRESS -U sa -P YourPassword123 -i database.sql

# Load sample data (optional)
sqlcmd -S localhost\SQLEXPRESS -U sa -P YourPassword123 -i sample-data.sql
```

### Option 2: SQLite (Local/Mobile)

**Mobile Database:**
- Automatically created on first app launch
- Location: `/data/data/com.companyname.mauiapp_mobile/files/smarttour_mobile.db3`

**Local Testing Database:**
```bash
# Copy template database
cp docs/DatabaseStructure/smarttour-mobile.db3 ./smarttour-dev.db3

# Access with SQLite CLI or DB Browser
# https://sqlitebrowser.org/
```

---

## Running Each Component

### 1. Backend API (ASP.NET Core)

**Terminal 1 — Start API:**
```bash
cd WebApplication_API
dotnet run

# Output should show:
# info: Microsoft.AspNetCore.Hosting.Hosting[14]
#       Now listening on: http://localhost:5000
```

**Test API:**
```bash
# In new terminal
curl http://localhost:5000/api/pois

# Or access Swagger UI
# http://localhost:5000/swagger
```

### 2. Admin Dashboard (Blazor)

**Terminal 2 — Start Admin Web:**
```bash
cd BlazorApp_AdminWeb
dotnet run

# Output should show:
# info: Microsoft.AspNetCore.Hosting.Hosting[14]
#       Now listening on: http://localhost:7000
```

**Access Dashboard:**
- Open browser: http://localhost:7000
- Login with test credentials (see setup docs)

### 3. Mobile App (Android)

**Terminal 3 — Build & Run:**
```bash
cd MauiApp_Mobile

# Build for Android
dotnet build -f net8.0-android -c Debug

# Run on emulator
dotnet run -f net8.0-android

# Or deploy to device
dotnet run -f net8.0-android --no-build
```

**Or use VS 2022:**
1. Open `Smart-Tourism-MAUI.sln`
2. Set `MauiApp_Mobile` as startup project
3. Select Android emulator from dropdown
4. Press `F5` to start debugging

### 4. All Components Together

**Recommended Terminal Setup:**
```
Terminal 1: WebApplication_API (Backend)
Terminal 2: BlazorApp_AdminWeb (Admin Web)
Terminal 3: MauiApp_Mobile (Mobile App)
Terminal 4: Git commands / utilities
```

**Or use Visual Studio:**
- Configure multiple startup projects in solution
- Right-click solution → Properties → Startup Project
- Select "Multiple startup projects"

---

## Common Development Tasks

### Build Specific Project

```bash
# Mobile (Android)
dotnet build MauiApp_Mobile -f net8.0-android -c Debug

# Backend API
dotnet build WebApplication_API -c Debug

# Admin Web
dotnet build BlazorApp_AdminWeb -c Debug

# All projects
dotnet build Smart-Tourism-MAUI.sln -c Debug
```

### Clean Build

```bash
dotnet clean Smart-Tourism-MAUI.sln
dotnet restore Smart-Tourism-MAUI.sln
dotnet build Smart-Tourism-MAUI.sln -c Debug
```

### Run Tests (if available)

```bash
dotnet test Smart-Tourism-MAUI.sln
```

### Debug Mobile App

**VS 2022:**
1. Set breakpoints in code
2. Start debugging (`F5`)
3. App will pause at breakpoints
4. Use Debug panel to step through code

**VS Code:**
```bash
# Requires C# extension
# Set breakpoints in code
# Run with debug
dotnet run -f net8.0-android --configuration Debug
```

### Database Migrations (EF Core)

```bash
# List pending migrations
dotnet ef migrations list

# Apply migrations
dotnet ef database update

# Create new migration
dotnet ef migrations add MigrationName

# Remove last migration
dotnet ef migrations remove
```

---

## Troubleshooting

### NuGet Package Restore Fails

**Error:** `Unable to load the service index for source`

**Solution:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore again
dotnet restore Smart-Tourism-MAUI.sln
```

### Android Build Fails

**Error:** `Platform version Android 33 (API 33) not found`

**Solution:**
```bash
# Install missing Android workload
dotnet workload install android

# Or reinstall
dotnet workload restore
```

### Port Already in Use

**Error:** `System.Net.Sockets.SocketException: Only one usage of each socket address`

**Solution:**
```powershell
# Find process using port 5000
netstat -ano | findstr :5000

# Kill process
taskkill /PID <PID> /F

# Or use different port in launchSettings.json
```

### Database Connection Fails

**Error:** `Cannot connect to database server`

**Solution:**
```bash
# Verify SQL Server is running
net start MSSQLSERVER

# Or check connection string in appsettings.json
# Test with SQL Management Studio first
```

### App Won't Install on Android Emulator

**Error:** `INSTALL_FAILED_VERSION_DOWNGRADE`

**Solution:**
```bash
# Uninstall app from emulator first
adb uninstall com.companyname.mauiapp_mobile

# Then reinstall
dotnet run -f net8.0-android --no-build
```

### GPS Not Working in Emulator

**Solution:**
```bash
# GPS requires Google Play Services
# Use Google APIs emulator image (not standard)
# Or set fake location in settings
```

---

## Verification Checklist

After completing setup, verify everything works:

- [ ] .NET 8 SDK installed (`dotnet --version`)
- [ ] Android SDK installed (`%ANDROID_HOME%` set)
- [ ] Repository cloned and remotes configured
- [ ] NuGet packages restored (`dotnet restore`)
- [ ] Solution builds successfully (`dotnet build`)
- [ ] Backend API runs (`dotnet run` in WebApplication_API)
- [ ] Admin Web loads (`http://localhost:7000`)
- [ ] Mobile app builds for Android (`dotnet build -f net8.0-android`)
- [ ] Database created (SQL Server or SQLite)
- [ ] Git branch configured (`git branch -vv`)

---

## Next Steps

After setup is complete:

1. **Read Documentation:**
   - [README.md](../README.md) - Project overview
   - [specification.md](../docs/specification.md) - Feature spec
   - [STRUCTURE.md](./STRUCTURE.md) - Code organization

2. **Start Developing:**
   - Create feature branch
   - Make changes
   - Commit and push
   - Create Pull Request

3. **Test Locally:**
   - Run all three components
   - Test mobile app on emulator
   - Test API endpoints
   - Test admin web dashboard

4. **Debug Issues:**
   - Check [Troubleshooting](#troubleshooting) section
   - Review project logs
   - Ask team for help

---

**Questions?** Create a GitHub issue or contact the team.

