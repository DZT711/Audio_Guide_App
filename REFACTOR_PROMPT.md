# QR Landing Page Refactoring - Detailed Step-by-Step Prompt

## Overview
Refactor the LocationQrService to generate adaptive themed landing pages that match the web sample design, with proper POI category mapping and live server data integration.

---

## PHASE 1: POI CATEGORY DATA EXTENSION

### Requirement 1.1 - Define All POI Categories with Theme Mapping
**Location:** `WebApplication_API/Data/` (create or extend CategorySeeding)

**POI Categories to Support (from web sample):**
```
1. HOTPOT (Lẩu) — theme-hotpot
2. SNAIL STALLS (Quán ốc) — theme-snail
3. PUBS/BEER (Quán nhậu) — theme-pub
4. BUBBLE TEA / DESSERTS (Trà sữa) — theme-dessert
5. FAST FOOD — theme-fastfood
6. JUNK FOOD VENDOR — theme-junkfood
7. VENDOR DINNER FOOD — theme-dinner
8. COFFEE SHOP — theme-coffee
9. SMOOTHIE TAKEAWAY — theme-smoothie
10. BUFFET RESTAURANT — theme-buffet
11. FOOD COURT — theme-foodcourt
12. CANTEEN — theme-canteen
13. SUPERMARKET — theme-supermarket
14. MINI MART — theme-minimart
15. BAKERY — theme-bakery
16. EATERY / RICE SHOP — theme-eatery
17. SEAFOOD RESTAURANT — theme-seafood
18. NOODLE SHOP — theme-noodle
19. PHO SHOP — theme-pho
20. STICKY RICE STALL — theme-stickyrice
21. CAFETERIA — theme-cafeteria
```

**Data Model Extension:**
- Add `ThemeName` field to Category model (e.g., "hotpot", "coffee", "pho")
- Add `IconEmoji` field to Category model (e.g., "🍲", "☕", "🍜")
- Add `PrimaryColor` field to Category model (hex, e.g., "#d94430" for hotpot)
- Add `SecondaryColor` field to Category model (hex, e.g., "#8b3923")

**No Breaking Changes:**
- Do NOT modify existing CategoryId values
- Do NOT change database migrations unnecessarily
- Only add new optional columns with defaults

**Validation Rules:**
- ThemeName must be one of the 21 allowed values
- PrimaryColor and SecondaryColor must be valid hex colors
- Each category must have a unique ThemeName
- Ensure backward compatibility - default to "hotpot" theme if ThemeName is null

---

## PHASE 2: QR LANDING PAGE REFACTORING

### Requirement 2.1 - Analyze Current Structure
**Current Files to Review:**
- `LocationQrService.cs` → `RenderLocationLandingPage()` method
- `LocationQrController.cs` → `OpenLocationLanding()` endpoint
- Sample web page: `smart-tourism-qr-landing.html`

**Current State:**
- Basic HTML with light blue theme (hardcoded)
- No theme switching based on category
- Minimal data display
- No adaptive responsive design for different categories

**Target State:**
- Full theme support based on POI category
- All required data fields dynamically populated
- Responsive adaptive layout
- Deep-link detection and app installation handling

### Requirement 2.2 - Refactor RenderLocationLandingPage() Method
**File:** `WebApplication_API/Services/LocationQrService.cs`

**Input Parameters (keep existing):**
```csharp
public string RenderLocationLandingPage(
    HttpContext httpContext,
    Location location,           // Must include Category
    Audio? defaultAudio,
    LocationQrGenerateRequest? request = null,
    LocationLandingInsights? insights = null)
```

**Data Requirements from Location object:**
- `location.LocationId`
- `location.Name`
- `location.Category.ThemeName` → **NEW** (maps to CSS theme class)
- `location.Category.PrimaryColor` → **NEW**
- `location.Category.SecondaryColor` → **NEW**
- `location.Category.IconEmoji` → **NEW**
- `location.Category.Name` (category display name)
- `location.Address`
- `location.Images` (multiple, for gallery)
- `location.AudioContents` (for audio player)
- `location.Owner?.FullName` (creator/owner name)
- `location.EstablishedYear` (founded year)
- `location.WebURL` (if available)
- `location.PhoneContact` (if available)

