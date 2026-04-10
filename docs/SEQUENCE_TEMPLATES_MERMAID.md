# Sequence Diagrams From Code

Tài liệu này chỉ chứa các sequence rút ra từ code thật của đồ án. Không dùng luồng suy diễn hay ví dụ ngoài.

Nguồn chính đã đọc:
- `WebApplication_API/Controller/*.cs`
- `BlazorApp_AdminWeb/Components/Pages/*.razor`
- `BlazorApp_AdminWeb/Services/AdminApiClient.cs`
- `BlazorApp_AdminWeb/Services/AdminAuthService.cs`

## 1. Đăng nhập (Web Admin)

```plantuml
@startuml
title 1. Đăng nhập
hide footbox
autonumber
actor "Người dùng" as NguoiDung
boundary "Login.razor" as TrangDangNhap
control "AdminAuthService" as DichVuXacThuc
control "AdminApiClient" as ApiClient
control "AuthController" as API
entity "DBContext/DashboardUsers" as CSDL
entity "localStorage" as BoNho

NguoiDung -> TrangDangNhap: Nhập tên đăng nhập và mật khẩu
TrangDangNhap -> DichVuXacThuc: Gọi LoginAsync(userName, password)
DichVuXacThuc -> ApiClient: LoginAsync(userName, password)
ApiClient -> API: POST Auth/login
API -> CSDL: Tìm DashboardUser theo Username
alt Không tìm thấy hoặc tài khoản không hoạt động
    API --> ApiClient: 401/403 với message
    ApiClient --> DichVuXacThuc: exception
    DichVuXacThuc --> TrangDangNhap: false + LastErrorMessage
    TrangDangNhap --> NguoiDung: Hiển thị thông báo lỗi
else Tìm thấy và hợp lệ
    API -> API: Kiểm tra PasswordHash
    alt Sai mật khẩu
        API --> ApiClient: 401 với message
        ApiClient --> DichVuXacThuc: exception
        DichVuXacThuc --> TrangDangNhap: false + LastErrorMessage
        TrangDangNhap --> NguoiDung: Hiển thị thông báo sai mật khẩu
    else Đúng mật khẩu
        API -> CSDL: Tạo session ticket 8 giờ, lưu UpdatedAt
        API --> ApiClient: token, expiresAt, user
        ApiClient --> DichVuXacThuc: AdminLoginResponse
        DichVuXacThuc -> BoNho: Lưu smartTourAdmin.auth
        DichVuXacThuc --> TrangDangNhap: true
        TrangDangNhap -> TrangDangNhap: NavigateTo("/dashboard")
        TrangDangNhap --> NguoiDung: Chuyển đến trang dashboard
    end
end
@enduml
```

## 2. Dashboard (Web Admin)

```plantuml
@startuml
title 2. Dashboard
hide footbox
autonumber
boundary "Dashboard.razor" as Page
control "AdminApiClient" as ApiClient
control "DashboardController" as API
entity "DBContext" as DB

Page -> ApiClient: GetDashboardOverviewAsync()
ApiClient -> API: GET Dashboard/overview
API -> DB: Đếm POI, audio, tour, user, telemetry theo quyền
API -> DB: Lấy hoạt động gần đây và focus items
API --> ApiClient: Dữ liệu tổng quan bảng điều khiển
ApiClient --> Page: Dữ liệu tổng quan

Page -> ApiClient: GetDashboardSnapshotAsync()
ApiClient -> API: GET Dashboard/snapshot
API -> DB: Lấy categories, locations, tours, audio, users
API --> ApiClient: Dữ liệu ảnh chụp hệ thống
ApiClient --> Page: Dữ liệu snapshot để xuất
@enduml
```

## 3. Danh mục (Web Admin)

