using System.ComponentModel;

namespace MilitarySupply.Views
{
    // View 확인용 임시 데이터입니다. 화면 배치와 바인딩 이름만 확인하기 위한 가상 값이며,
    // ViewModel 커밋에서 MainViewModel로 교체한 뒤 삭제합니다.
    public sealed class PreviewData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // ===== 공통 =====
        public string Today => DateTime.Today.ToString("yyyy. MM. dd");
        public string Status => "View 미리보기 · 임시 데이터";

        public List<MenuEntry> Menu { get; } =
        [
            new("", "대시보드"), new("", "병사 관리"), new("", "재고 관리"),
            new("", "장비 지급"), new("", "보급 요청"), new("", "통계"),
        ];

        static readonly string[] Subtitles =
        [
            "병력과 보급 현황을 한눈에 확인하세요.",
            "병사 기본 정보와 소속을 관리합니다.",
            "가용 재고와 품목별 최소 보유 기준을 관리합니다.",
            "병사에게 장비를 지급하고 반납 이력을 확인합니다.",
            "요청 등록부터 승인, 입고 완료까지 관리합니다.",
            "실시간 보급 지표와 모든 변경 이력을 확인합니다.",
        ];

        int pageIndex;
        public int PageIndex
        {
            get => pageIndex;
            set
            {
                if (pageIndex == value) return;
                pageIndex = value;
                PropertyChanged?.Invoke(this, new(nameof(PageIndex)));
                PropertyChanged?.Invoke(this, new(nameof(PageTitle)));
                PropertyChanged?.Invoke(this, new(nameof(PageSubtitle)));
            }
        }
        public string PageTitle => Menu[Math.Clamp(PageIndex, 0, 5)].Title;
        public string PageSubtitle => Subtitles[Math.Clamp(PageIndex, 0, 5)];

        // ===== 샘플 목록 =====
        public List<SampleSoldier> Soldiers { get; } =
        [
            new("24-76001234", "김민준", "상병", "1소대"),
            new("24-76005678", "이서준", "일병", "2소대"),
            new("25-76009012", "박도윤", "이병", "3소대"),
        ];
        public List<SampleSoldier> AllSoldiers => Soldiers;

        public List<SampleItem> Items { get; } =
        [
            new("EQ-001", "방탄헬멧", "개인장비", 42, 20, true),
            new("EQ-002", "전투복", "피복", 8, 30, true),
            new("EQ-003", "전투화", "피복", 25, 30, true),
            new("EQ-004", "전투조끼", "개인장비", 15, 10, false),
        ];
        public IEnumerable<SampleItem> LowItems => Items.Where(x => x.IsShortage);
        public IEnumerable<SampleItem> EquipmentItems => Items;
        public IEnumerable<SampleItem> RequestItems => Items.Where(x => x.IsShortage);

        public List<SampleRequest> Requests { get; } =
        [
            new(DateTime.Today, "전투복", 22, "신병 전입 대비", "승인 대기"),
            new(DateTime.Today.AddDays(-2), "전투화", 5, "최소재고 보충", "입고 완료"),
        ];
        public List<SampleRequest> RecentRequests => Requests;

        public List<SampleIssue> Issues { get; } =
        [
            new(DateTime.Now.AddHours(-3), "김민준 상병 · 1소대", "전투복", 1, "", "지급 완료"),
            new(DateTime.Now.AddHours(-1), "김민준 상병 · 1소대", "방탄헬멧", 1, "H-0012", "지급 확정"),
        ];

        public List<SampleStat> Stats { get; } =
        [
            new("방탄헬멧", "개인장비", 42, 3, 1, 20, "정상"),
            new("전투복", "피복", 8, 12, 0, 30, "부족"),
        ];

        public List<SampleHistory> History { get; } =
        [
            new(DateTime.Now.AddHours(-1), "지급 확정", "김민준 · 방탄헬멧 1개"),
            new(DateTime.Now.AddHours(-3), "지급 완료", "김민준 · 전투복 1개"),
        ];