**Data Requirements from insights (if provided):**
- `insights.CategoryName`
- `insights.OpeningHours`
- `insights.ImageUrl` (hero image)
- `insights.VisitCountAllTime`
- `insights.VisitCountLast7Days`
- `insights.AudioPlayCount`
- `insights.LastUpdatedUtc`

**Critical Business Logic (DO NOT CHANGE):**
- Deep-link URL generation: `smarttour://play/location/{locationId}?autoplay={autoplay}&audioTrackId={audioTrackId}`
- Download page URL: `LocationQr/public/download?locationId={id}&locationName={name}&openUrl={encodedDeepLink}&autoplay={autoplay}&audioTrackId={audioTrackId}`
- Android APK URL handling (redirects to latest APK)
- Fallback delay logic (minimum 600ms)
- HTML encoding for security

---

## PHASE 3: HTML TEMPLATE STRUCTURE

### Requirement 3.1 - HTML Template Architecture
**Base Template Structure (MUST preserve all sections):**

```html
<!DOCTYPE html>
<html lang="vi">
<head>
  <!-- Meta tags, charset, viewport -->
  <!-- Google Fonts link (existing) -->
  <!-- Inline CSS with theme classes -->
</head>
<body class="theme-{CATEGORY_THEME}">
  <!-- Theme Switcher (debug only - keep for testing) -->
  <div id="theme-switcher"><!-- ... --></div>
  
  <!-- Watermark Layer -->
  <div id="vn-watermark" aria-hidden="true"></div>
  
  <!-- Main 3-Column Layout -->
  <div id="page-wrapper">
    <!-- LEFT SIDEBAR: Heritage & Soul -->
    <aside class="side-col" id="left-column">
      <!-- Legacy year, owner badge, fun facts -->
    </aside>
    
    <!-- CENTER COLUMN: Main Content -->
    <div id="center-col">
      <!-- Hero Section with Dynamic Image -->
      <!-- Audio Player with Waveform -->
      <!-- POI Description & Details -->
      <!-- Image Gallery with Carousel -->
      <!-- Nested POI List (Related Locations) -->
      <!-- App Download CTA -->
      <!-- Footer with Stats -->
    </div>
    
    <!-- RIGHT SIDEBAR: Smart Actions -->
    <aside class="side-col" id="right-column">
      <!-- Quick action buttons -->
      <!-- Social proof badges -->
      <!-- Next stop recommendations -->
    </aside>
  </div>
  
  <!-- Inline JavaScript for interactivity -->
  <script>
    var POI_DATA = {
      /* All dynamic data from server */
    };
    // ... functions
  </script>
</body>
</html>
```

### Requirement 3.2 - CSS Theme System
**Theme Variables (must be customizable per category):**

For each theme, define in CSS:
```css
/* Example: theme-hotpot */
body.theme-hotpot {
  --primary: #d94430;           /* Main brand color */
  --primary-dark: #8b3923;      /* Darker variant */
  --primary-soft: #f8d7d3;      /* Light background */
  --accent: #d94430;
  --text-primary: #1a1a1a;
  --text-secondary: #666;
  --bg-light: #fff8f7;
}
```

**CSS Scope Rules (DO NOT break these):**
- All theme variables scoped under `body.theme-{name}`
- Keep existing utility classes untouched
- Add theme-specific overrides only where necessary
- Media queries for responsive (mobile, tablet, desktop)

---

## PHASE 4: DATA INTEGRATION & BINDING

### Requirement 4.1 - Server-Side Data Preparation
**In RenderLocationLandingPage() method:**

1. **Extract Category Theme:**
   ```csharp
   var themeName = location.Category?.ThemeName ?? "hotpot";
   var primaryColor = location.Category?.PrimaryColor ?? "#d94430";
   var secondaryColor = location.Category?.SecondaryColor ?? "#8b3923";
   var categoryIcon = location.Category?.IconEmoji ?? "🍽️";
   ```