```plantuml
@startuml
title 3. Danh mục
hide footbox
autonumber
boundary "CategoryList.razor" as Page
control "AdminApiClient" as ApiClient
control "CategoryController" as API
entity "DBContext/Categories" as DB

Page -> ApiClient: GetCategoriesAsync()
ApiClient -> API: GET Category
API -> DB: Lấy danh sách danh mục
API --> ApiClient: Danh sách danh mục

alt Tạo mới
    Page -> ApiClient: CreateCategoryAsync(model)
    ApiClient -> API: POST Category
    API -> DB: Kiểm tra trùng tên
    alt Trùng tên
        API --> ApiClient: 409 xung đột dữ liệu
    else Hợp lệ
        API -> DB: SaveChanges
        API --> ApiClient: Tạo mới thành công
    end
else Cập nhật
    Page -> ApiClient: UpdateCategoryAsync(id, model)
    ApiClient -> API: PUT Category/{id}
    API -> DB: Tìm category, kiểm tra trùng tên
    API -> DB: SaveChanges
    API --> ApiClient: Cập nhật thành công
else Archive
    Page -> ApiClient: DeleteCategoryAsync(id)
    ApiClient -> API: DELETE Category/{id}
    API -> DB: Đặt Status = 0
    API --> ApiClient: Archive thành công
end
@enduml
```

## 4. Ngôn ngữ (Web Admin)

```plantuml
@startuml
title 4. Ngôn ngữ
hide footbox
autonumber
boundary "LanguageList.razor" as Page
control "AdminApiClient" as ApiClient
control "LanguageController" as API
entity "DBContext/Languages" as DB

Page -> ApiClient: GetLanguagesAsync()
ApiClient -> API: GET Language
API -> DB: Lấy danh sách ngôn ngữ
API --> ApiClient: Danh sách ngôn ngữ

alt Tạo mới
    Page -> ApiClient: CreateLanguageAsync(model)
    ApiClient -> API: POST Language
    API -> DB: Kiểm tra trùng code
    alt Đặt làm mặc định
        API -> DB: Gỡ cờ mặc định ngôn ngữ khác
    end
    API -> DB: Lưu ngôn ngữ mới
    API -> DB: Đảm bảo luôn có 1 default
else Cập nhật
    Page -> ApiClient: UpdateLanguageAsync(id, model)
    ApiClient -> API: PUT Language/{id}
    API -> DB: Tìm ngôn ngữ, kiểm tra trùng code
    API -> DB: Cập nhật và đảm bảo default
else Archive
    Page -> ApiClient: DeleteLanguageAsync(id)
    ApiClient -> API: DELETE Language/{id}
    API -> DB: Đặt Status = 0, IsDefault = false
end
@enduml
```

## 5. Địa điểm / POI (Web Admin)

```plantuml
@startuml
title 5. Địa điểm / POI
hide footbox
autonumber
boundary "POIList.razor" as Page
control "AdminApiClient" as ApiClient
control "LocationController" as API
entity "DBContext/Locations" as DB

Page -> ApiClient: GetLocationsAsync()
ApiClient -> API: GET Location
API -> DB: Lấy danh sách theo quyền
API --> ApiClient: Danh sách địa điểm

alt Tạo mới
    Page -> ApiClient: CreateLocationAsync(model)
    ApiClient -> API: POST Location (multipart/form-data)
    API -> DB: Kiểm tra category, owner, file ảnh
    API -> DB: SaveChanges + lưu ảnh
    API --> ApiClient: Tạo mới thành công
else Cập nhật
    Page -> ApiClient: UpdateLocationAsync(id, model)
    ApiClient -> API: PUT Location/{id}
    API -> DB: Tìm location, kiểm tra quyền owner
    API -> DB: Cập nhật + đồng bộ ảnh
    API --> ApiClient: Cập nhật thành công
else Archive
    Page -> ApiClient: DeleteLocationAsync(id)
    ApiClient -> API: DELETE Location/{id}
    API -> DB: Đặt Status = 0
    API --> ApiClient: Archive thành công
else Gửi yêu cầu thay đổi
    Page -> ApiClient: SubmitLocationChangeRequestAsync(model,...)
    ApiClient -> API: POST ChangeRequest/location
    API -> DB: Tạo change request
    API --> ApiClient: Kết quả yêu cầu thay đổi
end
@enduml
```

## 6. Audio (Web Admin)

