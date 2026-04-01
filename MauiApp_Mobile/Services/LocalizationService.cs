using System.ComponentModel;

namespace MauiApp_Mobile.Services;

public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private string _language = "vi";

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationService()
    {
        SeedExtendedTranslations();
    }

    private readonly Dictionary<string, Dictionary<string, string>> _texts = new()
    {
        ["vi"] = new()
        {
            ["Lang.Title"] = "Chào mừng đến với\nVietAudio Guide",
            ["Lang.Subtitle"] = "Thuyết minh tự động cho các điểm tham\nquan lịch sử Việt Nam",
            ["Lang.Choose"] = "Chọn ngôn ngữ của bạn",
            ["Lang.Start"] = "Bắt đầu khám phá ❯",
            ["Lang.Footer"] = "Bạn có thể thay đổi ngôn ngữ trong Cài đặt",

            ["Places.Title"] = "Địa điểm",
            ["Places.Search"] = "Tìm kiếm điểm...",
            ["Places.CountSuffix"] = "địa điểm",
            ["Places.Filter"] = "Lọc",
            ["Places.EmptyTitle"] = "Không tìm thấy địa điểm",
            ["Places.EmptySubtitle"] = "Thử nhập từ khóa khác",

            ["Map.Title"] = "Bản đồ",
            ["Map.Search"] = "Tìm kiếm địa điểm...",
            ["Map.SearchPoi"] = "Tìm theo tên POI...",
            ["Map.SearchAddress"] = "Tìm theo địa chỉ...",
            ["Map.Area"] = "Khu vực bản đồ",
            ["Map.LocateHint"] = "Chạm la bàn để lấy vị trí hiện tại",
            ["Map.ModePoi"] = "Tên POI",
            ["Map.ModeAddress"] = "Địa chỉ",
            ["Map.ResultsPoi"] = "Kết quả POI",
            ["Map.ResultsAddress"] = "Kết quả địa chỉ",

            ["History.Title"] = "Lịch sử",
            ["History.Subtitle"] = "Các địa điểm đã nghe thuyết minh",
            ["History.Count"] = "4 địa điểm",
            ["History.Total"] = "15p 55s tổng",
            ["History.Today"] = "Hôm nay",
            ["History.Yesterday"] = "Hôm qua",

            ["Settings.Title"] = "Cài đặt",
            ["Settings.Audio"] = "Âm thanh",
            ["Settings.Language"] = "Ngôn ngữ",
            ["Settings.LanguageValue"] = "Tiếng Việt ›",
            ["Settings.Voice"] = "Giọng đọc",
            ["Settings.VoiceValue"] = "Giọng nữ miền Nam ▼",
            ["Settings.Speed"] = "Tốc độ đọc",
            ["Settings.Volume"] = "Âm lượng",
            ["Settings.TestVoice"] = "🎙 Test giọng đọc",
            ["Settings.Gps"] = "GPS và Vị trí",
            ["Settings.Accuracy"] = "Độ chính xác GPS",
            ["Settings.AccuracyValue"] = "Cao",
            ["Settings.TriggerRadius"] = "Bán kính kích hoạt",
            ["Settings.AlertRadius"] = "Bán kính cảnh báo",
            ["Settings.WaitTime"] = "Thời gian chờ",
            ["Settings.Behavior"] = "Hành vi",
            ["Settings.AutoPlay"] = "Tự động phát khi vào vùng",
            ["Settings.NotifyNear"] = "Thông báo khi đến gần",
            ["Settings.BackgroundTracking"] = "Theo dõi ở nền",
            ["Settings.BatterySaver"] = "Chế độ tiết kiệm pin",
            ["Settings.Offline"] = "Chế độ Offline",
            ["Settings.Save"] = "💾 Lưu cài đặt",
            ["Settings.SaveSuccess"] = "Theme và các thiết lập demo đã được áp dụng.",
            ["Settings.ChooseLanguage"] = "CHỌN NGÔN NGỮ",
            ["Settings.Appearance"] = "Giao diện",
            ["Settings.ThemeHint"] = "Chọn theme phù hợp với hành trình của bạn. Áp dụng ngay lập tức trên toàn app.",
            ["Settings.ThemeEcoTitle"] = "Eco Light",
            ["Settings.ThemeEcoSubtitle"] = "Tươi, sáng và gần gũi cho bản đồ khám phá ban ngày.",
            ["Settings.ThemeMidnightTitle"] = "Midnight",
            ["Settings.ThemeMidnightSubtitle"] = "Độ tương phản dịu mắt hơn khi đi buổi tối hoặc ngoài trời tối.",
            ["Settings.ThemeHeritageTitle"] = "Heritage",
            ["Settings.ThemeHeritageSubtitle"] = "Tông ấm gợi cảm giác di sản và du lịch văn hóa.",

            ["Filter.Title"] = "DANH MỤC",
            ["Filter.All"] = "Tất cả",
            ["Filter.SignatureDish"] = "Món ăn đặc trưng",
            ["Filter.FamousRestaurant"] = "Quán nổi tiếng",
            ["Filter.Drinks"] = "Đồ uống",
            ["Filter.FoodCulture"] = "Văn hóa ẩm thực",
            ["Filter.Utility"] = "Tiện ích"
        },

        ["en"] = new()
        {
            ["Lang.Title"] = "Welcome to\nVietAudio Guide",
            ["Lang.Subtitle"] = "Automatic audio guide for historical\nsites in Vietnam",
            ["Lang.Choose"] = "Choose your language",
            ["Lang.Start"] = "Start exploring ❯",
            ["Lang.Footer"] = "You can change the language in Settings",

            ["Places.Title"] = "Places",
            ["Places.Search"] = "Search places...",
            ["Places.CountSuffix"] = "places",
            ["Places.Filter"] = "Filter",
            ["Places.EmptyTitle"] = "No places found",
            ["Places.EmptySubtitle"] = "Try another keyword",

            ["Map.Title"] = "Map",
            ["Map.Search"] = "Search places...",
            ["Map.SearchPoi"] = "Search by POI name...",
            ["Map.SearchAddress"] = "Search by address...",
            ["Map.Area"] = "Map area",
            ["Map.LocateHint"] = "Tap the compass to jump to your current location",
            ["Map.ModePoi"] = "POI name",
            ["Map.ModeAddress"] = "Address",
            ["Map.ResultsPoi"] = "POI results",
            ["Map.ResultsAddress"] = "Address results",

            ["History.Title"] = "History",
            ["History.Subtitle"] = "Places you listened to",
            ["History.Count"] = "4 places",
            ["History.Total"] = "15m 55s total",
            ["History.Today"] = "Today",
            ["History.Yesterday"] = "Yesterday",

            ["Settings.Title"] = "Settings",
            ["Settings.Audio"] = "Audio",
            ["Settings.Language"] = "Language",
            ["Settings.LanguageValue"] = "English ›",
            ["Settings.Voice"] = "Voice",
            ["Settings.VoiceValue"] = "Southern female voice ▼",
            ["Settings.Speed"] = "Reading speed",
            ["Settings.Volume"] = "Volume",
            ["Settings.TestVoice"] = "🎙 Test voice",
            ["Settings.Gps"] = "GPS & Location",
            ["Settings.Accuracy"] = "GPS accuracy",
            ["Settings.AccuracyValue"] = "High",
            ["Settings.TriggerRadius"] = "Trigger radius",
            ["Settings.AlertRadius"] = "Alert radius",
            ["Settings.WaitTime"] = "Wait time",
            ["Settings.Behavior"] = "Behavior",
            ["Settings.AutoPlay"] = "Auto play in zone",
            ["Settings.NotifyNear"] = "Notify when nearby",
            ["Settings.BackgroundTracking"] = "Background tracking",
            ["Settings.BatterySaver"] = "Battery saver mode",
            ["Settings.Offline"] = "Offline mode",
            ["Settings.Save"] = "💾 Save settings",
            ["Settings.SaveSuccess"] = "Your theme and demo settings have been applied.",
            ["Settings.ChooseLanguage"] = "CHOOSE LANGUAGE",
            ["Settings.Appearance"] = "Appearance",
            ["Settings.ThemeHint"] = "Pick the look that fits your trip. Changes apply instantly across the app.",
            ["Settings.ThemeEcoTitle"] = "Eco Light",
            ["Settings.ThemeEcoSubtitle"] = "Fresh and bright for daytime map exploration.",
            ["Settings.ThemeMidnightTitle"] = "Midnight",
            ["Settings.ThemeMidnightSubtitle"] = "Softer contrast for evening walks and dark surroundings.",
            ["Settings.ThemeHeritageTitle"] = "Heritage",
            ["Settings.ThemeHeritageSubtitle"] = "A warm palette inspired by culture trips and historic spaces.",

            ["Filter.Title"] = "CATEGORY",
            ["Filter.All"] = "All",
            ["Filter.SignatureDish"] = "Signature dishes",
            ["Filter.FamousRestaurant"] = "Famous restaurants",
            ["Filter.Drinks"] = "Drinks",
            ["Filter.FoodCulture"] = "Food culture",
            ["Filter.Utility"] = "Utilities"
        },

        ["cn"] = new()
        {
            ["Lang.Title"] = "欢迎来到\nVietAudio Guide",
            ["Lang.Subtitle"] = "越南历史景点自动语音导览",
            ["Lang.Choose"] = "选择您的语言",
            ["Lang.Start"] = "开始探索 ❯",
            ["Lang.Footer"] = "您可以在设置中更改语言",

            ["Places.Title"] = "地点",
            ["Places.Search"] = "搜索地点...",
            ["Places.CountSuffix"] = "个地点",
            ["Places.Filter"] = "筛选",
            ["Places.EmptyTitle"] = "未找到地点",
            ["Places.EmptySubtitle"] = "请尝试其他关键词",

            ["Map.Title"] = "地图",
            ["Map.Search"] = "搜索地点...",
            ["Map.Area"] = "地图区域",
            ["Map.LocateHint"] = "点击指南针即可定位到当前位置",

            ["History.Title"] = "历史记录",
            ["History.Subtitle"] = "已收听讲解的地点",
            ["History.Count"] = "4 个地点",
            ["History.Total"] = "总计 15分55秒",
            ["History.Today"] = "今天",
            ["History.Yesterday"] = "昨天",

            ["Settings.Title"] = "设置",
            ["Settings.Audio"] = "音频",
            ["Settings.Language"] = "语言",
            ["Settings.LanguageValue"] = "中文 ›",
            ["Settings.Voice"] = "语音",
            ["Settings.VoiceValue"] = "越南南部女声 ▼",
            ["Settings.Speed"] = "语速",
            ["Settings.Volume"] = "音量",
            ["Settings.TestVoice"] = "🎙 测试语音",
            ["Settings.Gps"] = "GPS 和位置",
            ["Settings.Accuracy"] = "GPS 精度",
            ["Settings.AccuracyValue"] = "高",
            ["Settings.TriggerRadius"] = "触发半径",
            ["Settings.AlertRadius"] = "提醒半径",
            ["Settings.WaitTime"] = "等待时间",
            ["Settings.Behavior"] = "行为",
            ["Settings.AutoPlay"] = "进入区域自动播放",
            ["Settings.NotifyNear"] = "靠近时通知",
            ["Settings.BackgroundTracking"] = "后台跟踪",
            ["Settings.BatterySaver"] = "省电模式",
            ["Settings.Offline"] = "离线模式",
            ["Settings.Save"] = "💾 保存设置",
            ["Settings.SaveSuccess"] = "主题和演示设置已应用。",
            ["Settings.ChooseLanguage"] = "选择语言",
            ["Settings.Appearance"] = "外观",
            ["Settings.ThemeHint"] = "选择适合旅程氛围的主题，切换后会立即应用到整个应用。",
            ["Settings.ThemeEcoTitle"] = "Eco Light",
            ["Settings.ThemeEcoSubtitle"] = "清新明亮，适合白天查看地图。",
            ["Settings.ThemeMidnightTitle"] = "Midnight",
            ["Settings.ThemeMidnightSubtitle"] = "夜间出行时更柔和、更护眼。",
            ["Settings.ThemeHeritageTitle"] = "Heritage",
            ["Settings.ThemeHeritageSubtitle"] = "温暖色调，适合文化与历史类旅程。",

            ["Filter.Title"] = "分类",
            ["Filter.All"] = "全部",
            ["Filter.SignatureDish"] = "特色菜",
            ["Filter.FamousRestaurant"] = "知名餐馆",
            ["Filter.Drinks"] = "饮品",
            ["Filter.FoodCulture"] = "饮食文化",
            ["Filter.Utility"] = "实用服务"
        },

        ["jp"] = new()
        {
            ["Lang.Title"] = "ようこそ\nVietAudio Guideへ",
            ["Lang.Subtitle"] = "ベトナムの歴史観光地の自動音声ガイド",
            ["Lang.Choose"] = "言語を選択してください",
            ["Lang.Start"] = "探索を始める ❯",
            ["Lang.Footer"] = "設定で言語を変更できます",

            ["Places.Title"] = "場所",
            ["Places.Search"] = "場所を検索...",
            ["Places.CountSuffix"] = "件の場所",
            ["Places.Filter"] = "絞り込み",
            ["Places.EmptyTitle"] = "場所が見つかりません",
            ["Places.EmptySubtitle"] = "別のキーワードを試してください",

            ["Map.Title"] = "地図",
            ["Map.Search"] = "場所を検索...",
            ["Map.Area"] = "地図エリア",
            ["Map.LocateHint"] = "コンパスをタップすると現在地へ移動します",

            ["History.Title"] = "履歴",
            ["History.Subtitle"] = "音声案内を聞いた場所",
            ["History.Count"] = "4 か所",
            ["History.Total"] = "合計 15分55秒",
            ["History.Today"] = "今日",
            ["History.Yesterday"] = "昨日",

            ["Settings.Title"] = "設定",
            ["Settings.Audio"] = "音声",
            ["Settings.Language"] = "言語",
            ["Settings.LanguageValue"] = "日本語 ›",
            ["Settings.Voice"] = "音声",
            ["Settings.VoiceValue"] = "南部女性音声 ▼",
            ["Settings.Speed"] = "読み上げ速度",
            ["Settings.Volume"] = "音量",
            ["Settings.TestVoice"] = "🎙 音声をテスト",
            ["Settings.Gps"] = "GPS と位置情報",
            ["Settings.Accuracy"] = "GPS 精度",
            ["Settings.AccuracyValue"] = "高",
            ["Settings.TriggerRadius"] = "起動半径",
            ["Settings.AlertRadius"] = "通知半径",
            ["Settings.WaitTime"] = "待機時間",
            ["Settings.Behavior"] = "動作",
            ["Settings.AutoPlay"] = "エリアに入ると自動再生",
            ["Settings.NotifyNear"] = "近づいたら通知",
            ["Settings.BackgroundTracking"] = "バックグラウンド追跡",
            ["Settings.BatterySaver"] = "省電力モード",
            ["Settings.Offline"] = "オフラインモード",
            ["Settings.Save"] = "💾 設定を保存",
            ["Settings.SaveSuccess"] = "テーマとデモ設定を適用しました。",
            ["Settings.ChooseLanguage"] = "言語を選択",
            ["Settings.Appearance"] = "外観",
            ["Settings.ThemeHint"] = "旅の雰囲気に合うテーマを選ぶと、アプリ全体にすぐ反映されます。",
            ["Settings.ThemeEcoTitle"] = "Eco Light",
            ["Settings.ThemeEcoSubtitle"] = "昼間の地図探索に合う、明るく爽やかな表示です。",
            ["Settings.ThemeMidnightTitle"] = "Midnight",
            ["Settings.ThemeMidnightSubtitle"] = "夜の散策や暗い場所でも目にやさしい表示です。",
            ["Settings.ThemeHeritageTitle"] = "Heritage",
            ["Settings.ThemeHeritageSubtitle"] = "文化遺産の旅に合う、あたたかみのある配色です。",

            ["Filter.Title"] = "カテゴリ",
            ["Filter.All"] = "すべて",
            ["Filter.SignatureDish"] = "名物料理",
            ["Filter.FamousRestaurant"] = "有名店",
            ["Filter.Drinks"] = "飲み物",
            ["Filter.FoodCulture"] = "食文化",
            ["Filter.Utility"] = "便利機能"
        },

        ["kr"] = new()
        {
            ["Lang.Title"] = "환영합니다\nVietAudio Guide",
            ["Lang.Subtitle"] = "베트남 역사 관광지를 위한 자동 오디오 가이드",
            ["Lang.Choose"] = "언어를 선택하세요",
            ["Lang.Start"] = "탐험 시작 ❯",
            ["Lang.Footer"] = "설정에서 언어를 변경할 수 있습니다",

            ["Places.Title"] = "장소",
            ["Places.Search"] = "장소 검색...",
            ["Places.CountSuffix"] = "개의 장소",
            ["Places.Filter"] = "필터",
            ["Places.EmptyTitle"] = "장소를 찾을 수 없습니다",
            ["Places.EmptySubtitle"] = "다른 키워드를 입력해보세요",

            ["Map.Title"] = "지도",
            ["Map.Search"] = "장소 검색...",
            ["Map.Area"] = "지도 영역",
            ["Map.LocateHint"] = "나침반을 누르면 현재 위치로 이동합니다",

            ["History.Title"] = "기록",
            ["History.Subtitle"] = "설명을 들은 장소",
            ["History.Count"] = "4곳",
            ["History.Total"] = "총 15분 55초",
            ["History.Today"] = "오늘",
            ["History.Yesterday"] = "어제",

            ["Settings.Title"] = "설정",
            ["Settings.Audio"] = "오디오",
            ["Settings.Language"] = "언어",
            ["Settings.LanguageValue"] = "한국어 ›",
            ["Settings.Voice"] = "음성",
            ["Settings.VoiceValue"] = "베트남 남부 여성 음성 ▼",
            ["Settings.Speed"] = "읽기 속도",
            ["Settings.Volume"] = "볼륨",
            ["Settings.TestVoice"] = "🎙 음성 테스트",
            ["Settings.Gps"] = "GPS 및 위치",
            ["Settings.Accuracy"] = "GPS 정확도",
            ["Settings.AccuracyValue"] = "높음",
            ["Settings.TriggerRadius"] = "트리거 반경",
            ["Settings.AlertRadius"] = "알림 반경",
            ["Settings.WaitTime"] = "대기 시간",
            ["Settings.Behavior"] = "동작",
            ["Settings.AutoPlay"] = "구역 진입 시 자동 재생",
            ["Settings.NotifyNear"] = "가까워지면 알림",
            ["Settings.BackgroundTracking"] = "백그라운드 추적",
            ["Settings.BatterySaver"] = "절전 모드",
            ["Settings.Offline"] = "오프라인 모드",
            ["Settings.Save"] = "💾 설정 저장",
            ["Settings.SaveSuccess"] = "테마와 데모 설정이 적용되었습니다.",
            ["Settings.ChooseLanguage"] = "언어 선택",
            ["Settings.Appearance"] = "테마",
            ["Settings.ThemeHint"] = "여행 분위기에 맞는 테마를 선택하면 앱 전체에 바로 적용됩니다.",
            ["Settings.ThemeEcoTitle"] = "Eco Light",
            ["Settings.ThemeEcoSubtitle"] = "낮 시간 지도 탐색에 어울리는 밝고 산뜻한 테마입니다.",
            ["Settings.ThemeMidnightTitle"] = "Midnight",
            ["Settings.ThemeMidnightSubtitle"] = "야간 이동이나 어두운 환경에서 눈부심을 줄여 줍니다.",
            ["Settings.ThemeHeritageTitle"] = "Heritage",
            ["Settings.ThemeHeritageSubtitle"] = "문화와 유산 여행에 잘 어울리는 따뜻한 색감입니다。",

            ["Filter.Title"] = "카테고리",
            ["Filter.All"] = "전체",
            ["Filter.SignatureDish"] = "대표 음식",
            ["Filter.FamousRestaurant"] = "유명 맛집",
            ["Filter.Drinks"] = "음료",
            ["Filter.FoodCulture"] = "음식 문화",
            ["Filter.Utility"] = "편의 기능"
        },

        ["fr"] = new()
        {
            ["Lang.Title"] = "Bienvenue sur\nVietAudio Guide",
            ["Lang.Subtitle"] = "Guide audio automatique pour les sites\nhistoriques du Vietnam",
            ["Lang.Choose"] = "Choisissez votre langue",
            ["Lang.Start"] = "Commencer l'exploration ❯",
            ["Lang.Footer"] = "Vous pouvez changer la langue dans les paramètres",

            ["Places.Title"] = "Lieux",
            ["Places.Search"] = "Rechercher un lieu...",
            ["Places.CountSuffix"] = "lieux",
            ["Places.Filter"] = "Filtrer",
            ["Places.EmptyTitle"] = "Aucun lieu trouvé",
            ["Places.EmptySubtitle"] = "Essayez un autre mot-clé",

            ["Map.Title"] = "Carte",
            ["Map.Search"] = "Rechercher un lieu...",
            ["Map.Area"] = "Zone de la carte",
            ["Map.LocateHint"] = "Touchez la boussole pour afficher votre position actuelle",

            ["History.Title"] = "Historique",
            ["History.Subtitle"] = "Lieux déjà écoutés",
            ["History.Count"] = "4 lieux",
            ["History.Total"] = "15 min 55 s au total",
            ["History.Today"] = "Aujourd'hui",
            ["History.Yesterday"] = "Hier",

            ["Settings.Title"] = "Paramètres",
            ["Settings.Audio"] = "Audio",
            ["Settings.Language"] = "Langue",
            ["Settings.LanguageValue"] = "Français ›",
            ["Settings.Voice"] = "Voix",
            ["Settings.VoiceValue"] = "Voix féminine du Sud ▼",
            ["Settings.Speed"] = "Vitesse de lecture",
            ["Settings.Volume"] = "Volume",
            ["Settings.TestVoice"] = "🎙 Tester la voix",
            ["Settings.Gps"] = "GPS et position",
            ["Settings.Accuracy"] = "Précision GPS",
            ["Settings.AccuracyValue"] = "Élevée",
            ["Settings.TriggerRadius"] = "Rayon d’activation",
            ["Settings.AlertRadius"] = "Rayon d’alerte",
            ["Settings.WaitTime"] = "Temps d’attente",
            ["Settings.Behavior"] = "Comportement",
            ["Settings.AutoPlay"] = "Lecture automatique dans la zone",
            ["Settings.NotifyNear"] = "Notifier à proximité",
            ["Settings.BackgroundTracking"] = "Suivi en arrière-plan",
            ["Settings.BatterySaver"] = "Mode économie d’énergie",
            ["Settings.Offline"] = "Mode hors ligne",
            ["Settings.Save"] = "💾 Enregistrer",
            ["Settings.SaveSuccess"] = "Le theme et les reglages de demonstration sont appliques.",
            ["Settings.ChooseLanguage"] = "CHOISIR LA LANGUE",
            ["Settings.Appearance"] = "Apparence",
            ["Settings.ThemeHint"] = "Choisissez le theme qui correspond a votre parcours. Le changement est immediat dans toute l'application.",
            ["Settings.ThemeEcoTitle"] = "Eco Light",
            ["Settings.ThemeEcoSubtitle"] = "Un style clair et frais pour explorer la carte en journee.",
            ["Settings.ThemeMidnightTitle"] = "Midnight",
            ["Settings.ThemeMidnightSubtitle"] = "Un contraste plus doux pour les sorties du soir et les lieux sombres.",
            ["Settings.ThemeHeritageTitle"] = "Heritage",
            ["Settings.ThemeHeritageSubtitle"] = "Une palette chaude inspiree des voyages culturels et patrimoniaux.",

            ["Filter.Title"] = "CATÉGORIE",
            ["Filter.All"] = "Tous",
            ["Filter.SignatureDish"] = "Plats emblématiques",
            ["Filter.FamousRestaurant"] = "Restaurants célèbres",
            ["Filter.Drinks"] = "Boissons",
            ["Filter.FoodCulture"] = "Culture culinaire",
            ["Filter.Utility"] = "Utilitaires"
        }
    };

    private void SeedExtendedTranslations()
    {
        UpsertTexts("vi", new Dictionary<string, string>
        {
            ["Map.ViewDetails"] = "Xem chi tiết",
            ["Map.NearestLabel"] = "Gần bạn nhất",
            ["Map.CurrentLocationTitle"] = "Vị trí hiện tại",
            ["Map.SearchResultTitle"] = "Kết quả tìm kiếm",
            ["Map.NearestPrefix"] = "Gần nhất",
            ["Map.AddressTapSearchHint"] = "Nhập địa chỉ rồi chạm tìm kiếm để tra cứu trực tuyến.",
            ["Map.TypeMorePoi"] = "Nhập ít nhất 2 ký tự để tìm POI.",
            ["Map.TypeMoreAddress"] = "Nhập ít nhất 3 ký tự để tìm địa chỉ."
        });

        UpsertTexts("en", new Dictionary<string, string>
        {
            ["Map.ViewDetails"] = "View details",
            ["Map.NearestLabel"] = "Nearest to you",
            ["Map.CurrentLocationTitle"] = "Current location",
            ["Map.SearchResultTitle"] = "Search result",
            ["Map.NearestPrefix"] = "Nearest",
            ["Map.AddressTapSearchHint"] = "Enter an address, then tap search to look it up online.",
            ["Map.TypeMorePoi"] = "Type at least 2 characters to search POIs.",
            ["Map.TypeMoreAddress"] = "Type at least 3 characters to search addresses."
        });

        UpsertTexts("cn", new Dictionary<string, string>
        {
            ["Map.SearchPoi"] = "按 POI 名称搜索...",
            ["Map.SearchAddress"] = "按地址搜索...",
            ["Map.ModePoi"] = "POI 名称",
            ["Map.ModeAddress"] = "地址",
            ["Map.ResultsPoi"] = "POI 结果",
            ["Map.ResultsAddress"] = "地址结果",
            ["Map.ViewDetails"] = "查看详情",
            ["Map.NearestLabel"] = "离您最近",
            ["Map.CurrentLocationTitle"] = "当前位置",
            ["Map.SearchResultTitle"] = "搜索结果",
            ["Map.NearestPrefix"] = "最近",
            ["Map.AddressTapSearchHint"] = "输入地址后点击搜索即可在线查询。",
            ["Map.TypeMorePoi"] = "请输入至少 2 个字符来搜索 POI。",
            ["Map.TypeMoreAddress"] = "请输入至少 3 个字符来搜索地址。"
        });

        UpsertTexts("jp", new Dictionary<string, string>
        {
            ["Map.SearchPoi"] = "POI 名で検索...",
            ["Map.SearchAddress"] = "住所で検索...",
            ["Map.ModePoi"] = "POI 名",
            ["Map.ModeAddress"] = "住所",
            ["Map.ResultsPoi"] = "POI 結果",
            ["Map.ResultsAddress"] = "住所結果",
            ["Map.ViewDetails"] = "詳細を見る",
            ["Map.NearestLabel"] = "現在地から最寄り",
            ["Map.CurrentLocationTitle"] = "現在地",
            ["Map.SearchResultTitle"] = "検索結果",
            ["Map.NearestPrefix"] = "最寄り",
            ["Map.AddressTapSearchHint"] = "住所を入力してから検索をタップするとオンライン検索します。",
            ["Map.TypeMorePoi"] = "POI を検索するには 2 文字以上入力してください。",
            ["Map.TypeMoreAddress"] = "住所を検索するには 3 文字以上入力してください。"
        });

        UpsertTexts("kr", new Dictionary<string, string>
        {
            ["Map.SearchPoi"] = "POI 이름으로 검색...",
            ["Map.SearchAddress"] = "주소로 검색...",
            ["Map.ModePoi"] = "POI 이름",
            ["Map.ModeAddress"] = "주소",
            ["Map.ResultsPoi"] = "POI 결과",
            ["Map.ResultsAddress"] = "주소 결과",
            ["Map.ViewDetails"] = "상세 보기",
            ["Map.NearestLabel"] = "가장 가까운 장소",
            ["Map.CurrentLocationTitle"] = "현재 위치",
            ["Map.SearchResultTitle"] = "검색 결과",
            ["Map.NearestPrefix"] = "가장 가까운 곳",
            ["Map.AddressTapSearchHint"] = "주소를 입력한 뒤 검색 버튼을 눌러 온라인으로 찾아보세요.",
            ["Map.TypeMorePoi"] = "POI 검색을 위해 2자 이상 입력하세요.",
            ["Map.TypeMoreAddress"] = "주소 검색을 위해 3자 이상 입력하세요."
        });

        UpsertTexts("fr", new Dictionary<string, string>
        {
            ["Map.SearchPoi"] = "Rechercher par nom de POI...",
            ["Map.SearchAddress"] = "Rechercher par adresse...",
            ["Map.ModePoi"] = "Nom du POI",
            ["Map.ModeAddress"] = "Adresse",
            ["Map.ResultsPoi"] = "Resultats POI",
            ["Map.ResultsAddress"] = "Resultats d'adresse",
            ["Map.ViewDetails"] = "Voir le detail",
            ["Map.NearestLabel"] = "Le plus proche",
            ["Map.CurrentLocationTitle"] = "Position actuelle",
            ["Map.SearchResultTitle"] = "Resultat de recherche",
            ["Map.NearestPrefix"] = "Plus proche",
            ["Map.AddressTapSearchHint"] = "Saisissez une adresse puis touchez rechercher pour lancer la recherche en ligne.",
            ["Map.TypeMorePoi"] = "Saisissez au moins 2 caracteres pour chercher un POI.",
            ["Map.TypeMoreAddress"] = "Saisissez au moins 3 caracteres pour chercher une adresse."
        });
    }

    private void UpsertTexts(string language, IReadOnlyDictionary<string, string> values)
    {
        if (!_texts.TryGetValue(language, out var target))
        {
            target = new Dictionary<string, string>();
            _texts[language] = target;
        }

        foreach (var pair in values)
        {
            target[pair.Key] = pair.Value;
        }
    }

    public string T(string key)
    {
        if (_texts.TryGetValue(Language, out var lang) && lang.TryGetValue(key, out var value))
            return value;

        if (_texts["vi"].TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }
}