2. **Build POI_DATA JavaScript object:**
   ```json
   {
     "locationId": 5,
     "locationName": "Khanh Hoi Canal Viewpoint",
     "categoryName": "Scenic Spot",
     "categoryIcon": "🌅",
     "address": "Khanh Hoi, District 1",
     "openingHours": "5:00 AM - 9:00 PM",
     "establishedYear": 2015,
     "phoneContact": "+84 28 1234 5678",
     "websiteUrl": "https://example.com",
     "ownerName": "Smart Tourism",
     "ownerVerified": true,
     "images": [
       { "url": "...", "alt": "Main view", "title": "Title" }
     ],
     "audio": {
       "id": 17,
       "title": "Location Narration",
       "durationSeconds": 240,
       "autoplay": true
     },
     "analytics": {
       "visitCount": 1250,
       "visitCountRecent": 85,
       "audioPlayCount": 320
     },
     "links": {
       "deepLink": "smarttour://play/location/5?autoplay=true&audioTrackId=17",
       "downloadPage": "/LocationQr/public/download?locationId=5&...",
       "apkUrl": "/LocationQr/public/android-apk"
     }
   }
   ```

3. **Encode all URLs properly:**
   - Use `Uri.EscapeDataString()` for URL parameters
   - Use `WebUtility.HtmlEncode()` for HTML content
   - Use `JsonSerializer.Serialize()` for JSON-embedded strings

### Requirement 4.2 - Template Variable Injection
**Variables to inject into HTML template:**

```csharp
var locationName = WebUtility.HtmlEncode(location.Name);
var themeName = location.Category?.ThemeName ?? "hotpot";
var categoryIcon = location.Category?.IconEmoji ?? "🍽️";
var primaryColor = location.Category?.PrimaryColor ?? "#d94430";
var secondaryColor = location.Category?.SecondaryColor ?? "#8b3923";
var address = WebUtility.HtmlEncode(location.Address ?? "Address not provided");
var heroImageUrl = ResolveLocationHeroImageUrl(httpContext, location, insights?.ImageUrl);
var ownerName = location.Owner?.FullName ?? location.Owner?.Username ?? "Unknown";
var establishedYear = location.EstablishedYear.HasValue ? location.EstablishedYear.Value.ToString() : "Year not available";
var phoneContact = WebUtility.HtmlEncode(location.PhoneContact ?? "Not provided");
var websiteUrl = WebUtility.HtmlEncode(location.WebURL ?? "");

// Deep links
var deepLinkUrl = links.DeepLinkUrl;  // From BuildLocationLinks()
var downloadPageUrl = links.DownloadPageUrl;
var androidApkUrl = links.AndroidApkUrl;

// Analytics
var visitCountAll = insights?.VisitCountAllTime ?? 0;
var visitCountRecent = insights?.VisitCountLast7Days ?? 0;
var audioPlayCount = insights?.AudioPlayCount ?? 0;
```

---

## PHASE 5: BUILD STEP-BY-STEP IMPLEMENTATION

### Step 5.1 - Prepare Category Data Model Extension
**What to do:**
1. Extend `Category` model to include `ThemeName`, `PrimaryColor`, `SecondaryColor`, `IconEmoji`
2. Create/run migration (add columns as nullable with defaults)
3. Seed database with all 21 category records with theme mappings
4. Update CategorySeeding class to include theme data

**Testing:**
- Verify all categories exist in database
- Verify theme name matches CSS class names exactly
- Verify colors are valid hex format

**Files to Modify:**
- `WebApplication_API/Model/Category.cs` (add properties)
- `WebApplication_API/Data/DBContext.cs` (modelBuilder configuration if needed)
- `WebApplication_API/Data/DbInitializer.cs` or seeding logic

---