```plantuml
@startuml
title 6. Audio
hide footbox
autonumber
boundary "AudioList.razor" as Page
control "AdminApiClient" as ApiClient
control "AudioController" as API
entity "DBContext/AudioContents" as DB

Page -> ApiClient: GetAudioAsync()
ApiClient -> API: GET Audio
API -> DB: Lấy danh sách audio theo quyền
API --> ApiClient: Danh sách âm thanh

Page -> ApiClient: GenerateAudioTtsPreviewAsync(request)
ApiClient -> API: POST Audio/preview-tts
alt Gemini bật
    API -> API: Dịch và sinh speech bằng Gemini
else Dùng TTS preview mặc định
    API -> API: Sinh audio preview bằng TtsPreviewService
end
API --> ApiClient: Tệp âm thanh + header phản hồi

alt Tạo mới / cập nhật / archive
    Page -> ApiClient: CreateAudioAsync / UpdateAudioAsync / DeleteAudioAsync
    ApiClient -> API: POST/PUT/DELETE Audio
    API -> DB: Kiểm tra location, language, quyền owner
    API -> DB: Lưu / cập nhật / đặt Status = 0
    API --> ApiClient: Thành công (tạo mới hoặc cập nhật)
else Gửi yêu cầu thay đổi
    Page -> ApiClient: SubmitAudioChangeRequestAsync(...)
    ApiClient -> API: POST ChangeRequest/audio
    API -> DB: Tạo change request
end
@enduml
```

## 7. Tour (Web Admin)

```plantuml
@startuml
title 7. Tour
hide footbox
autonumber
boundary "TourList.razor" as Page
control "AdminApiClient" as ApiClient
control "TourController" as API
control "TourRoutePlanningService" as RouteSvc
entity "DBContext" as DB

Page -> ApiClient: GetToursAsync()
ApiClient -> API: GET Tour
API -> DB: Lấy danh sách tour theo quyền
API --> ApiClient: Danh sách tuyến tham quan

Page -> ApiClient: PreviewTourRouteAsync(request)
ApiClient -> API: POST Tour/preview
API -> RouteSvc: Tính quãng đường + thời gian dự kiến
API --> ApiClient: Dữ liệu xem trước tuyến đường

alt Tạo / cập nhật
    Page -> ApiClient: CreateTourAsync(model, preview) / UpdateTourAsync(id, model, preview)
    ApiClient -> API: POST/PUT Tour
    API -> DB: Kiểm tra POI, trạng thái, tạo / cập nhật stops
    API --> ApiClient: Thành công (tạo/cập nhật tuyến)
else Archive
    Page -> ApiClient: DeleteTourAsync(id)
    ApiClient -> API: DELETE Tour/{id}
    API -> DB: Đặt Status = 0
    API --> ApiClient: Archive thành công
end
@enduml
```

## 8. Người dùng dashboard (Web Admin)

```plantuml
@startuml
title 8. Người dùng dashboard
hide footbox
autonumber
boundary "UserList.razor" as Page
control "AdminApiClient" as ApiClient
control "DashboardUserController" as API
entity "DBContext/DashboardUsers" as DB

Page -> ApiClient: GetUsersAsync()
ApiClient -> API: GET DashboardUser
API -> DB: Lấy danh sách user + số POI/audio sở hữu
API --> ApiClient: Danh sách người dùng

alt Tạo mới
    Page -> ApiClient: CreateUserAsync(model)
    ApiClient -> API: POST DashboardUser
    API -> DB: Kiểm tra role, username/email/phone, hash password
    API -> DB: SaveChanges
    API --> ApiClient: Tạo mới thành công
else Mời người dùng
    Page -> ApiClient: InviteUserAsync(model)
    ApiClient -> API: POST DashboardUser/invite
    API -> DB: Sinh mật khẩu tạm + hash + lưu user status=0
    API --> ApiClient: Kết quả mời người dùng
else Cập nhật / đổi trạng thái
    Page -> ApiClient: UpdateUserAsync(id, model)
    ApiClient -> API: PUT DashboardUser/{id}
    API -> DB: Tìm user, kiểm tra trùng, cập nhật, hash mật khẩu nếu có
    API --> ApiClient: Cập nhật thành công
else Archive
    Page -> ApiClient: UpdateUserAsync(id, model with Status=0)
    ApiClient -> API: PUT DashboardUser/{id}
    API -> DB: Đặt Status = 0 nếu hợp lệ
    API --> ApiClient: Archive thành công
end
@enduml
```

