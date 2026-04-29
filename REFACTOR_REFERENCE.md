# QR Landing Page Refactoring - Reference & Examples

## POI CATEGORY THEME MAPPING TABLE

| Category ID | Category Name | Theme Class | Emoji | Primary Color | Secondary Color | Description |
|-------------|---|---|---|---|---|---|
| 1 | Hotpot | `theme-hotpot` | 🍲 | `#d94430` | `#8b3923` | Warm fire tones for hot pot restaurants |
| 2 | Snail Stalls | `theme-snail` | 🐌 | `#2d5016` | `#1a3d0a` | Coastal/street tones |
| 3 | Pubs/Beer | `theme-pub` | 🍺 | `#3d2817` | `#1a1410` | Dark night/neon tones |
| 4 | Bubble Tea/Desserts | `theme-dessert` | 🧋 | `#f596aa` | `#d95e8a` | Pastel pink tones |
| 5 | Fast Food | `theme-fastfood` | 🍔 | `#ff6b35` | `#ff4500` | Pop art bright orange |
| 6 | Junk Food Vendor | `theme-junkfood` | 🌭 | `#ffd700` | `#ffed4e` | Electric street gold |
| 7 | Vendor Dinner Food | `theme-dinner` | 🍲 | `#8b4513` | `#5c2d0b` | Candlelight evening brown |
| 8 | Coffee Shop | `theme-coffee` | ☕ | `#6f4e37` | `#3e2723` | Espresso artisan brown |
| 9 | Smoothie Takeaway | `theme-smoothie` | 🥤 | `#ff69b4` | `#ff1493` | Tropical burst pink |
| 10 | Buffet Restaurant | `theme-buffet` | 🍽️ | `#ffd700` | `#ffa500` | Grand feast gold |
| 11 | Food Court | `theme-foodcourt` | 🏪 | `#4a7c59` | `#2d5a3d` | Urban hub green |
| 12 | Canteen | `theme-canteen` | 🍱 | `#87ceeb` | `#4da6d6` | Clean & functional sky blue |
| 13 | Supermarket | `theme-supermarket` | 🛒 | `#0066cc` | `#0052a3` | Retail fresh blue |
| 14 | Mini Mart | `theme-minimart` | 🏬 | `#ff6b9d` | `#ff4757` | Corner store warmth |
| 15 | Bakery | `theme-bakery` | 🍰 | `#f4a261` | `#e76f51` | Golden crust orange |
| 16 | Eatery / Rice Shop | `theme-eatery` | 🍚 | `#daa520` | `#cd853f` | Homestyle goldenrod |
| 17 | Seafood Restaurant | `theme-seafood` | 🦐 | `#20b2aa` | `#0e7490` | Deep ocean teal |
| 18 | Noodle Shop | `theme-noodle` | 🍜 | `#c1440e` | `#8b2e0b` | Steamy umami red |
| 19 | Pho Shop | `theme-pho` | 🍲 | `#a0522d` | `#704214` | Vietnamese heritage sienna |
| 20 | Sticky Rice Stall | `theme-stickyrice` | 🌾 | `#ffa500` | `#ff8c00` | Dawn market orange |
| 21 | Cafeteria | `theme-cafeteria` | 🥗 | `#90ee90` | `#3cb371` | Mint industrial green |

---

## DATABASE SEEDING EXAMPLE

### C# Code to Add to CategorySeeding:

```csharp
public static void SeedCategories(DBContext context)
{
    var categories = new[]
    {
        new Category 
        { 
            CategoryId = 1, 
            Name = "Hotpot", 
            Description = "Hot pot restaurants and lẩu",
            ThemeName = "hotpot",
            PrimaryColor = "#d94430",
            SecondaryColor = "#8b3923",
            IconEmoji = "🍲",
            Status = 1 
        },
        new Category 
        { 
            CategoryId = 2, 
            Name = "Snail Stalls", 
            Description = "Quán ốc - Street snail vendors",
            ThemeName = "snail",
            PrimaryColor = "#2d5016",
            SecondaryColor = "#1a3d0a",
            IconEmoji = "🐌",
            Status = 1 
        },
        new Category 
        { 
            CategoryId = 3, 
            Name = "Pubs & Beer", 
            Description = "Quán nhậu - Vietnamese beer halls",
            ThemeName = "pub",
            PrimaryColor = "#3d2817",
            SecondaryColor = "#1a1410",
            IconEmoji = "🍺",
            Status = 1 
        },
        // ... repeat for all 21 categories
    };

    foreach (var category in categories)
    {
        var existingCategory = context.Categories.FirstOrDefault(c => c.CategoryId == category.CategoryId);
        if (existingCategory == null)
        {
            context.Categories.Add(category);
        }
        else
        {
            // Update existing category with theme data
            existingCategory.ThemeName = category.ThemeName;
            existingCategory.PrimaryColor = category.PrimaryColor;
            existingCategory.SecondaryColor = category.SecondaryColor;
            existingCategory.IconEmoji = category.IconEmoji;
        }
    }

    context.SaveChanges();
}
```