### Step 5.2 - Refactor LocationQrService.RenderLocationLandingPage()
**What to do:**
1. Read category theme information from `location.Category`
2. Extract all required fields from location, images, audio, owner
3. Build complete POI_DATA object with all fields
4. Replace hardcoded blue theme with dynamic theme class: `class="theme-{themeName}"`
5. Inject all dynamic data into template using string interpolation
6. Add CSS theme variables for primary/secondary colors

**Key Code Section:**
```csharp
var themeName = location.Category?.ThemeName ?? "hotpot";
var primaryColor = location.Category?.PrimaryColor ?? "#d94430";
var categoryIcon = location.Category?.IconEmoji ?? "🍽️";

// Build POI_DATA object
var poiData = new {
    locationId = location.LocationId,
    locationName = locationName,
    categoryName = location.Category?.Name ?? "Uncategorized",
    categoryIcon = categoryIcon,
    // ... all other fields
};

return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
  <style>
    :root {
      --primary: {{primaryColor}};
      --primary-dark: {{secondaryColor}};
      /* other theme vars */
    }
    /* Include complete CSS from sample */
  </style>
</head>
<body class="theme-{{themeName}}">
  <!-- Full HTML structure from sample -->
  <script>
    var POI_DATA = {{JsonSerializer.Serialize(poiData)}};
    // ... all JavaScript functions
  </script>
</body>
</html>
""";
```

**Testing:**
- Render page for each category
- Verify theme class is applied correctly
- Verify POI_DATA contains all required fields
- Test with missing data (nulls should have defaults)

---

### Step 5.3 - Preserve and Copy Complete CSS & JavaScript
**What to do:**
1. Copy all CSS from `smart-tourism-qr-landing.html` sample
2. Ensure all 21 theme definitions are included
3. Copy all JavaScript functions (data access, theme switching, player, gallery, etc.)
4. Modify JS to use injected POI_DATA instead of hardcoded values
5. Add minimal CSS overrides for dynamic colors only

**CSS Sections to Include:**
- Root variables (:root)
- All 21 theme definitions (body.theme-{name})
- Utility classes (reveal, animations, etc.)
- Hero section styles
- Audio player styles
- Gallery carousel styles
- Footer styles
- Responsive media queries
- All pseudo-elements and animations

**JavaScript Functions to Include:**
- `setTheme(theme)` - Apply theme dynamically
- `updateSideColumns(theme)` - Update side panel data
- `switchLang(lang)` - Language toggle
- `buildWaveform()` - Audio waveform visualization
- `togglePlay()` - Audio player control
- `toggleMute()` - Mute/unmute
- `goSlide(n)` - Gallery carousel
- `buildDots()` - Gallery indicators
- `initReveal()` - Scroll reveal animations

**Testing:**
- Verify all CSS rules apply correctly
- Verify all JavaScript functions execute without errors
- Test theme switching (if debug switcher enabled)
- Test audio player, gallery, scroll animations

---

### Step 5.4 - Handle Deep-Link & Download Page Parameters
**What to do:**
1. Ensure `links.DeepLinkUrl` is properly generated in BuildLocationLinks()
2. Ensure `links.DownloadPageUrl` includes all required query parameters
3. Add fallback link if user has app installed → Open in app directly
4. Add button logic: "Open in App" if app detected, otherwise "Get App"

**URL Format Example:**
```
Deep Link: smarttour://play/location/5?autoplay=true&audioTrackId=17
Download Page: /LocationQr/public/download?locationId=5&locationName=Khanh%20Hoi&openUrl=smarttour%3A%2F%2Fplay%2Flocation%2F5%3Fautoplay%3Dtrue%26audioTrackId%3D17&autoplay=true&audioTrackId=17
```

**JavaScript to Add:**
```javascript
function tryOpenDeepLink() {
  const deepLink = POI_DATA.links.deepLink;
  setTimeout(() => {
    window.location.href = deepLink;
  }, 500);
  // If app not installed, fallback to download page after 2s
  setTimeout(() => {
    window.location.href = POI_DATA.links.downloadPage;
  }, 2000);
}
```