## 9. Hộp thư (Web Admin)

```plantuml
@startuml
title 9. Hộp thư
hide footbox
autonumber
boundary "Inbox.razor" as Page
control "AdminApiClient" as ApiClient
control "InboxController" as API
control "ChangeRequestWorkflowService" as Flow

Page -> ApiClient: GetInboxAsync(query)
ApiClient -> API: GET Inbox?page=...&pageSize=...&unreadOnly=...
API -> Flow: Lấy inbox theo user hiện tại
API --> ApiClient: Dữ liệu tổng quan hộp thư

Page -> ApiClient: MarkInboxMessageReadAsync(id)
ApiClient -> API: POST Inbox/{id}/read
API -> Flow: Đánh dấu đã đọc
API --> ApiClient: Đánh dấu đã đọc thành công

Page -> ApiClient: CreateInboxAnnouncementAsync(request)
ApiClient -> API: POST Inbox/announcement
API -> Flow: Gửi thông báo hệ thống tới mọi inbox
API --> ApiClient: Gửi thông báo thành công
@enduml
```

## 10. Moderation / Change request (Web Admin)

```plantuml
@startuml
title 10. Moderation / Change request
hide footbox
autonumber
boundary "ModerationList.razor" as Page
control "AdminApiClient" as ApiClient
control "ChangeRequestController" as API
control "ChangeRequestWorkflowService" as Flow

Page -> ApiClient: GetChangeRequestsAsync(query)
ApiClient -> API: GET ChangeRequest
API -> Flow: Lấy danh sách yêu cầu
API --> ApiClient: Danh sách yêu cầu thay đổi

alt Phê duyệt
    Page -> ApiClient: ApproveChangeRequestAsync(id, note)
    ApiClient -> API: POST ChangeRequest/{id}/approve
    API -> Flow: Áp dụng thay đổi vào dữ liệu live
    API --> ApiClient: Kết quả sau khi phê duyệt
else Từ chối
    Page -> ApiClient: RejectChangeRequestAsync(id, note)
    ApiClient -> API: POST ChangeRequest/{id}/reject
    API -> Flow: Từ chối và báo cho chủ yêu cầu
    API --> ApiClient: Kết quả sau khi từ chối
end
@enduml
```

## 11. Usage history (Web Admin)

```plantuml
@startuml
title 11. Usage history
hide footbox
autonumber
boundary "UsageHistory.razor" as Page
control "AdminApiClient" as ApiClient
control "UsageController" as API
entity "DBContext" as DB

Page -> ApiClient: GetUsageHistoryAsync()
ApiClient -> API: GET Usage
API -> DB: Lấy playback events theo scope user
API -> DB: Ghép tour names, tính thống kê
API --> ApiClient: Dữ liệu lịch sử sử dụng
@enduml
```

## 12. Statistics (Web Admin)

```plantuml
@startuml
title 12. Statistics
hide footbox
autonumber
boundary "Statistics.razor" as Page
control "AdminApiClient" as ApiClient
control "StatisticsController" as API
entity "DBContext" as DB

Page -> ApiClient: GetStatisticsAsync(query)
ApiClient -> API: GET Statistics?from=...&to=...&tourId=...&ward=...&search=...
API -> DB: Lấy locations, audio, tours, playback, tracking
API -> DB: Tổng hợp biểu đồ, heatmap, top POI, average listening
API --> ApiClient: Dữ liệu thống kê tổng quan
@enduml
```

## Ghi chú

- Các sequence trên đều phản ánh hành vi đang có trong code.
- Nếu muốn vẽ tiếp màn hình phụ như `ActivityHistory.razor` hoặc các luồng JS preview ảnh/map, nên vẽ riêng vì đó là nhánh UI chứ không phải CRUD chính.
- Với Draw.io hiện tại, hãy dùng `Arrange -> Insert -> Advanced -> PlantUML`, rồi paste từng block `@startuml ... @enduml`.