---

## POI_DATA STRUCTURE EXAMPLE

### JSON Structure Injected into Page:

```json
{
  "locationId": 5,
  "locationName": "Khanh Hoi Canal Viewpoint",
  "categoryName": "Scenic Spot",
  "categoryIcon": "🌅",
  "categoryTheme": "hotpot",
  "address": "Khanh Hoi, District 1, Ho Chi Minh City",
  "openingHours": "5:00 AM - 9:00 PM",
  "phoneContact": "+84 28 1234 5678",
  "websiteUrl": "https://khanhhoicanal.com",
  "establishedYear": 2015,
  "ownerName": "Mr. Nguyen Van A",
  "ownerVerified": true,
  "images": [
    {
      "url": "https://api.smarttourism.local/api/images/loc-5-hero.jpg",
      "alt": "Khanh Hoi Canal - Main viewpoint",
      "title": "Image 1 of 4"
    },
    {
      "url": "https://api.smarttourism.local/api/images/loc-5-gallery-1.jpg",
      "alt": "Sunset at Khanh Hoi",
      "title": "Image 2 of 4"
    }
  ],
  "audio": {
    "id": 17,
    "title": "Welcome to Khanh Hoi Canal - Vietnamese Narration",
    "durationSeconds": 240,
    "language": "vi-VN",
    "autoplay": true
  },
  "analytics": {
    "visitCount": 1250,
    "visitCountRecent": 85,
    "audioPlayCount": 320,
    "lastUpdated": "2026-04-15T10:30:00Z"
  },
  "links": {
    "deepLink": "smarttour://play/location/5?autoplay=true&audioTrackId=17",
    "downloadPage": "/LocationQr/public/download?locationId=5&locationName=Khanh%20Hoi%20Canal%20Viewpoint&openUrl=smarttour%3A%2F%2Fplay%2Flocation%2F5%3Fautoplay%3Dtrue%26audioTrackId%3D17&autoplay=true&audioTrackId=17",
    "apkUrl": "/LocationQr/public/android-apk"
  },
  "funFact": "This canal was named after a wealthy merchant from the 18th century",
  "tip": "Best visited at sunset for stunning photos"
}
```

---

## THEME CSS VARIABLES EXAMPLE

### Colors to Be Set Dynamically Per Theme:

```css
/* HOTPOT THEME (Red/Warm) */
body.theme-hotpot {
  --primary: #d94430;
  --primary-dark: #8b3923;
  --primary-soft: #f8d7d3;
  --text-primary: #1a1a1a;
  --text-secondary: #666;
  --border-color: rgba(217, 68, 48, 0.2);
  --accent: #d94430;
  --accent-light: #ff6b5b;
}

/* COFFEE THEME (Brown/Dark) */
body.theme-coffee {
  --primary: #6f4e37;
  --primary-dark: #3e2723;
  --primary-soft: #f4ede4;
  --text-primary: #2c1810;
  --text-secondary: #704214;
  --border-color: rgba(111, 78, 55, 0.2);
  --accent: #8b6f47;
  --accent-light: #a0826d;
}

/* SEAFOOD THEME (Teal/Ocean) */
body.theme-seafood {
  --primary: #20b2aa;
  --primary-dark: #0e7490;
  --primary-soft: #ccf2f4;
  --text-primary: #0a2e36;
  --text-secondary: #17626c;
  --border-color: rgba(32, 178, 170, 0.2);
  --accent: #20b2aa;
  --accent-light: #48bfb9;
}

/* Pattern: All themes follow this structure */
```

---

## RESPONSIVE BREAKPOINTS

### Mobile (< 1024px):
- Single column layout
- Hero image: full width
- Side columns: hidden (accessible via FAB)
- Bottom sheet for additional info
- Touch-friendly buttons: min 44px height

### Tablet (768px - 1023px):
- Still single column
- Slightly larger text
- More padding
- Full-width gallery

### Desktop (1024px - 1399px):
- 3-column layout active
- Left sidebar: Heritage info
- Center: Main content
- Right sidebar: Quick actions
- Gallery in 2-column grid

### Wide Desktop (≥ 1400px):
- More breathing room
- Slightly larger fonts
- Gallery in 3-column grid
- Enhanced spacing

---

## JAVASCRIPT INTEGRATION GUIDE

### Functions That Must Be Preserved:

1. **Theme System:**
   ```javascript
   function setTheme(theme) {
     document.body.className = `theme-${theme}`;
     localStorage.setItem('activeTheme', theme);
     updateSideColumns(theme);
   }
   ```

2. **Audio Player:**
   ```javascript
   function togglePlay() {
     playing = !playing;
     // Update UI and waveform animation
     updateWaveform();
   }
   
   function buildWaveform() {
     // Generate animated bars for audio visualization
   }
   ```

3. **Gallery Carousel:**
   ```javascript
   function goSlide(n) {
     currentSlide = (n + SLIDE_COUNT) % SLIDE_COUNT;
     updateGallery();
   }
   ```