**Testing:**
- Verify deep-link format is correct
- Verify URL parameters are properly encoded
- Test fallback to download page if app not installed

---

### Step 5.5 - Gallery & Image Handling
**What to do:**
1. Extract `location.Images` collection (if available)
2. Build image gallery with carousel functionality
3. Use first image as hero, rest in gallery
4. Provide fallback placeholder if no images
5. Encode image URLs and alt text properly

**Code to Add:**
```csharp
var galleryImages = location.Images?
    .OrderBy(x => x.SortOrder)
    .Select(x => new {
        url = WebUtility.HtmlEncode(x.ImageUrl),
        alt = WebUtility.HtmlEncode(x.Description ?? location.Name),
        title = $"Image {location.Images.IndexOf(x) + 1}"
    })
    .ToList() ?? new();
```

**Testing:**
- Test with multiple images (4+)
- Test with single image
- Test with no images (placeholder shown)
- Test carousel navigation arrows

---

### Step 5.6 - Audio Player Integration
**What to do:**
1. Extract `location.AudioContents` → preferably the default or first audio
2. Generate waveform visualization
3. Implement play/pause/progress/mute controls
4. Show audio title, duration, language
5. Add autoplay logic if `request.Autoplay == true`

**Code to Add:**
```csharp
var audioTrack = defaultAudio ?? location.AudioContents?.FirstOrDefault();
var audioData = audioTrack != null ? new {
    id = audioTrack.AudioId,
    title = WebUtility.HtmlEncode(audioTrack.Title),
    durationSeconds = audioTrack.DurationSeconds ?? 0,
    language = audioTrack.LanguageCode ?? "vi-VN"
} : null;
```

**Testing:**
- Test audio player displays correct metadata
- Test play/pause controls work
- Test progress bar interaction
- Test autoplay trigger on page load (if enabled)
- Test mute button

---

### Step 5.7 - Validation & Error Handling
**What to do:**
1. Add null-coalescing operators for all optional fields
2. Provide meaningful defaults for missing data
3. Test with incomplete location data
4. Ensure no JavaScript errors in console
5. Add try-catch for JSON serialization

**Validation Checklist:**
- ✅ Category exists → default to "hotpot" if missing
- ✅ Images exist → show placeholder if empty
- ✅ Audio exists → hide player if null
- ✅ Opening hours exist → show "Hours not available"
- ✅ Phone contact → show "Contact not available"
- ✅ Website URL → hide link if empty
- ✅ Owner name → show "Smart Tourism" if missing
- ✅ Theme name → validate it's one of 21 allowed values

**Testing:**
- Create test Location with minimal required fields only
- Verify page still renders correctly
- Check browser console for JavaScript errors
- Test with special characters in location names

---

### Step 5.8 - CSS Media Queries & Responsive Design
**What to do:**
1. Ensure existing media queries remain functional
2. Mobile first: single column layout (< 1024px)
3. Tablet/Desktop: 3-column layout (≥ 1024px)
4. Wide Desktop: breathing room (≥ 1400px)
5. Test on multiple screen sizes

**Breakpoints to Verify:**
- Mobile: 320px - 767px
- Tablet: 768px - 1023px
- Desktop: 1024px - 1399px
- Wide: 1400px+

**Testing:**
- Mobile device (375px width)
- Tablet (768px width)
- Desktop (1024px, 1400px)
- Verify layout reflows correctly
- Verify touch targets are sufficient size

---

## PHASE 6: QUALITY ASSURANCE

### Requirement 6.1 - No Side Effects
**Rules to follow:**
- DO NOT modify `LocationQrController.cs` logic
- DO NOT change `BuildLocationLinks()` method
- DO NOT modify database schema beyond adding theme fields
- DO NOT change authentication/authorization logic
- DO NOT modify other service methods

