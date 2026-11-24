using System.Windows.Input;
using MilitarySupply.Models;
using MilitarySupply.Services;

namespace MilitarySupply.ViewModels
{
    public sealed class MainViewModel : Observable
    {
        private readonly SupplyService service;
        private readonly IUserDialogs dialogs;

        public MainViewModel(SupplyService service, IUserDialogs dialogs)
        {
            this.service = service;
            this.dialogs = dialogs;
        }

        // 실행 → 화면 새로 고침 → 아래 상태 글자 바꾸기
        private ICommand Command(Action action, string message) =>
            new RelayCommand(() => Run(() =>
            {
                action();
                Refresh();
                Status = message;
            }));

        // 실패하면 상태 글자와 메시지 창으로 알린다
        private void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Status = "처리 실패 · " + ex.Message;
                dialogs.ShowError(ex.Message);
            }
        }

        // 데이터가 바뀌면 화면에 묶인 목록과 숫자를 다시 읽게 한다
        public void Refresh()
        {
            foreach (var name in new[]
            {
                nameof(Requests), nameof(RecentRequests), nameof(Metrics), nameof(LowItems),
                nameof(Rate), nameof(RateText), nameof(DonutPath),
                nameof(RequiredCount), nameof(CompleteCount), nameof(IncompleteCount),
                nameof(TotalAvailable), nameof(TotalIssued),
            })
                Changed(name);
        }

        // 왼쪽 메뉴와 페이지 제목
        public List<MenuEntry> Menu { get; } =
        [
            new("", "대시보드"),
            new("", "병사 관리"),
            new("", "재고 관리"),
            new("", "장비 지급"),
            new("", "보급 요청"),
            new("", "통계"),
        ];

        private static readonly string[] Subtitles =
        [
            "병력과 보급 현황을 한눈에 확인하세요.",
            "병사 기본 정보와 소속을 관리합니다.",
            "가용 재고와 품목별 최소 보유 기준을 관리합니다.",
            "병사에게 장비를 지급하고 반납 이력을 확인합니다.",
            "요청 등록부터 승인, 입고 완료까지 관리합니다.",
            "실시간 보급 지표와 모든 변경 이력을 확인합니다.",
        ];

        private int pageIndex;
        public int PageIndex
        {
            get => pageIndex;
            set
            {
                if (Set(ref pageIndex, value))
                {
                    Changed(nameof(PageTitle));
                    Changed(nameof(PageSubtitle));
                }
            }
        }

        public string PageTitle => Menu[Math.Clamp(PageIndex, 0, 5)].Title;
        public string PageSubtitle => Subtitles[Math.Clamp(PageIndex, 0, 5)];

        public string Today => DateTime.Today.ToString("yyyy. MM. dd");
        public string StoragePath => service.FilePath;

        private string status = "준비 완료 · 가상 데모 데이터 · 모든 변경 사항은 자동 저장됩니다.";
        public string Status
        {
            get => status;
            set => Set(ref status, value);
        }

        private string ItemLabel(string id) => service.Data.Items.First(x => x.Id == id).Name;

        // 대시보드
        public double Rate => service.Rate;
        public string RateText => $"{Rate:0}%";

        public int RequiredCount => service.Data.Items.Count(x => x.Required);
        public int CompleteCount => RequiredCount == 0 ? 0 : service.Data.Soldiers.Count(s =>
            service.Data.Items.Where(x => x.Required).All(i =>
                service.Data.Issues.Any(x => x.SoldierId == s.Id && x.ItemId == i.Id && x.ReturnedAt == null && !x.AwaitingDelivery)));
        public int IncompleteCount => service.Data.Soldiers.Count - CompleteCount;

        public long TotalAvailable => service.Data.Items.Sum(x => (long)x.Quantity);
        public long TotalIssued => service.Data.Issues.Where(x => x.ReturnedAt == null && !x.AwaitingDelivery).Sum(x => (long)x.Quantity);

        public IEnumerable<StockItem> LowItems => service.Data.Items.Where(x => x.Quantity < x.Minimum).ToList();

        public IEnumerable<RequestRow> Requests => service.Data.Requests
            .OrderByDescending(x => x.Date)
            .Select(x => new RequestRow(x.Id, ItemLabel(x.ItemId), x.Quantity, x.Reason, x.Status, x.Date))
            .ToList();
        public IEnumerable<RequestRow> RecentRequests => Requests.Take(5);

        public IEnumerable<Metric> Metrics => new[]
        {
            new Metric("", "총 병력", $"{service.Data.Soldiers.Count}명", "등록된 전체 인원", "#1E2124"),
            new Metric("", "장비 지급률", RateText, "필수장비 전체 지급 인원 기준", "#0B50D0"),
            new Metric("", "재고 부족 품목", $"{LowItems.Count()}건", "최소재고 미만", "#7A4D00"),
            new Metric("", "보고 대기건", $"{service.Data.Requests.Count(x => x.Status == "승인 대기")}건", "보급 요청 승인 대기", "#1E2124"),
        };

        // 도넛 차트 원호 (200 x 200 기준). 0%는 시작점만, 100%는 거의 한 바퀴로 그린다
        public string DonutPath
        {
            get
            {
                if (Rate <= 0) return "M 100,17";

                var angle = Math.Min(Rate, 99.9999) / 100 * Math.PI * 2;
                var x = 100 + 83 * Math.Sin(angle);
                var y = 100 - 83 * Math.Cos(angle);
                return FormattableString.Invariant($"M 100,17 A 83,83 0 {(Rate > 50 ? 1 : 0)},1 {x},{y}");
            }
        }
    }
}