4. **Scroll Reveal:**
   ```javascript
   function initReveal() {
     // Trigger animations as elements scroll into view
   }
   ```

---

## SECURITY CONSIDERATIONS

### URL Encoding Rules:

```csharp
// ✅ CORRECT
var encodedUrl = Uri.EscapeDataString("smarttour://play/location/5?autoplay=true");
var downloadUrl = $"/LocationQr/public/download?openUrl={encodedUrl}";

// ❌ WRONG (will break)
var downloadUrl = $"/LocationQr/public/download?openUrl={directUrl}";
```

### HTML Encoding Rules:

```csharp
// ✅ CORRECT
var safeName = WebUtility.HtmlEncode(location.Name);
var html = $"<h1>{safeName}</h1>";

// ❌ WRONG (XSS vulnerability)
var html = $"<h1>{location.Name}</h1>";
```

### JSON Serialization Rules:

```csharp
// ✅ CORRECT
var json = JsonSerializer.Serialize(poiData);
var script = $"<script>var POI_DATA = {json};</script>";

// ❌ WRONG (will break)
var script = $"<script>var POI_DATA = {poiData.ToString()};</script>";
```

---

## TESTING SCENARIOS

### Scenario 1: Complete POI with All Data
**Input:** Location with all fields populated
**Expected:** Page renders with all sections visible
**Test URL:** `/LocationQr/public/location/5?autoplay=true&audioTrackId=17`

### Scenario 2: Minimal POI (Required Fields Only)
**Input:** Location with only name, category, status
**Expected:** Page renders with default/placeholder data for missing fields
**Test URL:** Create test location with minimal data

### Scenario 3: Different Category Themes
**Input:** Repeat scenario 1 for 5 different categories (hotpot, coffee, seafood, pho, noodle)
**Expected:** Each page displays with correct theme colors and CSS
**Test URLs:** `/LocationQr/public/location/1`, `/LocationQr/public/location/8`, etc.

### Scenario 4: Mobile Responsive
**Input:** Any valid location on mobile browser (375px width)
**Expected:** Single column layout, bottom sheet for sidebar, touch-friendly
**Test:** Use browser DevTools mobile simulator

### Scenario 5: Missing Images
**Input:** Location with no images
**Expected:** Placeholder image shown, gallery hidden
**Test:** Create test location without images

### Scenario 6: Missing Audio
**Input:** Location with no audio content
**Expected:** Audio player hidden/disabled
**Test:** Create test location without audio

### Scenario 7: Special Characters in Name
**Input:** Location name with quotes, ampersands, emojis
**Expected:** Properly HTML-encoded, no XSS
**Test URL:** Create location with `Name = "Café & Co.'s 🍰 Shop"`

---

## DEPLOYMENT VALIDATION CHECKLIST

### Pre-Deployment:
- [ ] All 21 categories have unique ThemeName values
- [ ] All colors are valid hex format (#RRGGBB)
- [ ] Database migration applies cleanly
- [ ] No compilation errors
- [ ] All unit tests pass

### Post-Deployment:
- [ ] Test each category theme renders correctly
- [ ] Verify deep-link URL format
- [ ] Verify download page URL is complete
- [ ] Test on 3+ browsers
- [ ] Test on 3+ mobile devices
- [ ] Check browser console for errors
- [ ] Verify no data leaks in HTML/CSS
- [ ] Confirm performance (< 3s load)

---

## COMMON PITFALLS TO AVOID

❌ **Don't:**
- Hardcode theme names in multiple places
- Use `location.Name` directly without encoding
- Mix string interpolation and concatenation for URLs
- Forget to import required using statements
- Leave commented debug code
- Use inline styles when CSS variables exist
- Forget media queries for responsive design
- Skip null checks on optional fields
- Copy/paste CSS without verifying selectors
- Forget to bind POI_DATA before referencing in JS

✅ **Do:**
- Use single source of truth for theme names
- HTML encode all user input
- Use `Uri.EscapeDataString()` for URLs
- Add proper using directives
- Remove debug code before commit
- Use CSS custom properties (--variable-name)
- Test at multiple breakpoints
- Always provide default values
- Verify CSS selector specificity
- Initialize POI_DATA before any JS function calls

---

## ROLLBACK PLAN (If Issues Occur)

**If critical bugs found:**
1. Revert to previous version of LocationQrService.cs
2. Keep Category model changes (backward compatible)
3. Remove theme-related HTML/CSS
4. Restore simple blue theme template
5. Re-deploy simpler version
6. Fix issues in staging
7. Test thoroughly before re-deploying

**Time estimate to rollback:** < 5 minutes

---

## SUCCESS CRITERIA

✅ All requirements met when:
1. Page renders correctly for all 21 POI categories
2. Theme CSS applies dynamically based on category
3. All live data from server displays correctly
4. Responsive design works on mobile/tablet/desktop
5. Audio player, gallery, animations all functional
6. Deep-link and download page URLs work
7. No console errors or warnings
8. No visual regressions from sample design
9. All existing API logic preserved
10. Zero breaking changes to other components