**Files that MUST NOT be modified:**
- `WebApplication_API/Controller/LocationQrController.cs` (keep endpoints unchanged)
- `WebApplication_API/Data/DBContext.cs` (minimal, only add theme properties)
- `Project_SharedClassLibrary/Contracts/LocationQrStatusDto.cs` (if it exists)

---

### Requirement 6.2 - Testing Checklist

**Functional Tests:**
- [ ] Page renders for all 21 POI categories
- [ ] Theme CSS applies correctly per category
- [ ] All data fields display correctly
- [ ] Images gallery works with carousel
- [ ] Audio player displays and controls work
- [ ] Deep-link URL is correct format
- [ ] Download page URL includes all parameters
- [ ] Fallback links work if data is missing

**Visual Tests:**
- [ ] Layout is responsive (mobile, tablet, desktop)
- [ ] Colors match category theme
- [ ] Typography is readable
- [ ] Animations are smooth (reveal, player, carousel)
- [ ] No CSS conflicts or broken layouts
- [ ] All images load correctly

**Technical Tests:**
- [ ] No JavaScript errors in console
- [ ] HTML is valid (no unclosed tags)
- [ ] URL encoding is correct (special characters)
- [ ] JSON serialization is valid
- [ ] Performance: page load < 3 seconds
- [ ] Accessibility: basic ARIA labels present

**Security Tests:**
- [ ] All user input is HTML encoded
- [ ] All URLs are properly encoded
- [ ] No SQL injection vulnerabilities
- [ ] No XSS vulnerabilities
- [ ] JSON is safely serialized

---

### Requirement 6.3 - Browser Compatibility
**Test on:**
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile Chrome
- Mobile Safari

**Features to Test:**
- CSS Grid and Flexbox layouts
- CSS custom properties (variables)
- Canvas for waveform (if used)
- Fetch API (if used)
- LocalStorage (if needed)

---

## PHASE 7: DEPLOYMENT CHECKLIST

- [ ] All database migrations applied
- [ ] All 21 categories seeded with theme data
- [ ] LocationQrService.cs refactored with full HTML template
- [ ] RenderLocationLandingPage() returns adaptive themed HTML
- [ ] All tests pass (functional, visual, technical, security)
- [ ] No breaking changes to existing endpoints
- [ ] Sample test URLs work:
  - `/LocationQr/public/location/5?autoplay=true&audioTrackId=17`
  - `/LocationQr/public/download?locationId=5&locationName=Test&openUrl=...`
- [ ] Browser console clean (no errors or warnings)
- [ ] Performance acceptable (Lighthouse score > 80)

---

## REFERENCE: Expected Behavior Flow

```
User scans QR code (from LocationQrManager.razor)
         ↓
QR links to: /LocationQr/public/location/{locationId}
         ↓
LocationQrController.OpenLocationLanding()
         ↓
LocationQrService.RenderLocationLandingPage()
         ↓
Returns HTML with:
  - Theme class: theme-{categoryName}
  - POI_DATA object with all live data
  - Responsive layout (mobile/tablet/desktop)
  - Audio player (if audio available)
  - Image gallery (if images available)
         ↓
Browser renders page with category-specific theme
         ↓
User taps "Open in App" button (if installed)
  OR User taps "Get App" → redirects to download page
         ↓
Download page shows app installation QR code & deep link option
```

---

## SUMMARY OF CHANGES

| Component | Change Type | Details |
|-----------|------------|---------|
| Category Model | Extension | Add ThemeName, PrimaryColor, SecondaryColor, IconEmoji |
| Category Data | Seeding | Add all 21 POI categories with theme mappings |
| LocationQrService | Refactor | Completely refactor RenderLocationLandingPage() |
| HTML Template | Replacement | Replace simple template with full adaptive design |
| CSS | Addition | Include all 21 theme definitions + responsive styles |
| JavaScript | Addition | Include all interactive functions (player, gallery, etc.) |
| LocationQrController | No Change | Keep existing logic untouched |
| Database | Minimal | Add theme columns only |

---

**Total Impact: Medium** - Significant visual/UX changes, minimal logic changes, zero breaking changes to API.
