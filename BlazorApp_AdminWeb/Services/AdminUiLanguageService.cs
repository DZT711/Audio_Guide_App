using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminUiLanguageService
{
    private static readonly IReadOnlyDictionary<string, string> VietnameseTexts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["language.en"] = "EN",
            ["language.vi"] = "VI",
            ["header.brand"] = "Smart Tourism Admin",
            ["header.logout"] = "Đăng xuất",
            ["header.default-context"] = "Phiên quản trị",
            ["page.login"] = "Đăng nhập",
            ["page.dashboard"] = "Bảng điều khiển",
            ["page.statistics"] = "Thống kê",
            ["page.usage"] = "Lịch sử sử dụng",
            ["page.poi"] = "Quản lý địa điểm",
            ["page.categories"] = "Quản lý danh mục",
            ["page.languages"] = "Quản lý ngôn ngữ",
            ["page.audio"] = "Quản lý audio & giọng đọc",
            ["page.tours"] = "Quản lý tour",
            ["page.users"] = "Quản lý người dùng",
            ["page.moderation"] = "Kiểm duyệt",
            ["page.history"] = "Lịch sử hoạt động",
            ["page.admin"] = "Quản trị",
            ["poi.page.title"] = "Quản lý địa điểm",
            ["poi.access.denied"] = "Truy cập bị từ chối.",
            ["poi.access.denied.desc"] = "Vai trò hiện tại không có quyền xem địa điểm.",
            ["poi.hero.title"] = "Quản lý địa điểm",
            ["poi.hero.desc"] = "Quản lý các điểm du lịch trực tiếp qua API, bao gồm danh mục, tọa độ, thông tin liên hệ và trạng thái phát hành.",
            ["poi.action.new"] = "Tạo POI mới",
            ["poi.action.request-new"] = "Yêu cầu POI mới",
            ["poi.bulk.title"] = "QR hàng loạt",
            ["poi.refresh"] = "Làm mới",
            ["poi.stats.total"] = "Tổng địa điểm",
            ["poi.stats.categories"] = "Danh mục liên kết",
            ["poi.stats.matching"] = "Khớp bộ lọc",
            ["poi.directory.title"] = "Địa điểm du lịch",
            ["poi.search.placeholder"] = "Tìm địa điểm...",
            ["poi.filter.categories.all"] = "Tất cả danh mục",
            ["poi.filter.status.all"] = "Tất cả trạng thái",
            ["poi.load.failed"] = "Không thể tải danh sách địa điểm.",
            ["poi.load.failed.toast"] = "Tải địa điểm thất bại",
            ["poi.empty.filtered"] = "Không có địa điểm nào khớp bộ lọc hiện tại.",
            ["poi.map.unavailable"] = "Bộ chọn OpenStreetMap hiện không khả dụng.",
            ["poi.map.unavailable.title"] = "Bản đồ không khả dụng",
            ["poi.map.search.empty"] = "Hãy nhập địa danh, đường hoặc địa chỉ để tìm trên bản đồ.",
            ["poi.map.search.none"] = "Không tìm thấy địa điểm nào cho '{0}'.",
            ["poi.map.search.unavailable"] = "Tính năng tìm trên bản đồ hiện không khả dụng.",
            ["poi.readonly.title"] = "Chỉ có quyền xem",
            ["poi.save.readonly"] = "Vai trò hiện tại chỉ được xem địa điểm, không thể lưu thay đổi.",
            ["poi.save.title"] = "Đã lưu địa điểm",
            ["poi.save.created"] = "Tạo địa điểm thành công.",
            ["poi.save.updated"] = "Cập nhật địa điểm thành công.",
            ["poi.save.failed"] = "Lưu địa điểm thất bại",
            ["poi.request.title"] = "Đã gửi yêu cầu",
            ["poi.request.created"] = "Yêu cầu tạo POI mới đã được gửi để Admin hoặc Developer duyệt.",
            ["poi.request.updated"] = "Yêu cầu cập nhật POI đã được gửi để Admin hoặc Developer duyệt.",
            ["poi.validation.generic"] = "Hãy sửa các trường POI đang được đánh dấu trước khi lưu.",
            ["poi.validation.title"] = "Biểu mẫu địa điểm chưa hoàn chỉnh",
            ["poi.archive.readonly"] = "Vai trò hiện tại có thể xem địa điểm nhưng không thể lưu trữ.",
            ["poi.archive.title"] = "Đã lưu trữ địa điểm",
            ["poi.archive.success"] = "'{0}' hiện đã được chuyển sang trạng thái ngưng hoạt động.",
            ["poi.archive.requested"] = "Yêu cầu lưu trữ cho '{0}' đã được gửi để phê duyệt.",
            ["poi.editor.category.none"] = "Hãy chọn danh mục và hoàn tất từng phần trước khi lưu.",
            ["poi.editor.category.selected"] = "Đã chọn danh mục",
            ["poi.save.saving"] = "Đang lưu...",
            ["poi.save.submitting"] = "Đang gửi...",
            ["poi.save.create"] = "Tạo địa điểm",
            ["poi.save.update"] = "Lưu thay đổi",
            ["poi.save.submit-create"] = "Gửi POI mới",
            ["poi.save.submit-update"] = "Gửi yêu cầu cập nhật",
            ["sidebar.overview"] = "Tổng quan",
            ["sidebar.management"] = "Quản lý",
            ["sidebar.admin"] = "Khu quản trị",
            ["sidebar.dashboard"] = "Bảng điều khiển",
            ["sidebar.statistics"] = "Thống kê",
            ["sidebar.usage"] = "Lịch sử sử dụng",
            ["sidebar.inbox"] = "Hộp thư",
            ["sidebar.locations"] = "Địa điểm",
            ["sidebar.tours"] = "Tour",
            ["sidebar.categories"] = "Danh mục",
            ["sidebar.languages"] = "Ngôn ngữ",
            ["sidebar.audio"] = "Audio & giọng đọc",
            ["sidebar.users"] = "Người dùng",
            ["sidebar.moderation"] = "Kiểm duyệt",
            ["sidebar.activity"] = "Lịch sử hoạt động",
            ["sidebar.navigation"] = "Điều hướng",
            ["status.active"] = "Đang hoạt động",
            ["status.inactive"] = "Ngưng hoạt động",
            ["role.admin"] = "Quản trị viên",
            ["role.developer"] = "Lập trình viên",
            ["role.user"] = "Chủ sở hữu",
            ["role.editor"] = "Biên tập viên",
            ["role.dataanalyst"] = "Phân tích dữ liệu",
            ["qr.no-access.title"] = "Vai trò hiện tại không có quyền dùng QR.",
            ["qr.no-access.desc"] = "Bộ quyền của tài khoản này chưa mở công cụ QR cho địa điểm.",
            ["qr.save-first.title"] = "Hãy lưu địa điểm trước.",
            ["qr.save-first.desc"] = "Sau khi địa điểm có ID thật, bạn mới xem trạng thái, preview và tải QR được.",
            ["qr.eyebrow"] = "QR Địa Điểm",
            ["qr.header.desc"] = "Tạo QR production để mở app, fallback sang trang cài đặt, và giữ autoplay đồng bộ với audio của địa điểm.",
            ["qr.refresh"] = "Làm mới trạng thái",
            ["qr.refreshing"] = "Đang làm mới...",
            ["qr.load-error.title"] = "Không thể tải trạng thái QR.",
            ["qr.loading"] = "Đang tải trạng thái QR...",
            ["qr.status"] = "Trạng thái",
            ["qr.status.active-title"] = "Địa điểm đang hoạt động",
            ["qr.status.active-desc"] = "QR sinh ra sẽ mở địa điểm bình thường.",
            ["qr.status.inactive-title"] = "Địa điểm đang tắt",
            ["qr.status.inactive-desc"] = "Địa điểm tắt sẽ đưa người dùng sang màn unavailable.",
            ["qr.default-audio"] = "Audio mặc định",
            ["qr.default-audio.empty"] = "Chưa có audio mặc định đang hoạt động",
            ["qr.default-audio.desc"] = "Để trống track cụ thể thì QR sẽ ưu tiên phát audio mặc định này.",
            ["qr.default-audio.empty-desc"] = "Vẫn có thể mở app tới địa điểm, nhưng người dùng sẽ tự chọn audio thủ công.",
            ["qr.owner"] = "Chủ sở hữu",
            ["qr.owner.empty"] = "Chưa gán",
            ["qr.location-id"] = "Mã địa điểm",
            ["qr.setup.kicker"] = "Thiết lập QR",
            ["qr.setup.title"] = "Tinh chỉnh cách địa điểm này được mở",
            ["qr.setup.desc"] = "Chọn định dạng file, kích thước xuất ra, và cách audio hoạt động khi mobile app được mở từ QR.",
            ["qr.format"] = "Định dạng",
            ["qr.size"] = "Kích thước (px)",
            ["qr.size.hint"] = "Khuyến nghị 256-1024 để in ấn và chia sẻ màn hình.",
            ["qr.behavior.autoplay"] = "Tự phát audio địa điểm sau deep link",
            ["qr.behavior.autoplay.hint"] = "Bật tùy chọn này để app mở xong là phát luôn track mặc định khi có thể.",
            ["qr.behavior.track"] = "Audio cụ thể",
            ["qr.behavior.track.search"] = "Tìm theo tên track, ngôn ngữ hoặc voice",
            ["qr.behavior.track.hint"] = "Gõ để lọc các audio đang active của địa điểm này rồi chọn nhanh.",
            ["qr.behavior.track.empty"] = "Không có audio active nào cho địa điểm này.",
            ["qr.behavior.track.default"] = "Dùng audio mặc định",
            ["qr.behavior.track.selected"] = "Đang chọn",
            ["qr.behavior.track.id"] = "Track ID",
            ["qr.behavior.track.available"] = "Track active khả dụng",
            ["qr.persist.hint"] = "Các thiết lập QR này sẽ được lưu cùng địa điểm khi bạn bấm Lưu thay đổi.",
            ["qr.validation.none"] = "Chưa có lỗi xác thực.",
            ["qr.generate"] = "Tạo preview",
            ["qr.generating"] = "Đang tạo...",
            ["qr.download"] = "Tải QR",
            ["qr.downloading"] = "Đang tải...",
            ["qr.clear"] = "Xóa preview",
            ["qr.preview.kicker"] = "Preview",
            ["qr.preview.title"] = "QR sẵn sàng để xuất",
            ["qr.preview.desc"] = "Generate một lần để xem đúng file mà admin sẽ tải xuống và chia sẻ.",
            ["qr.preview.ready"] = "Preview sẵn sàng",
            ["qr.preview.empty-title"] = "Chưa tạo preview",
            ["qr.preview.empty-desc"] = "Dùng phần thiết lập bên trái để tạo QR preview cho địa điểm này.",
            ["qr.links.kicker"] = "Liên kết mở",
            ["qr.links.title"] = "Các route public gắn với QR này",
            ["qr.links.desc"] = "Mở nhanh landing page, fallback download page và Android install QR.",
            ["qr.link.landing"] = "Landing public",
            ["qr.link.landing-title"] = "Mở landing khi quét",
            ["qr.link.download"] = "Trang fallback",
            ["qr.link.download-title"] = "Mở trang tải app",
            ["qr.link.apk"] = "QR cài Android",
            ["qr.link.apk-title"] = "Mở QR APK"
        };

    public event Action? Changed;

    public string CurrentLanguage { get; private set; } = "en";

    public bool IsVietnamese =>
        string.Equals(CurrentLanguage, "vi", StringComparison.OrdinalIgnoreCase);

    public void SetLanguage(string language)
    {
        var normalizedLanguage = string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase)
            ? "vi"
            : "en";

        if (string.Equals(CurrentLanguage, normalizedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentLanguage = normalizedLanguage;
        Changed?.Invoke();
    }

    public string T(string key, string? fallback = null)
    {
        if (!IsVietnamese)
        {
            return fallback ?? key;
        }

        return VietnameseTexts.TryGetValue(key, out var value)
            ? value
            : fallback ?? key;
    }

    public string GetRoleLabel(string? role)
    {
        var fallback = string.IsNullOrWhiteSpace(role) ? "Unknown" : role;

        return role?.Trim() switch
        {
            AdminRoles.Admin => T("role.admin", fallback),
            AdminRoles.Developer => T("role.developer", fallback),
            AdminRoles.User => T("role.user", fallback),
            AdminRoles.Editor => T("role.editor", fallback),
            AdminRoles.DataAnalyst => T("role.dataanalyst", fallback),
            _ => fallback
        };
    }

    public string GetStatusLabel(int status) =>
        status == 1
            ? T("status.active", "Active")
            : T("status.inactive", "Inactive");
}