        // ===== 대시보드 =====
        public List<Metric> Metrics { get; } =
        [
            new("", "총 병력", "3명", "등록된 전체 인원", "#1E2124"),
            new("", "장비 지급률", "33%", "필수장비 전체 지급 인원 기준", "#0B50D0"),
            new("", "재고 부족 품목", "2건", "최소재고 미만", "#7A4D00"),
            new("", "보고 대기건", "1건", "보급 요청 승인 대기", "#1E2124"),
        ];
        public string RateText => "33%";
        public string DonutPath => "M 100,17 A 83,83 0 0,1 171.9,141.5";
        public int CompleteCount => 1;
        public int IncompleteCount => 2;

        // ===== 장비 지급 =====
        public SampleSoldier? IssueSoldier { get; set; }
        public List<SampleSlot> EquipmentSlots { get; } =
        [
            new("Head", "머리 · 헬멧", "방탄헬멧", "지급 확정 · 인도 대기", "#7A4D00", 0.48),
            new("Armor", "몸통 · 방탄복", "장비를 끌어 놓으세요", "미장착", "#464C53", 0.10),
            new("Vest", "가슴 · 전투조끼", "전투조끼", "구성 중", "#7A4D00", 0.30),
            new("Pack", "등 · 군장", "장비를 끌어 놓으세요", "미장착", "#464C53", 0.10),
            new("Boots", "발 · 전투화", "전투화", "지급 완료 · 장착", "#1A662B", 1),
            new("Weapon", "총기", "장비를 끌어 놓으세요", "미장착", "#464C53", 0.10),
            new("Uniform", "전투복", "전투복", "지급 완료 · 장착", "#1A662B", 1),
            new("Pouch", "허리 · 탄알집", "장비를 끌어 놓으세요", "미장착", "#464C53", 0.10),
        ];
        public string LoadoutSummary => "구성 중 1종  /  확정 대기 1종  /  장착 2종";

        // ===== 보급 요청 =====
        public bool ShowShortageOnly { get; set; } = true;
        public SampleItem? RequestItem { get; set; }
        public string RequestStockSummary => "부족 품목 2종 · 최소재고 도달에 필요한 수량 27개";
        public string RequestItemSummary => "품목을 선택하면 현재 재고와 부족 수량을 확인할 수 있습니다.";

        // ===== 통계 =====
        public long TotalAvailable => Items.Sum(x => (long)x.Quantity);
        public long TotalIssued => 15;
        public int RequiredCount => Items.Count(x => x.Required);
        public string StoragePath => "(미리보기 · 저장소 미연결)";
    }

    public record MenuEntry(string Icon, string Title);
    public record Metric(string Icon, string Title, string Value, string Note, string Color);
    public record SampleSoldier(string Id, string Name, string Rank, string Unit)
    {
        public string Display => $"{Name} {Rank} · {Unit}";
    }
    public record SampleItem(string Id, string Name, string Category, int Quantity, int Minimum, bool Required)
    {
        public string Status => Quantity < Minimum ? "부족" : "정상";
        public string Requirement => Required ? "필수" : "선택";
        public bool IsShortage => Quantity < Minimum;
        public string SupplySummary => $"현재 {Quantity}개 / 최소 {Minimum}개";
    }
    public record SampleRequest(DateTime Date, string Item, int Quantity, string Reason, string Status);
    public record SampleIssue(DateTime Date, string Soldier, string Item, int Quantity, string Serial, string Status);
    public record SampleStat(string Item, string Category, int Available, int Issued, int Reserved, int Minimum, string Status);
    public record SampleHistory(DateTime Date, string Action, string Detail);
    public record SampleSlot(string Key, string Label, string ItemName, string State, string StateColor, double Opacity)
    {
        public bool HasDraft => State == "구성 중";
        public string Serial { get; set; } = "";
    }
}
