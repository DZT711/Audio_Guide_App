using MauiApp_Mobile.Models;

namespace MauiApp_Mobile.Services;

public sealed class PlaceCatalogService
{
    public static PlaceCatalogService Instance { get; } = new();

    private readonly IReadOnlyList<PlaceItem> _places;

    private PlaceCatalogService()
    {
        _places = new List<PlaceItem>
        {
            new()
            {
                Id = "com-tam-goc-sai-gon",
                Name = "Cơm Tấm Góc Sài Gòn",
                Description = "Quán cơm tấm đông khách, vị đậm đà gần trung tâm",
                AudioDescription = "Cơm Tấm Góc Sài Gòn nổi bật với sườn nướng thơm, bì chả đầy đặn và nước mắm pha vừa vị.",
                Category = "Món ăn đặc trưng",
                Rating = "9/10",
                Image = "map.png",
                GalleryImages = ["map.png", "location.png", "route.png"],
                Address = "58 Võ Văn Tần, Quận 3, TP.HCM",
                Phone = "(028) 3820 1122",
                Email = "comtamgocsaigon@example.vn",
                Website = "comtamgocsaigon.vn",
                EstablishedYear = "2016",
                RadiusText = "75m",
                GpsText = "10.779120, 106.683900",
                Latitude = 10.779120,
                Longitude = 106.683900,
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new()
            {
                Id = "pho-bo-nguyen-dinh-chieu",
                Name = "Phở Bò Nguyễn Đình Chiểu",
                Description = "Tô phở nóng với nước dùng thanh và bò mềm",
                AudioDescription = "Phở Bò Nguyễn Đình Chiểu phục vụ phở truyền thống với nước dùng trong, thơm mùi quế hồi.",
                Category = "Món ăn đặc trưng",
                Rating = "9/10",
                Image = "location.png",
                GalleryImages = ["location.png", "history.png", "route.png"],
                Address = "124 Nguyễn Đình Chiểu, Quận 3, TP.HCM",
                Phone = "(028) 3930 2233",
                Email = "phobondc@example.vn",
                Website = "phobondc.vn",
                EstablishedYear = "2018",
                RadiusText = "90m",
                GpsText = "10.777950, 106.685150",
                Latitude = 10.777950,
                Longitude = 106.685150,
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new()
            {
                Id = "bun-bo-hue-chi-mai",
                Name = "Bún Bò Huế Chị Mai",
                Description = "Bún bò cay nhẹ, topping đầy đủ và nước dùng đậm",
                AudioDescription = "Bún Bò Huế Chị Mai nổi tiếng với nước lèo đậm vị, chả cua thơm và thịt bò mềm.",
                Category = "Món ăn đặc trưng",
                Rating = "8/10",
                Image = "route.png",
                GalleryImages = ["route.png", "map.png", "history.png"],
                Address = "36 Trần Quốc Thảo, Quận 3, TP.HCM",
                Phone = "(028) 3932 4455",
                Email = "bunbochimai@example.vn",
                Website = "bunbochimai.vn",
                EstablishedYear = "2019",
                RadiusText = "110m",
                GpsText = "10.780020, 106.686050",
                Latitude = 10.780020,
                Longitude = 106.686050,
                CategoryColor = Color.FromArgb("#FFE3E3"),
                CategoryTextColor = Color.FromArgb("#E53935")
            },
            new()
            {
                Id = "quan-moc-garden-sai-gon",
                Name = "Quán Mộc Garden Sài Gòn",
                Description = "Không gian sân vườn mát, phù hợp ăn uống nhóm nhỏ",
                AudioDescription = "Quán Mộc Garden Sài Gòn có không gian xanh và thực đơn Việt hiện đại, phù hợp gặp gỡ bạn bè.",
                Category = "Quán nổi tiếng",
                Rating = "9/10",
                Image = "history.png",
                GalleryImages = ["history.png", "settings.png", "location.png"],
                Address = "22 Pasteur, Quận 3, TP.HCM",
                Phone = "(028) 3829 6677",
                Email = "mocgarden@example.vn",
                Website = "mocgardensaigon.vn",
                EstablishedYear = "2017",
                RadiusText = "120m",
                GpsText = "10.776980, 106.683480",
                Latitude = 10.776980,
                Longitude = 106.683480,
                CategoryColor = Color.FromArgb("#FFF7D6"),
                CategoryTextColor = Color.FromArgb("#CA8A04")
            },
            new()
            {
                Id = "cafe-song-xanh",
                Name = "Cafe Sông Xanh",
                Description = "Quán cà phê yên tĩnh, thích hợp nghỉ chân buổi chiều",
                AudioDescription = "Cafe Sông Xanh phục vụ cà phê rang mộc và nhiều loại đồ uống nhẹ trong không gian thư giãn.",
                Category = "Đồ uống",
                Rating = "8/10",
                Image = "settings.png",
                GalleryImages = ["settings.png", "offline.png", "map.png"],
                Address = "75 Nam Kỳ Khởi Nghĩa, Quận 3, TP.HCM",
                Phone = "(028) 3911 7788",
                Email = "cafesongxanh@example.vn",
                Website = "cafesongxanh.vn",
                EstablishedYear = "2020",
                RadiusText = "95m",
                GpsText = "10.779640, 106.682950",
                Latitude = 10.779640,
                Longitude = 106.682950,
                CategoryColor = Color.FromArgb("#E6F4FF"),
                CategoryTextColor = Color.FromArgb("#2563EB")
            },
            new()
            {
                Id = "cho-am-thuc-dem-nguyen-thuong-hien",
                Name = "Chợ Ẩm Thực Đêm Nguyễn Thượng Hiền",
                Description = "Khu ăn đêm nhộn nhịp với nhiều gian hàng đặc sản địa phương",
                AudioDescription = "Chợ Ẩm Thực Đêm Nguyễn Thượng Hiền mang đến trải nghiệm đường phố sôi động với nhiều món ăn nhanh và món vặt nổi tiếng.",
                Category = "Văn hóa ẩm thực",
                Rating = "9/10",
                Image = "offline.png",
                GalleryImages = ["offline.png", "route.png", "settings.png"],
                Address = "12 Nguyễn Thượng Hiền, Quận 3, TP.HCM",
                Phone = "(028) 3988 1155",
                Email = "chonguyenthuonghien@example.vn",
                Website = "amthucdemnguyenthuonghien.vn",
                EstablishedYear = "2015",
                RadiusText = "130m",
                GpsText = "10.781180, 106.684620",
                Latitude = 10.781180,
                Longitude = 106.684620,
                CategoryColor = Color.FromArgb("#F3E8FF"),
                CategoryTextColor = Color.FromArgb("#7C3AED")
            },
            new()
            {
                Id = "tram-thong-tin-du-lich-vo-van-tan",
                Name = "Trạm Thông Tin Du Lịch Võ Văn Tần",
                Description = "Điểm hỗ trợ khách du lịch với bản đồ, wifi và gợi ý tuyến tham quan",
                AudioDescription = "Trạm Thông Tin Du Lịch Võ Văn Tần hỗ trợ khách tra cứu địa điểm, tuyến đi bộ và các tiện ích công cộng lân cận.",
                Category = "Tiện ích",
                Rating = "8/10",
                Image = "location.png",
                GalleryImages = ["location.png", "map.png", "offline.png"],
                Address = "101 Võ Văn Tần, Quận 3, TP.HCM",
                Phone = "(028) 3822 6601",
                Email = "tramdulichvvt@example.vn",
                Website = "tramdulichvovan.vn",
                EstablishedYear = "2021",
                RadiusText = "70m",
                GpsText = "10.778330, 106.686780",
                Latitude = 10.778330,
                Longitude = 106.686780,
                CategoryColor = Color.FromArgb("#DCFCE7"),
                CategoryTextColor = Color.FromArgb("#15803D")
            },
            new()
            {
                Id = "tiem-banh-sai-gon-xua",
                Name = "Tiệm Bánh Sài Gòn Xưa",
                Description = "Tiệm bánh nhỏ với phong cách hoài niệm và các món ngọt truyền thống",
                AudioDescription = "Tiệm Bánh Sài Gòn Xưa phục vụ bánh da lợn, bánh bò và cà phê sữa trong không gian đậm chất Sài Gòn cũ.",
                Category = "Quán nổi tiếng",
                Rating = "8/10",
                Image = "history.png",
                GalleryImages = ["history.png", "map.png", "settings.png"],
                Address = "19 Bà Huyện Thanh Quan, Quận 3, TP.HCM",
                Phone = "(028) 3933 8808",
                Email = "banhsaigonxua@example.vn",
                Website = "tiembanhsaigonxua.vn",
                EstablishedYear = "2014",
                RadiusText = "88m",
                GpsText = "10.779430, 106.685930",
                Latitude = 10.779430,
                Longitude = 106.685930,
                CategoryColor = Color.FromArgb("#FEF3C7"),
                CategoryTextColor = Color.FromArgb("#B45309")
            }
        };
    }

    public IReadOnlyList<PlaceItem> GetPlaces() => _places;

    public PlaceItem? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _places.FirstOrDefault(item =>
            string.Equals(item.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<PlaceItem> SearchByName(string keyword, int maxResults = 6)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Array.Empty<PlaceItem>();

        var trimmedKeyword = keyword.Trim();

        return _places
            .Select(item => new
            {
                Place = item,
                Score = GetSearchScore(item, trimmedKeyword)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Place.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .Select(item => item.Place)
            .ToList();
    }

    public IReadOnlyList<MapPlacePoint> GetMapPoints()
    {
        return _places
            .Select(item => new MapPlacePoint
            {
                Id = item.Id,
                Title = item.Name,
                Description = item.Description,
                Address = item.Address,
                Category = item.Category,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Image = item.Image,
                GalleryImages = item.GalleryImages
            })
            .ToList();
    }

    private static int GetSearchScore(PlaceItem item, string keyword)
    {
        if (item.Name.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            return 300;

        if (item.Name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            return 220;

        if (item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return 180;

        if (item.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return 120;

        if (item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return 90;

        return 0;
    }

    public sealed class MapPlacePoint
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string Image { get; init; } = string.Empty;
        public IReadOnlyList<string> GalleryImages { get; init; } = Array.Empty<string>();
    }
}
