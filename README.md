# App Thuyết Minh Phố Ẩm Thực Vĩnh Khánh 
# (Audio Guide App for Vinh Khanh Food Street)

> **Document Type:** Product Requirements Document (PRD)

> **Version:** v0.1

> **Ngày / Date:** 2026

> **Trạng thái / Status:** In Development

---

## Mục Lục / Table of Contents

1. [Thành viên / Team Members](#1-thành-viên--team-members)
2. [Tổng quan dự án / Project Overview](#2-tổng-quan-dự-án--project-overview)
3. [Mục tiêu sản phẩm / Product Goals](#3-mục-tiêu-sản-phẩm--product-goals)
4. [Đối tượng người dùng / Target Users & User Stories](#4-đối-tượng-người-dùng--target-users--user-stories)
5. [Phạm vi tính năng / Feature Scope](#5-phạm-vi-tính-năng--feature-scope)
6. [Yêu cầu chức năng / Functional Requirements](#6-yêu-cầu-chức-năng--functional-requirements)
7. [Yêu cầu phi chức năng / Non-Functional Requirements](#7-yêu-cầu-phi-chức-năng--non-functional-requirements)
8. [Kiến trúc hệ thống / System Architecture](#8-kiến-trúc-hệ-thống--system-architecture)
9. [Công nghệ sử dụng / Technology Stack](#9-công-nghệ-sử-dụng--technology-stack)
10. [Mô hình dữ liệu / Data Models](#10-mô-hình-dữ-liệu--data-models)
11. [Thiết kế API / API Design](#11-thiết-kế-api--api-design)
12. [Phân quyền hệ thống / RBAC & Authorization](#12-phân-quyền-hệ-thống--rbac--authorization)
13. [Cấu trúc đồ án dự kiến / Planned Project Structure](#13-cấu-trúc-đồ-án-dự-kiến--planned-project-structure)
14. [Lộ trình phát triển / Development Milestones](#14-lộ-trình-phát-triển--development-milestones)
15. [Rủi ro & Giảm thiểu / Risks & Mitigations](#15-rủi-ro--giảm-thiểu--risks--mitigations)

---

## 1. Thành viên / Team Members

| # | Họ và tên / Full Name | MSSV / Student ID |
|---|---|---|
| 1 | Nguyễn Sĩ Huy | 3123411122 |
| 2 | Nguyễn Văn Cường | 3123411045 |

---

## 2. Tổng quan dự án / Project Overview

### 2.1 Bối cảnh / Context

**[VI]** Phố Vĩnh Khánh (Quận 4, TP.HCM) là một trong những tuyến phố ẩm thực đường phố nổi tiếng nhất Sài Gòn, thu hút hàng nghìn lượt khách tham quan mỗi ngày, bao gồm cả du khách quốc tế. Tuy nhiên, việc khám phá và tìm hiểu về các điểm ăn uống tại đây gặp nhiều khó khăn do rào cản ngôn ngữ và thiếu thông tin có cấu trúc.

**[EN]** Vinh Khanh Street (District 4, Ho Chi Minh City) is one of Saigon's most iconic street food destinations, attracting thousands of visitors daily — including international tourists. However, exploring and learning about the food stalls is challenging due to language barriers and the lack of structured information.

### 2.2 Mô tả sản phẩm / Product Description

**[VI]** Xây dựng một hệ thống thuyết minh âm thanh đa ngôn ngữ cho phố ẩm thực Vĩnh Khánh, bao gồm:
- **Ứng dụng di động đa nền tảng (.NET MAUI):** Du khách sử dụng để nghe thuyết minh tự động khi đến gần các điểm ăn uống (dựa trên GPS + Geofencing), xem bản đồ tương tác và chi tiết các POI, hoạt động offline.
- **Trang web quản trị (Blazor):** Admin và chủ quán quản lý nội dung POI, audio, người dùng.
- **Backend API (ASP.NET Core):** Phục vụ dữ liệu POI, sinh âm thanh TTS, phân quyền RBAC, đồng bộ offline.

**[EN]** Build a multilingual audio commentary system for Vinh Khanh food street, comprising:
- **Cross-platform mobile app (.NET MAUI):** Tourists use to hear auto-triggered commentary when approaching food stalls (GPS + Geofencing), view an interactive map and POI details, and operate fully offline.
- **Admin web (Blazor):** Admins and shop owners manage POI content, audio, and users.
- **Backend API (ASP.NET Core):** Serves POI data, generates TTS audio, handles RBAC authorization, and manages offline synchronization.

### 2.3 Phát biểu vấn đề / Problem Statement

| Vấn đề / Problem | Giải pháp / Solution |
|---|---|
| Du khách không biết đặc trưng của từng quán | POI có mô tả chi tiết + ảnh + audio thuyết minh |
| Rào cản ngôn ngữ với khách quốc tế | Hỗ trợ đa ngôn ngữ: vi, en, zh, ja, ko |
| Mạng không ổn định trong hẻm nhỏ | Offline-first: SQLite + audio cache local |
| Không biết đang đứng gần quán nào | GPS + Geofencing tự động kích hoạt audio |
| Chủ quán khó cập nhật thông tin | Owner Portal trên web, duyệt bởi Admin |

---

## 3. Mục tiêu sản phẩm / Product Goals

### 3.1 Mục tiêu chính / Primary Goals

**[VI]**
- Cung cấp trải nghiệm thuyết minh âm thanh **tự động, không cần thao tác** khi du khách đi dạo phố.
- Hoạt động **hoàn toàn offline** sau khi đã đồng bộ lần đầu (không phụ thuộc vào mạng di động).
- Hỗ trợ tối thiểu **5 ngôn ngữ**: Tiếng Việt, Anh, Trung, Nhật, Hàn.
- Chi phí vận hành **$0** cho TTS và bản đồ (dùng Edge-TTS + PMTiles).

**[EN]**
- Deliver **hands-free, automatic audio commentary** as tourists walk the street.
- Operate **fully offline** after the initial sync (no mobile data dependency).
- Support at least **5 languages**: Vietnamese, English, Chinese, Japanese, Korean.
- **$0 operational cost** for TTS and map tiles (using Edge-TTS + PMTiles).

### 3.2 Chỉ số thành công / Success Metrics

| Chỉ số / Metric | Mục tiêu / Target |
|---|---|
| Số POI đã có audio | ≥ 30 địa điểm |
| Thời gian phát audio sau khi vào zone | ≤ 3 giây |
| Tỉ lệ phát audio thành công offline | 100% (sau sync) |
| Số ngôn ngữ hỗ trợ | 5 |
| Thời gian load màn hình chính | ≤ 2 giây |

---

## 4. Đối tượng người dùng / Target Users & User Stories

### 4.1 Personas

#### 👤 Du khách nội địa / Domestic Tourist
- **Mô tả:** Người dùng điện thoại Android/iOS, muốn khám phá đặc sản Quận 4, không rành địa bàn.
- **Nhu cầu:** Xem bản đồ, nghe giới thiệu món ăn, biết giờ mở cửa.

#### 🌏 Du khách quốc tế / International Tourist
- **Mô tả:** Người nước ngoài không đọc được tiếng Việt, đến đây theo gợi ý.
- **Nhu cầu:** Nghe audio bằng ngôn ngữ mẹ đẻ, xem ảnh, hiểu đặc trưng từng quán.

#### 🏪 Chủ quán / Shop Owner (POI Owner)
- **Mô tả:** Chủ các hàng ăn trên phố Vĩnh Khánh.
- **Nhu cầu:** Cập nhật thông tin quán, menu, ảnh trên web admin.

#### 🛡️ Quản trị viên / Administrator
- **Mô tả:** Người vận hành hệ thống, kiểm duyệt nội dung.
- **Nhu cầu:** CRUD toàn bộ POI, quản lý tài khoản, theo dõi hệ thống.

### 4.2 User Stories

**Du khách:**
- Là du khách, tôi muốn **tự động nghe giới thiệu** khi đến gần một quán ăn mà không cần nhấn nút.
- Là du khách quốc tế, tôi muốn **chọn ngôn ngữ** và nghe bằng tiếng mẹ đẻ của mình.
- Là du khách, tôi muốn **xem bản đồ** biết mình đang đứng ở đâu và quán nào xung quanh.
- Là du khách, tôi muốn **dùng app khi không có mạng** trong hẻm nhỏ.
- Là du khách, tôi muốn **xem chi tiết POI**: ảnh, giờ mở cửa, menu, mô tả.

**Chủ quán:**
- Là chủ quán, tôi muốn **đăng ký tài khoản** và được admin xét duyệt.
- Là chủ quán, tôi muốn **cập nhật thông tin quán** của mình mà không ảnh hưởng quán khác.
- Là chủ quán, tôi muốn **dùng AI** để cải thiện mô tả quán của mình (giới hạn 10 lần/ngày).

**Admin:**
- Là admin, tôi muốn **tạo/sửa/xóa POI** và tự động kích hoạt sinh audio.
- Là admin, tôi muốn **duyệt đăng ký** của chủ quán trước khi họ được phép chỉnh sửa.
- Là admin, tôi muốn **theo dõi tiến trình** sinh audio hàng loạt theo thời gian thực.

---

## 5. Phạm vi tính năng / Feature Scope

### 5.1 Trong phạm vi (MVP) / In Scope

| Tính năng / Feature | Nền tảng / Platform |
|---|---|
| Bản đồ tương tác hiển thị POI | Mobile |
| GPS + Geofencing tự động kích hoạt audio | Mobile |
| Phát audio thuyết minh đa ngôn ngữ | Mobile |
| Tải dữ liệu offline (SQLite + audio cache) | Mobile |
| Màn hình chi tiết POI (ảnh, mô tả, giờ mở cửa, menu) | Mobile |
| Chọn ngôn ngữ | Mobile |
| Đăng nhập Admin / Owner | Web |
| CRUD POI, Menu, Hình ảnh | Web |
| Quản lý tài khoản + phân quyền RBAC | Web |
| Sinh audio TTS tự động khi tạo POI | Backend |
| API RESTful đầy đủ | Backend |
| Đồng bộ offline từ server | Backend |

### 5.2 Ngoài phạm vi (Future) / Out of Scope

- Hệ thống thanh toán / booking tour
- Live chat / push notification
- Tích hợp mạng xã hội (đăng review, share)
- Server-side routing / geofencing

---

## 6. Yêu cầu chức năng / Functional Requirements

### 6.1 Module Mobile — Ứng dụng MAUI

#### FR-M01: Khởi động ứng dụng / App Startup

**[VI]** Khi mở app, hệ thống phải:
1. Đăng ký dịch vụ Location chạy nền (foreground service trên Android).
2. Hiển thị Splash Screen trong khi tải.
3. Nhắc người dùng chọn ngôn ngữ (lần đầu tiên).
4. Song song thực hiện: lấy tọa độ GPS (timeout 10s), tải POI từ SQLite (0ms), gọi API đồng bộ POI mới.
5. Cập nhật UI sau khi có dữ liệu (GPS + dữ liệu offline hiện trước, online cập nhật sau).

**[EN]** On app launch, the system must:
1. Register background location service (foreground service on Android).
2. Display Splash Screen during load.
3. Prompt language selection (first launch only).
4. In parallel: acquire GPS (10s timeout), load POIs from SQLite (instant), call API to sync new POIs.
5. Update UI progressively (offline data shows first, online data updates after).

#### FR-M02: Bản đồ tương tác / Interactive Map

- Hiển thị bản đồ khu vực phố Vĩnh Khánh với các marker POI.
- Đánh dấu vị trí người dùng (chấm xanh) cập nhật theo GPS (throttle 5s).
- Nhấn marker POI → mở POI Detail Page.
- Hỗ trợ zoom, pan, rotate bản đồ.
- **Chế độ offline:** Render bản đồ từ file PMTiles cache (không cần internet).
- **Chế độ online:** Render từ MapTiler API (dữ liệu toàn cầu).

#### FR-M03: GPS + Geofencing / Location-based Trigger

Luồng xử lý 4 giai đoạn:

| Giai đoạn | Mô tả |
|---|---|
| **Giai đoạn 1 — GPS Collection** | `watchPosition()` / `ILocationService` liên tục → throttle 5s → tránh tính toán liên tục. Cập nhật marker vị trí trên map. |
| **Giai đoạn 2 — Zone Detection** | `IGeofenceService.CheckGeofences(position)`: tính khoảng cách Haversine tới từng POI. ≤ `geofence_radius` (mặc định 30m) → thêm vào `pendingEntries`. Cooldown 5 phút sau mỗi lần trigger. |
| **Giai đoạn 3 — Audio Decision (Heartbeat 1s)** | `ReconcileLoop` mỗi 1s: xác nhận ENTER sau debounce 3s → chọn POI ưu tiên (audio_priority → khoảng cách) → gửi tới AudioService. |
| **Giai đoạn 4 — Audio Playback** | `IAudioService.PlayWithFallback(poi, lang)` → 4-Tier Hybrid (xem FR-M04). |

**Hằng số quan trọng:**
```
GPS Throttle          = 5s
Geofence Radius       = 30m (mặc định, có thể tuỳ chỉnh per POI)
Geofence Debounce     = 3s
Geofence Cooldown     = 5 phút
Heartbeat Interval    = 1s
```

#### FR-M04: Audio Thuyết Minh / Audio Narration — 4-Tier Hybrid

| Tier | Tên | Độ trễ | Điều kiện |
|---|---|---|---|
| **Tier 1** | Pre-generated Audio | 0ms | File MP3 đã cache trong SQLite/local storage |
| **Tier 1.5** | On-demand Translate + TTS | 2–5s | POI có nội dung nhưng chưa có audio cho ngôn ngữ này |
| **Tier 2** | Cloud TTS API | 3–8s | Cần sinh mới từ server, lưu cache sau |
| **Tier 3** | Device TTS (fallback) | 0ms | Offline hoàn toàn, không có file audio nào |

**Nguyên tắc:** Luôn ưu tiên Tier thấp hơn (độ trễ thấp hơn, chất lượng cao hơn). Tier 3 chỉ dùng khi offline hoàn toàn.

**Queue Audio:** Chỉ phát 1 audio tại một thời điểm (single-slot queue). Audio mới có priority cao hơn sẽ ngắt audio đang phát.

#### FR-M05: Màn hình Chi tiết POI / POI Detail Screen

Hiển thị đầy đủ:
- Carousel ảnh (tối đa 8 ảnh, swipe)
- Tên quán, loại ẩm thực, đặc trưng nổi bật
- Mô tả chi tiết (theo ngôn ngữ đã chọn)
- Giờ mở cửa, số điện thoại, giá tầm
- Nút phát/dừng audio thuyết minh
- Danh sách menu (tên món + giá)
- Khoảng cách từ vị trí hiện tại

#### FR-M06: Offline / Đồng bộ dữ liệu

- **SQLite:** Lưu trữ toàn bộ danh sách POI, nội dung đa ngôn ngữ, metadata.
- **Audio Cache:** Lưu file MP3 vào local storage, index trong SQLite (`audio_path`).
- **Đồng bộ:** Khi có mạng, so sánh `updated_at` → tải delta (chỉ POI thay đổi).
- **Hotset:** Khi mở app, dịch trước 10 POI gần nhất trong 1.5km (đợi GPS tối đa 2.5s).
- **Warmup:** Download toàn bộ audio pack cho ngôn ngữ đã chọn (tùy chọn, qua Settings).
- **Offline Map:** Download PMTiles file cho khu vực Quận 4 (bbox: 106.69–106.715, 10.745–10.765).

#### FR-M07: Màn hình Cài đặt / Settings Screen

- Chọn / thay đổi ngôn ngữ thuyết minh.
- Tải gói dữ liệu offline (POI + Audio + Bản đồ).
- Xem trạng thái dung lượng đã dùng.
- Bật/tắt tự động phát audio khi vào zone.

---

### 6.2 Module Admin Web — Blazor

#### FR-A01: Xác thực / Authentication

- Đăng nhập bằng username + password.
- JWT Access Token (30 phút) lưu trong httpOnly cookie.
- JWT Refresh Token (7 ngày) lưu trong httpOnly cookie.
- Dual-mode: Cookie (browser) + Bearer Header (API fallback).
- Tự động làm mới token khi hết hạn.

#### FR-A02: Quản lý POI

- **Xem danh sách** POI với filter (tên, loại, trạng thái) và phân trang.
- **Tạo POI mới:** Form nhập tên, mô tả (VI), tọa độ (map picker), ảnh, giờ mở cửa, geofence_radius, audio_priority.
- **Sửa POI:** Chỉnh sửa mọi trường, upload/xóa ảnh (tối đa 8 ảnh, ≤ 5MB mỗi ảnh).
- **Xóa POI:** Cascade xóa localizations + file ảnh/audio liên quan.
- **Toggle hiển thị:** Bật/tắt POI trên app mà không xóa.
- Sau khi tạo/sửa POI → **tự động kích hoạt background task** sinh audio cho 5 ngôn ngữ.

#### FR-A03: Quản lý Audio — Theo dõi thời gian thực

- Xem tiến trình sinh audio theo từng POI và ngôn ngữ.
- **Real-time progress** qua Server-Sent Events (SSE): progress bar + trạng thái (queued → running → completed/failed).
- Hành động: Pause / Resume / Cancel từng task.
- Tối đa 3 task TTS chạy song song (Semaphore = 3).
- Có thể tái sinh audio cho POI cụ thể nếu cần.

#### FR-A04: Quản lý Người dùng / Users & Roles

- CRUD tài khoản Admin và Owner.
- CRUD Roles: tạo role tùy chỉnh với permissions tùy chọn.
- Duyệt đăng ký Owner: Xem thông tin đăng ký, phê duyệt / từ chối.
- Xem Audit Log: lịch sử hành động của từng user.

#### FR-A05: Owner Portal

- **Đăng ký:** Owner điền form (tên, liên hệ, CCCD, thông tin quán) → trạng thái `pending`.
- **Sau khi Admin duyệt:** Owner đăng nhập, chỉnh sửa **chỉ quán của mình**.
- **Submit nội dung:** Gửi nội dung chỉnh sửa → `poi_submissions` → Admin duyệt → public.
- **AI Advisor:** Owner nhấn "Cải thiện mô tả" → Gemini / AI API → gợi ý mô tả mới (10 lần/ngày).
- **PII:** CCCD của Owner được mã hóa Fernet khi lưu DB, tự xóa sau 180 ngày.

---

### 6.3 Module Backend — ASP.NET Core API

#### FR-B01: Module Content (POI)

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/v1/poi` | GET | Danh sách tất cả POI (public) |
| `/api/v1/poi/load-all` | GET | Tải POI kèm localization theo `?lang=` |
| `/api/v1/poi/nearby` | GET | POI trong bán kính `?lat=&lng=&radius=` |
| `/api/v1/poi/{id}` | GET | Chi tiết 1 POI |
| `/api/v1/poi` | POST | Tạo POI mới (Admin) |
| `/api/v1/poi/{id}` | PUT | Cập nhật POI (Admin) |
| `/api/v1/poi/{id}` | DELETE | Xóa POI cascade (Admin) |
| `/api/v1/poi/{id}/images` | POST | Upload ảnh (tối đa 8, 5MB) |

**3-Tier Content Fallback** khi load:
1. **Tier 1:** Ngôn ngữ được yêu cầu (target lang)
2. **Tier 2:** English (fallback, đánh dấu `is_fallback=true`)
3. **Tier 3:** Tiếng Việt gốc (cuối cùng, `audio_url = null`)

#### FR-B02: Module Audio / TTS

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/v1/audio/tts` | POST | Sinh audio từ text, stream MP3 về client |
| `/api/v1/audio/pack-manifest` | GET | Manifest audio pack (hash SHA-256, URLs) |
| `/api/v1/audio/tasks/stream` | GET (SSE) | Real-time tiến trình sinh audio |
| `/api/v1/audio/tasks/{id}/pause` | POST | Tạm dừng task |
| `/api/v1/audio/tasks/{id}/resume` | POST | Tiếp tục task |
| `/api/v1/audio/tasks/{id}/cancel` | POST | Huỷ task |

**TTS Pipeline:**
```
Text gốc (VN) → deep-translator → Bản dịch → MD5 hash check
→ [Cache HIT] → Trả file có sẵn
→ [Cache MISS] → Edge-TTS synthesis → Lưu MP3 → Upsert DB → Trả file
```

**5 giọng đọc tốt nhất:**
- Tiếng Việt: `vi-VN-HoaiMyNeural`
- Tiếng Anh: `en-US-JennyNeural`
- Tiếng Trung: `zh-CN-XiaoxiaoNeural`
- Tiếng Nhật: `ja-JP-NanamiNeural`
- Tiếng Hàn: `ko-KR-SunHiNeural`

#### FR-B03: Module Localization

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/v1/localizations/prepare-hotset` | POST | Dịch + TTS trước cho top N POI gần nhất |
| `/api/v1/localizations/on-demand` | POST | Dịch tức thì 1 POI (rate limit 30/10 phút) |
| `/api/v1/localizations/warmup` | POST | Dịch toàn bộ corpus (background, không chặn) |

**Hằng số:**
```
HOTSET_MAX_POI_IDS     = 10
HOTSET_NEARBY_RADIUS   = 1500m
ON_DEMAND_RATE_LIMIT   = 30 req / 10 phút
```

#### FR-B04: Module Maps

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/v1/maps/offline-manifest` | GET | Manifest: bbox, checksums SHA-256, asset URLs |
| `/api/v1/maps/packs/{version}/{file}` | GET | Serve PMTiles (Range Requests) |
| `/api/v1/maps/styles/{path}` | GET | Style JSON + sprites |
| `/api/v1/maps/fonts/{fontstack}/{range}.pbf` | GET | Glyph PBFs |

**Bảo mật:** `resolve_safe_path(base, relative)` — chặn Path Traversal attack (`../../etc/passwd`).

#### FR-B05: Module Admin & Auth

- **Auth:** Login, Logout, Refresh Token, Change Password, Me (xem thông tin bản thân).
- **Users:** CRUD tài khoản Admin/Owner.
- **Roles:** CRUD roles động với permissions tùy chọn.
- **Owner Registrations:** Xem danh sách, duyệt / từ chối, PII encrypted.
- **POI Submissions:** Xem bài đăng chờ duyệt, phê duyệt / từ chối.
- **Audit Logs:** Xem nhật ký hành động (action, user_id, resource, timestamp).

#### FR-B06: Module AI Advisor

| Endpoint | Method | Mô tả |
|---|---|---|
| `/api/v1/ai/enhance-description` | POST | Dùng Gemini 2.0 Flash cải thiện mô tả POI |

**Ràng buộc:**
```
AI Rate Limit (Owner) = 10 lần/ngày (reset 0:00 hàng ngày)
AI Rate Limit (Admin) = Unlimited
AI Timeout           = 30 giây
Prompt rule          = KHÔNG bịa thông tin, CHỈ thêm tính từ tích cực, 200-300 từ
```

---

## 7. Yêu cầu phi chức năng / Non-Functional Requirements

### 7.1 Hiệu năng / Performance

| Yêu cầu | Mục tiêu |
|---|---|
| Thời gian phát audio sau trigger geofence | ≤ 3 giây (Tier 1, từ cache) |
| Thời gian load danh sách POI offline | ≤ 500ms |
| Thời gian phản hồi API (p95) | ≤ 500ms |
| Cập nhật marker GPS trên map | Mỗi 5 giây |
| Số POI đồng thời hiển thị trên map | ≤ 500 (performance OK) |

### 7.2 Bảo mật / Security

- **XSS Protection:** JWT trong httpOnly cookie (JS không đọc được).
- **CSRF Protection:** SameSite=Lax cookie.
- **PII Encryption:** CCCD/thông tin nhạy cảm của Owner mã hóa Fernet, tự redact sau 180 ngày.
- **Path Traversal Guard:** Tất cả đường dẫn file phải resolve trong base directory.
- **Input Validation:** Kiểm tra tất cả input từ client (type, size, format).
- **Rate Limiting:** On-demand TTS và AI Advisor có giới hạn request.
- **RBAC:** Mọi endpoint Admin phải check permission trước khi xử lý.

### 7.3 Khả dụng Offline / Offline Availability

- App phải hoạt động **100% tính năng cốt lõi** (map, POI list, geofence, audio Tier 1) khi không có mạng.
- Dữ liệu offline được đồng bộ khi có mạng (background sync, không chặn UI).
- **Disk Quota:** Khi đầy bộ nhớ → tự động xóa cache ít dùng (audio cache cũ) để nhường chỗ.

### 7.4 Khả năng mở rộng / Extensibility

- Kiến trúc MVVM tách biệt rõ ràng giữa View / ViewModel / Model / Service.
- Dependency Injection toàn bộ (đăng ký trong `MauiProgram.cs`).
- Interface-driven: mọi Service đều có Interface (`IApiService`, `IGeofenceService`, v.v.) — dễ mock và unit test.
- Backend theo mô hình Modular Monolith: 6+ module độc lập, dễ tách thành microservice sau này.

### 7.5 Khả năng dùng được / Usability

- Ứng dụng mobile: UI rõ ràng, dễ dùng với 1 tay.
- Không cần đăng ký tài khoản để dùng tính năng du lịch (guest mode).
- Hỗ trợ đa nền tảng: Android (chính), iOS (phụ), Windows (desktop backup).
- Kích thước chữ tối thiểu 14sp, nút tối thiểu 44dp (WCAG).

---

## 8. Kiến trúc hệ thống / System Architecture

### 8.1 Tổng quan / Overview

```
┌───────────────────────────────────────────────────────────────┐
│                     CLIENT SIDE                               │
│                                                               │
│  ┌─────────────────────┐     ┌─────────────────────────────┐  │
│  │   MauiApp_Mobile    │     │    BlazorApp_AdminWeb       │  │
│  │  (.NET MAUI / C#)   │     │    (Blazor Server / C#)     │  │
│  │                     │     │                             │  │
│  │  MVVM Architecture  │     │   Razor Components          │  │
│  │  Views / ViewModels │     │   Admin + Owner Portal      │  │
│  │  Services (DI)      │     │                             │  │
│  └─────────┬───────────┘     └───────────┬─────────────────┘  │
│            │ REST API                    │ REST API           │
└────────────┼─────────────────────────────┼────────────────────┘
             │                             │
             ▼                             ▼
┌───────────────────────────────────────────────────────────────┐
│               WebApplication_API (ASP.NET Core / C#)          │
│                                                               │
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌───────┐ ┌─────────┐   │
│  │ content  │ │  audio   │ │ admin  │ │  loc. │ │  maps   │   │
│  │ (POI)    │ │ (TTS)    │ │(RBAC)  │ │(i18n) │ │(PMTiles)│   │
│  └──────────┘ └──────────┘ └────────┘ └───────┘ └─────────┘   │
│           ┌───────────┐  ┌───────────────┐                    │
│           │ ai_advisor│  │  geo/payment  │  (reserved)        │
│           └───────────┘  └───────────────┘                    │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │         Project_SharedClassLibrary (C# Class Library)   │  │
│  │         Models / DTOs / Enums / Helpers / Constants     │  │
│  └─────────────────────────────────────────────────────────┘  │
│                             │                                 │
│               ┌─────────────┼──────────────┐                  │
│               ▼             ▼              ▼                  │
│         ┌──────────┐  ┌──────────┐  ┌──────────────┐          │
│         │SQLLite   │  │ SQLite   │  │  File Storage│          │
│         │(Backend) │  │(Mobile   │  │  MP3 / Images│          │
│         │          │  │ Offline) │  │              │          │
│         └──────────┘  └──────────┘  └──────────────┘          │
└───────────────────────────────────────────────────────────────┘
```

### 8.2 Mô hình MVVM (MAUI Mobile) / MVVM Pattern

**[VI]** Ứng dụng MAUI tuân theo mô hình **MVVM (Model-View-ViewModel)**:

```
┌──────────┐   Data Binding   ┌─────────────┐   Calls   ┌──────────────────────┐
│   View   │ ←───────────→   │  ViewModel  │ ──────→   │   Service / Model    │
│  (XAML)  │                 │  (C# class) │           │  (IApiService, etc.) │
└──────────┘                 └─────────────┘           └──────────────────────┘
     ▲                              │
     │ Commands / Events            │ INotifyPropertyChanged
     └──────────────────────────────┘
```

- **View (XAML):** Chỉ chứa UI, không có logic. Binding tới ViewModel.
- **ViewModel:** Chứa logic UI, gọi Service. Implement `INotifyPropertyChanged` (thừa kế `BaseViewModel`).
- **Model:** Các entity thuần túy (từ `SharedClassLibrary`).
- **Service:** Xử lý nghiệp vụ thực sự (GPS, Audio, API, SQLite). Inject qua DI.

### 8.3 Luồng Startup Backend / Backend Startup Sequence

```
[1] Security Config Check
    → Kiểm tra JWT_SECRET, PII_ENCRYPTION_KEY đã đổi khỏi giá trị mặc định
    → Từ chối khởi động nếu không an toàn

[2] Database Connection
    → Kết nối SQL Server qua Entity Framework Core
    → Kiểm tra connectivity

[3] Data Seeding
    → ensure_roles() → tạo 4 default roles + Super Admin nếu chưa có
    → Seed dữ liệu POI mẫu (nếu DB trống)

[4] Indexing
    → Compound index {poi_id, lang} trên bảng LocalizedContent
    → Spatial index trên POI.Location cho $nearSphere query

[5] Mount Routers → Server Ready
    → content, audio, admin, owner, ai, localization, maps (prefix /api/v1)
```

---

## 9. Công nghệ sử dụng / Technology Stack

### 9.1 Tổng hợp Stack / Full Stack Summary

| Layer | Công nghệ | Phiên bản | Lý do chọn |
|---|---|---|---|
| **Mobile Framework** | .NET MAUI | .NET 10 | Cross-platform (Android, iOS, Windows, macOS) từ một codebase C# |
| **Mobile UI** | XAML + MAUI Controls | - | Native UI, MVVM-friendly data binding |
| **Mobile Map** | Microsoft.Maui.Controls.Maps / MapLibre | - | Hiển thị bản đồ tương tác, hỗ trợ custom marker |
| **Mobile Offline DB** | SQLite + sqlite-net-pcl | 1.9+ | Nhẹ, cross-platform, lưu POI và audio index |
| **Admin Web** | Blazor Server | .NET 10 | C# fullstack, Razor Components, real-time SignalR |
| **Backend API** | ASP.NET Core Web API | .NET 10 | REST API, CORS, Middleware pipeline |
| **Backend ORM** | Entity Framework Core | 8.x | Code-first, Migrations, LINQ |
| **Backend DB** | SQL Server / SQLite (dev) | - | Relational, tốt cho RBAC và Audit Log |
| **Shared Library** | C# Class Library | .NET 10 | Models, DTOs, Enums dùng chung |
| **TTS Engine** | Edge-TTS (Microsoft) | - | Miễn phí, chất lượng cao, 5 ngôn ngữ |
| **Translation** | deep-translator (GoogleTranslator) | - | Miễn phí, hỗ trợ đa ngôn ngữ |
| **AI** | Google Gemini 2.0 Flash | - | Cải thiện mô tả POI, rate limit 10/ngày |
| **Map Tiles (Online)** | MapTiler API | - | Bản đồ toàn cầu chất lượng cao |
| **Map Tiles (Offline)** | PMTiles | - | Single-file, binary seek, hoạt động local |
| **Auth** | JWT (httpOnly Cookie + Bearer) | - | Bảo mật XSS/CSRF |
| **PII Encryption** | AES-256 / Fernet (via .NET Cryptography) | - | Mã hóa CCCD, tự redact sau 180 ngày |
| **IDE** | Visual Studio 2022 / VS Code | - | MAUI, Blazor, ASP.NET Core support |
| **Version Control** | Git + GitHub | - | Quản lý source code, branching |

### 9.2 Architecture Pattern

| Pattern | Áp dụng tại | Mục đích |
|---|---|---|
| **MVVM** | MauiApp_Mobile | Tách biệt UI / Logic, testable ViewModel |
| **Dependency Injection** | Toàn bộ hệ thống | Loose coupling, dễ mock test |
| **Repository Pattern** | Backend API | Tách logic DB ra khỏi Controller |
| **Modular Monolith** | Backend API | 6 module độc lập, dễ mở rộng sau |
| **Offline-First** | Mobile | SQLite làm source of truth, sync khi online |
| **Dynamic RBAC** | Admin module | Static permissions (code) + Dynamic roles (DB) |
| **4-Tier Hybrid Audio** | Mobile + Backend | Pre-gen → On-demand → Cloud TTS → Device TTS |
| **3-Tier Content Fallback** | Backend content module | Target lang → English → Vietnamese |

---

## 10. Mô hình dữ liệu / Data Models

### 10.1 Backend Database (SQL Server)

#### Bảng `POIs`
```csharp
public class POI
{
    public Guid Id { get; set; }
    public string Name { get; set; }              // Tên quán (VI)
    public string Description { get; set; }       // Mô tả gốc (VI)
    public string FoodCategory { get; set; }      // Loại ẩm thực
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double GeofenceRadius { get; set; }    // metres, default = 30
    public int AudioPriority { get; set; }        // Ưu tiên phát audio (thấp = cao hơn)
    public string OpeningHours { get; set; }
    public string PhoneNumber { get; set; }
    public string PriceRange { get; set; }
    public bool IsActive { get; set; }
    public string AudioStatus { get; set; }       // "pending" | "processing" | "completed"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public ICollection<POIImage> Images { get; set; }
    public ICollection<LocalizedContent> Localizations { get; set; }
    public ICollection<MenuItem> MenuItems { get; set; }
}
```

#### Bảng `LocalizedContent`
```csharp
public class LocalizedContent
{
    public Guid Id { get; set; }
    public Guid POIId { get; set; }
    public string Language { get; set; }          // "vi" | "en" | "zh" | "ja" | "ko"
    public string Name { get; set; }
    public string Description { get; set; }
    public string AudioUrl { get; set; }          // Đường dẫn file MP3
    public bool IsFallback { get; set; }          // true = nội dung fallback từ ngôn ngữ khác
    public DateTime GeneratedAt { get; set; }
    
    // Index: (POIId, Language) UNIQUE
}
```

#### Bảng `AdminUsers`
```csharp
public class AdminUser
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }      // bcrypt
    public string RoleId { get; set; }
    public bool IsVerified { get; set; }
    public string EncryptedPII { get; set; }      // AES-256 encrypted CCCD (Owner only)
    public DateTime PIICreatedAt { get; set; }    // Để redact sau 180 ngày
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public Role Role { get; set; }
}
```

#### Bảng `Roles`
```csharp
public class Role
{
    public string Id { get; set; }                // "super_admin" | "admin" | "poi_owner" | "user"
    public string DisplayName { get; set; }
    public int Priority { get; set; }             // Thấp = cao hơn (super_admin = 0)
    public List<string> Permissions { get; set; } // JSON array
}
```

#### Bảng `AuditLogs`
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public string Action { get; set; }            // "create_poi" | "delete_user" | etc.
    public Guid UserId { get; set; }
    public string Resource { get; set; }          // "poi/{id}"
    public string Details { get; set; }           // JSON
    public DateTime Timestamp { get; set; }
}
```

#### Các bảng khác
```
POIImages           → Id, POIId, ImageUrl, Order, CreatedAt
MenuItems           → Id, POIId, Name, Price, Description, IsAvailable
POIOwnerRegs        → Id, UserId, BusinessName, CCCD (encrypted), Status (pending/approved/rejected)
POISubmissions      → Id, POIId, OwnerId, ProposedChanges (JSON), Status
AIUsageLimits       → Id, UserId, Date, Count
```

### 10.2 Mobile Database (SQLite)

```csharp
// Table: OfflinePOIs
[Table("OfflinePOIs")]
public class OfflinePOI
{
    [PrimaryKey] public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }       // Theo ngôn ngữ đã chọn
    public string Language { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double GeofenceRadius { get; set; }
    public int AudioPriority { get; set; }
    public string AudioPath { get; set; }          // Path file MP3 local
    public string AudioUrl { get; set; }           // URL server (fallback)
    public bool IsFallback { get; set; }
    public string ImagesJson { get; set; }         // JSON array of URLs
    public string OpeningHours { get; set; }
    public string PhoneNumber { get; set; }
    public string PriceRange { get; set; }
    public string MenuItemsJson { get; set; }      // JSON array of menu items
    public DateTime SyncedAt { get; set; }
}

// Table: AppSettings
[Table("AppSettings")]
public class AppSetting
{
    [PrimaryKey] public string Key { get; set; }
    public string Value { get; set; }
}
// Keys: "selected_lang", "last_sync_time", "offline_map_version", "offline_audio_version"
```

---

## 11. Thiết kế API / API Design

### 11.1 Quy tắc chung / General Rules

- Base URL: `https://[host]/api/v1`
- Format: `application/json`
- Authentication: `Authorization: Bearer {token}` hoặc httpOnly Cookie
- Error format:
```json
{
  "error": "NOT_FOUND",
  "message": "POI không tồn tại",
  "detail": null
}
```
- Pagination: `?page=1&per_page=20` → Response kèm `total`, `pages`

### 11.2 Nhóm Endpoint chính / Main Endpoint Groups

| Nhóm | Prefix | Auth |
|---|---|---|
| POI (Public) | `/api/v1/poi` | Không cần |
| Audio | `/api/v1/audio` | Một phần (SSE public) |
| Localization | `/api/v1/localizations` | Không cần (public read) |
| Maps | `/api/v1/maps` | Không cần |
| Admin | `/api/v1/admin` | Admin JWT |
| Owner | `/api/v1/owner` | Owner JWT |
| AI Advisor | `/api/v1/ai` | Admin/Owner JWT |

---

## 12. Phân quyền hệ thống / RBAC & Authorization

### 12.1 Domains & Permissions (29 permissions tổng)

| Domain | Permissions |
|---|---|
| `poi` | read, create, update, delete, approve, toggle |
| `menu` | read, create, update, delete |
| `user` | read, create, update, delete |
| `role` | read, create, update, delete |
| `analytics` | view, export, view_own |
| `audit` | read, manage |
| `system` | config, logs, backup |
| `owner` | register, access, submit_poi, manage_own_poi |
| `content` | moderate, publish |

### 12.2 Default Roles

| Role | Priority | Permissions |
|---|---|---|
| `super_admin` | 0 (cao nhất) | Tất cả 29 permissions |
| `admin` | 1 | POI, Menu, User, Role, Analytics, Audit, Content |
| `poi_owner` | 10 | poi:read + owner:* + menu:read/create/update + analytics:view_own |
| `user` | 100 | poi:read + menu:read + owner:register |

### 12.3 Cơ chế / Mechanism

- **Static permissions:** Định nghĩa trong code (enum), không thay đổi khi chạy.
- **Dynamic roles:** Lưu trong DB, Admin có thể tạo role mới với permissions tùy chọn.
- **JWT payload:** Chứa danh sách permissions → không cần query DB mỗi request.
- **Role cache TTL:** 300s — sau khi role thay đổi, token cũ vẫn valid tối đa 5 phút.
- **Route Guard:** Mỗi endpoint Admin annotate bằng `[RequirePermission("poi:delete")]`.

---

## 13. Cấu trúc đồ án dự kiến / Planned Project Structure

### MAUI

```
MauiApp_Mobile/
│
├── Models/                 # Model phục vụ riêng cho UI (nếu khác Shared Library)
│   └── ObservablePOI.cs    # Lớp POI có kế thừa ObservableObject để update giao diện
│
├── ViewModels/             # Hậu đài xử lý logic cho Giao diện
│   ├── BaseViewModel.cs    # Lớp cơ sở chứa INotifyPropertyChanged
│   ├── MainViewModel.cs    # Logic cho màn hình chính (Trạng thái GPS, POI gần nhất)
│   ├── MapViewModel.cs     # Logic xử lý bản đồ và hiển thị Icon
│   └── SettingsViewModel.cs# Logic chọn ngôn ngữ, tải gói offline
│
├── Views/                  # Giao diện XAML
│   ├── MainPage.xaml       # Màn hình điều hướng chính
│   ├── MapPage.xaml        # Màn hình bản đồ tương tác
│   ├── POIDetailPage.xaml  # Chi tiết điểm thuyết minh
│   └── SettingsPage.xaml   # Cấu hình ngôn ngữ và hệ thống
│
├── Services/               # Các "Bộ máy" cốt lõi (Cực kỳ quan trọng)
│   ├── IApiService.cs      # Gọi API từ WebApplication_API
│   ├── IDatabaseService.cs # Quản lý SQLite offline (Lưu POI, Audio path)
│   ├── IGeofenceService.cs # Bộ máy tính toán tọa độ & kích hoạt điểm
│   ├── IAudioService.cs    # Xử lý phát file .mp3 và TTS
│   └── ILocationService.cs # Xử lý Background/Foreground GPS
│
├── Helpers/                # Các tiện ích nhỏ
│   ├── AppConstants.cs     # Lưu Key, API URL, Bán kính mặc định
│   └── PermissionsHelper.cs# Kiểm tra và xin quyền GPS/Storage
│
├── Resources/              
│   ├── Raw/                # Chứa file âm thanh mặc định (.mp3)
│   └── Styles/             # Định nghĩa màu sắc, font chữ cho app
│
└── MauiProgram.cs          # Nơi đăng ký Dependency Injection (DI)
```

### Class Library

```
Project_SharedClassLibrary/
│
├── Models/                 # Các thực thể chính (Entities) khớp với Database
│   ├── POI.cs              # Điểm quan tâm (Point of Interest)
│   ├── AudioContent.cs     # Nội dung thuyết minh (đa ngôn ngữ)
│   ├── User.cs             # Thông tin người dùng/admin
│   └── Tour.cs             # Thông tin gói tour
│
├── DTOs/                   # Data Transfer Objects (Dùng để truyền tải qua API)
│   ├── Requests/           # Dữ liệu gửi lên (LoginRequest, CreatePOIDto...)
│   └── Responses/          # Dữ liệu trả về (POIDetailsDto, AuthResponse...)
│
├── Enums/                  # Các hằng số định nghĩa kiểu dữ liệu
│   ├── LanguageType.cs     # VN, EN, JP, KR...
│   ├── POIPriority.cs      # Low, Medium, High
│   └── AudioSourceType.cs  # TTS (Text) hay File (Mp3)
│
├── Helpers/                # Các công cụ tính toán dùng chung
│   └── GeoLocationHelper.cs# Công thức Haversine tính khoảng cách GPS
│
└── Constants/              # Các hằng số cố định
    └── ApiEndpoints.cs     # Lưu các đường dẫn API mẫu
```

## Cấu trúc các thành phần của đồ án

```
WebAppThuyetMinh/                              # Thư mục gốc của dự án
│
├── README.md                                  # File tài liệu hướng dẫn dự án
│
├── BlazorApp_AdminWeb/                        # Ứng dụng web admin sử dụng Blazor
│   ├── appsettings.Development.json           # Cấu hình ứng dụng cho môi trường phát triển
│   ├── appsettings.json                       # Cấu hình ứng dụng chung (database, logging, v.v.)
│   ├── BlazorApp_AdminWeb.csproj              # File cấu hình dự án Blazor Web
│   ├── Program.cs                             # File khởi tạo ứng dụng, đăng ký dịch vụ
│   ├── Components/                            # Thư mục chứa các component Razor
│   │   ├── _Imports.razor                     # File import các namespace dùng chung
│   │   ├── App.razor                          # Component gốc của ứng dụng
│   │   ├── Routes.razor                       # Cấu hình routing cho ứng dụng
│   │   ├── Layout/                            # Thư mục chứa layout
│   │   │   ├── MainLayout.razor               # Layout chính cho trang
│   │   │   ├── MainLayout.razor.css           # Style cho MainLayout
│   │   │   ├── NavMenu.razor                  # Component menu điều hướng
│   │   │   ├── NavMenu.razor.css              # Style cho NavMenu
│   │   │   ├── ReconnectModal.razor           # Component modal khi mất kết nối
│   │   │   ├── ReconnectModal.razor.css       # Style cho ReconnectModal
│   │   │   └── ReconnectModal.razor.js        # JavaScript xử lý logic ReconnectModal
│   │   ├── Pages/                             # Thư mục chứa các trang
│   │   │   ├── Counter.razor                  # Trang đếm số (ví dụ)
│   │   │   ├── Error.razor                    # Trang hiển thị lỗi
│   │   │   ├── Home.razor                     # Trang chính/Home
│   │   │   ├── NotFound.razor                 # Trang 404 không tìm thấy
│   │   │   └── Weather.razor                  # Trang dự báo thời tiết
│   ├── Properties/                            # Thuộc tính dự án
│   │   └── launchSettings.json                # Cấu hình khởi chạy (port, environment)
│   ├── wwwroot/                               # Thư mục chứa tài nguyên tĩnh (CSS, JS, images)
│   │   ├── app.css                            # File CSS chung cho ứng dụng
│   │   └── lib/                               # Thư mục chứa thư viện externe (Bootstrap, v.v.)
│   │       └── bootstrap/                     # Framework Bootstrap
│   ├── bin/                                   # Thư mục chứa file biên dịch (Debug, Release)
│   │   └── Debug/
│   │       └── net10.0/                       # Các file .dll, .exe đã biên dịch
│   └── obj/                                   # Đối tượng biên dịch, cache MSBuild
│       ├── BlazorApp_AdminWeb.csproj.nuget.dgspec.json    # Thông tin NuGet dependency graph
│       ├── BlazorApp_AdminWeb.csproj.nuget.g.props        # Props file được tạo từ NuGet
│       ├── BlazorApp_AdminWeb.csproj.nuget.g.targets      # Targets file được tạo từ NuGet
│       ├── project.assets.json                # File lock của NuGet packages
│       └── Debug/
│           └── net10.0/                       # Cache debug output
│
├── MauiApp_Mobile/                            # Ứng dụng di động đa nền tảng (.NET MAUI)
│   ├── App.xaml                               # Cấu hình app resource (Style, Color, Font)
│   ├── App.xaml.cs                            # Code-behind xử lý logic khởi tạo App
│   ├── AppShell.xaml                          # File định nghĩa shell (Menu, Tabs, Navigation)
│   ├── AppShell.xaml.cs                       # Code-behind xử lý AppShell
│   ├── MainPage.xaml                          # Giao diện màn hình chính
│   ├── MainPage.xaml.cs                       # Code-behind xử lý logic MainPage
│   ├── MauiApp_Mobile.csproj                  # File cấu hình dự án MAUI
│   ├── MauiApp_Mobile.csproj.user             # Cấu hình user cá nhân (không commit)
│   ├── MauiProgram.cs                         # File khởi tạo app, đăng ký dịch vụ dependency injection
│   ├── Platforms/                             # Code riêng cho từng nền tảng
│   │   ├── Android/                           # Code riêng cho Android
│   │   ├── iOS/                               # Code riêng cho iOS
│   │   ├── MacCatalyst/                       # Code riêng cho macOS
│   │   └── Windows/                           # Code riêng cho Windows
│   ├── Resources/                             # Thư mục chứa tài nguyên (Icon, Font, Image, Audio)
│   │   ├── AppIcon/                           # Icon ứng dụng cho các kích thước khác nhau
│   │   ├── Fonts/                             # Các font chữ tùy chỉnh
│   │   ├── Images/                            # Hình ảnh minh họa (.png, .svg, v.v.)
│   │   ├── Raw/                               # File âm thanh (.mp3, .wav, v.v.) - VỊ TRÍ QUAN TRỌNG
│   │   └── Styles/                            # File style chung
│   ├── Properties/                            # Thuộc tính dự án
│   │   └── launchSettings.json                # Cấu hình khởi chạy
│   ├── bin/                                   # Thư mục chứa output biên dịch
│   │   └── Debug/                             # Build debug cho các nền tảng (Android, iOS, Windows)
│   └── obj/                                   # Cache MSBuild, object files
│       ├── MauiApp_Mobile.csproj.nuget.dgspec.json
│       ├── MauiApp_Mobile.csproj.nuget.g.props
│       ├── MauiApp_Mobile.csproj.nuget.g.targets
│       ├── project.assets.json                # Lock file NuGet packages
│       └── Debug/
│
├── Project_SharedClassLibrary/                # Thư viện lớp dùng chung giữa các dự án
│   ├── Class1.cs                              # Lớp mẫu (có thể xóa hoặc sử dụng)
│   ├── Shared_ClassLibrary.csproj             # File cấu hình dự án
│   ├── bin/                                   # Output biên dịch
│   │   └── Debug/                             # Build debug (.dll)
│   └── obj/                                   # Cache biên dịch
│       ├── Project_SharedClassLibrary.csproj.nuget.dgspec.json
│       ├── Project_SharedClassLibrary.csproj.nuget.g.props
│       ├── Project_SharedClassLibrary.csproj.nuget.g.targets
│       ├── Shared_ClassLibrary.csproj.nuget.dgspec.json
│       ├── Shared_ClassLibrary.csproj.nuget.g.props
│       ├── Shared_ClassLibrary.csproj.nuget.g.targets
│       ├── project.assets.json
│       └── Debug/
│
└── WebApplication_API/                        # Ứng dụng Web API (ASP.NET Core)
    ├── appsettings.Development.json           # Cấu hình phát triển (logging, database dev)
    ├── appsettings.json                       # Cấu hình chung (database, CORS, middleware)
    ├── Program.cs                             # File khởi tạo ứng dụng, cấu hình middleware
    ├── WebApplication_API.csproj              # File cấu hình dự án Web API
    ├── WebApplication_API.http                # File HTTP test API (tương tự Postman)
    ├── Properties/                            # Thuộc tính dự án
    │   └── launchSettings.json                # Cấu hình khởi chạy (HTTP/HTTPS port)
    ├── bin/                                   # Output biên dịch
    │   └── Debug/                             # Build debug (.dll, .exe)
    └── obj/                                   # Cache biên dịch
        ├── project.assets.json                # Lock file NuGet packages
        ├── WebApplication_API.csproj.nuget.dgspec.json
        ├── WebApplication_API.csproj.nuget.g.props
        ├── WebApplication_API.csproj.nuget.g.targets
        └── Debug/                             # Cache debug build
```

---

## 14. Lộ trình phát triển / Development Milestones

### Phase 1 — Foundation (Tuần 1–2)
**[VI]** Thiết lập nền tảng cơ bản.
- [ ] Tạo Solution .NET, cấu hình DI container.
- [ ] Xây dựng `SharedClassLibrary`: Models, DTOs, Enums, Helpers.
- [ ] Backend: Kết nối SQL Server, Entity Framework migrations, seed data (4 roles + Super Admin).
- [ ] Backend: Auth module (Login, JWT, Refresh Token).
- [ ] Backend: CRUD POI cơ bản (GET all, GET by id, POST, PUT, DELETE).
- [ ] Mobile: Tích hợp SQLite, `IDatabaseService` cơ bản (lưu/đọc POI).

### Phase 2 — Core Mobile Features (Tuần 3–4)
**[VI]** Xây dựng tính năng di động cốt lõi.
- [ ] Mobile: `ILocationService` — GPS watchPosition, foreground service (Android).
- [ ] Mobile: `IGeofenceService` — Haversine distance, debounce 3s, cooldown 5 phút.
- [ ] Mobile: `IAudioService` — phát MP3 local, Device TTS fallback.
- [ ] Mobile: `MapViewModel` + `MapPage.xaml` — bản đồ + marker POI + vị trí user.
- [ ] Mobile: `MainViewModel` — ghép GPS + Geofence + Audio.
- [ ] Mobile: `POIDetailPage.xaml` — ảnh carousel, mô tả, menu.

### Phase 3 — Audio & Localization (Tuần 5–6)
**[VI]** Tích hợp TTS và đa ngôn ngữ.
- [ ] Backend: `TTS Service` — Edge-TTS, deep-translator, MD5 cache, 5 ngôn ngữ.
- [ ] Backend: `AudioTaskManager` — Semaphore 3, SSE progress stream.
- [ ] Backend: Localization module — Hotset, On-demand, Warmup endpoints.
- [ ] Backend: Auto-generate audio khi tạo/sửa POI (background task).
- [ ] Mobile: Tích hợp `IApiService` — download audio + sync offline.
- [ ] Mobile: `SettingsPage.xaml` — chọn ngôn ngữ, download gói offline.

### Phase 4 — Admin Web (Tuần 7–8)
**[VI]** Hoàn thiện trang quản trị.
- [ ] Blazor: Đăng nhập, quản lý session.
- [ ] Blazor: CRUD POI với map picker, upload ảnh.
- [ ] Blazor: Real-time Audio Task Monitor (SSE).
- [ ] Blazor: RBAC — CRUD Users, Roles, duyệt Owner Registrations.
- [ ] Blazor: Owner Portal — đăng ký, quản lý quán của mình, AI Advisor.
- [ ] Blazor: Audit Logs viewer.

### Phase 5 — Offline Maps & Polish (Tuần 9–10)
**[VI]** Bản đồ offline và hoàn thiện.
- [ ] Chuẩn bị PMTiles cho Quận 4.
- [ ] Backend: Maps module (serve PMTiles, manifest, fonts, sprites).
- [ ] Mobile: Tích hợp map offline (PMTiles local protocol).
- [ ] Mobile: 3 chế độ bản đồ (Cloud / Offline / Hybrid).
- [ ] Performance test: đo latency geofence → audio.
- [ ] Bug fixes, UI polish, kiểm thử thực địa tại phố Vĩnh Khánh.

---

## 15. Rủi ro & Giảm thiểu / Risks & Mitigations

| Rủi ro / Risk | Xác suất | Mức độ | Giảm thiểu / Mitigation |
|---|---|---|---|
| GPS không chính xác trong hẻm (nhiễu sóng) | Cao | Cao | Debounce 3s + throttle 5s; geofence_radius đủ lớn (30m) |
| Edge-TTS API bị giới hạn / down | Trung | Cao | Cache MD5: HIT = không gọi API; Device TTS làm Tier 3 fallback |
| Bộ nhớ thiết bị đầy (audio pack lớn) | Trung | Trung | LRU eviction audio cache cũ; cảnh báo người dùng khi < 100MB |
| Gemini API thay đổi giá / policy | Thấp | Thấp | Rate limit 10/ngày; feature này là phụ trợ, không critical |
| MAUI cross-platform bug (iOS đặc thù) | Cao | Trung | Focus Android trước (MVP), iOS là phase sau; foreground service Android-specific |
| Dữ liệu POI lỗi thời (chủ quán nghỉ) | Trung | Trung | Owner Portal cho phép chủ quán tự cập nhật; Admin có thể toggle/xóa |
| Thiếu kinh nghiệm MAUI trong nhóm | Trung | Cao | Bắt đầu với Android only; học từ MAUI official samples; pair programming |

---

## Phụ lục / Appendix

### A. Hằng số hệ thống / System Constants

| Hằng số | Giá trị | Nơi dùng |
|---|---|---|
| `ACCESS_TOKEN_EXPIRE` | 30 phút | Backend JWT |
| `REFRESH_TOKEN_EXPIRE` | 7 ngày | Backend JWT |
| `ROLE_CACHE_TTL` | 300 giây | Admin Service |
| `MAX_CONCURRENT_TTS` | 3 | AudioTaskManager |
| `PII_RETENTION_DAYS` | 180 ngày | Admin Service |
| `MAX_POI_IMAGES` | 8 ảnh | Content Service |
| `MAX_IMAGE_SIZE` | 5 MB | Content Service |
| `ON_DEMAND_RATE_LIMIT` | 30 req / 10 phút | Localization Service |
| `HOTSET_MAX_POI_IDS` | 10 | Localization Service |
| `HOTSET_NEARBY_RADIUS` | 1500m | Localization Service |
| `AI_DAILY_LIMIT_OWNER` | 10 | AI Advisor Service |
| `GPS_THROTTLE` | 5 giây | LocationService |
| `GEOFENCE_DEBOUNCE` | 3 giây | GeofenceService |
| `GEOFENCE_DEFAULT_RADIUS` | 30m | GeofenceService |
| `GEOFENCE_COOLDOWN` | 5 phút | GeofenceService |
| `HEARTBEAT_INTERVAL` | 1 giây | GeofenceService |
| `PREFETCH_QUEUE_LIMIT` | 3 POI / batch | Background Prefetch |
| `PREFETCH_GATE_MIN` | 30 giây | Background Prefetch |
| `HOTSET_GPS_TIMEOUT` | 2.5 giây | Startup Flow |

### B. Ngôn ngữ hỗ trợ / Supported Languages

| Code | Ngôn ngữ | Giọng TTS | Ghi chú |
|---|---|---|---|
| `vi` | Tiếng Việt | `vi-VN-HoaiMyNeural` | Ngôn ngữ gốc |
| `en` | English | `en-US-JennyNeural` | Fallback chung |
| `zh` | 中文 | `zh-CN-XiaoxiaoNeural` | Giản thể |
| `ja` | 日本語 | `ja-JP-NanamiNeural` | |
| `ko` | 한국어 | `ko-KR-SunHiNeural` | |

### C. Thuật ngữ / Glossary

| Thuật ngữ | Định nghĩa |
|---|---|
| **POI** | Point of Interest — Điểm quan tâm (quán ăn, địa điểm) |
| **Geofence** | Vùng địa lý ảo xung quanh POI, khi người dùng vào vùng này sẽ trigger sự kiện |
| **Haversine** | Công thức tính khoảng cách giữa 2 tọa độ GPS trên mặt cầu |
| **PMTiles** | Định dạng file bản đồ tile đơn, hỗ trợ Range Requests → phục vụ offline |
| **TTS** | Text-to-Speech — Chuyển văn bản thành giọng nói |
| **RBAC** | Role-Based Access Control — Phân quyền theo vai trò |
| **SSE** | Server-Sent Events — Kênh push dữ liệu từ server → client theo thời gian thực |
| **Hotset** | Tập POI ưu tiên gần người dùng nhất, được dịch + sinh audio trước khi cần |
| **Warmup** | Quá trình dịch + sinh audio toàn bộ POI dưới nền để chuẩn bị offline |
| **PII** | Personally Identifiable Information — Thông tin nhận dạng cá nhân (CCCD) |
| **MVVM** | Model-View-ViewModel — Kiến trúc UI tách biệt logic khỏi giao diện |
| **DI** | Dependency Injection — Kỹ thuật inject phụ thuộc qua constructor/interface |

---

*© 2026 — Nguyễn Sĩ Huy & Nguyễn Văn Cường — Dự Án Thuyết Minh Phố Ẩm Thực*
