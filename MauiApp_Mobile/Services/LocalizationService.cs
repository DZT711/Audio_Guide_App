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
            ["Map.Area"] = "Khu vực bản đồ",

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
            ["Map.Area"] = "Map area",

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

            ["Filter.Title"] = "CATÉGORIE",
            ["Filter.All"] = "Tous",
            ["Filter.SignatureDish"] = "Plats emblématiques",
            ["Filter.FamousRestaurant"] = "Restaurants célèbres",
            ["Filter.Drinks"] = "Boissons",
            ["Filter.FoodCulture"] = "Culture culinaire",
            ["Filter.Utility"] = "Utilitaires"
        }
    };

    public string T(string key)
    {
        if (_texts.TryGetValue(Language, out var lang) && lang.TryGetValue(key, out var value))
            return value;

        if (_texts["vi"].TryGetValue(key, out var fallback))
            return fallback;

        return key;
    }
}