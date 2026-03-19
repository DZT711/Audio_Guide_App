# App Thuyết Minh Phố Ẩm Thực Vĩnh Khánh

## Thành viên

1. Nguyễn Sĩ Huy - 3123411122
2. Nguyễn Văn Cường - 3123411045

## Cấu trúc đồ án dự kiến

### MAUI

```html
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

```html
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

```html
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
│   │   ├── Raw/                               # File âm thanh (.mp3, .wav, v.v.) - VỊ TRỊ QUAN TRỌNG
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
