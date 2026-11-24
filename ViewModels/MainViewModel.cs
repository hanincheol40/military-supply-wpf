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

            // 병사 관리
            AddSoldierCommand = Command(() => service.SaveSoldier(SoldierForm(), false), "병사를 등록했습니다.");
            UpdateSoldierCommand = Command(() =>
            {
                if (SelectedSoldier == null || SoldierId != SelectedSoldier.Id)
                    throw new InvalidOperationException("병사를 선택하고 군번은 유지하세요.");
                service.SaveSoldier(SoldierForm(), true);
            }, "병사 정보를 수정했습니다.");
            NewSoldierCommand = new RelayCommand(() =>
            {
                SelectedSoldier = null;
                SoldierId = SoldierName = Unit = "";
                Rank = "이병";
            });

            // 재고 관리
            AddItemCommand = Command(() => service.SaveItem(ItemForm(), false), "품목을 등록했습니다.");
            UpdateItemCommand = Command(() =>
            {
                if (SelectedItem == null || ItemId != SelectedItem.Id)
                    throw new InvalidOperationException("품목을 선택하고 코드는 유지하세요.");
                service.SaveItem(ItemForm(), true);
            }, "재고를 조정했습니다.");
            NewItemCommand = new RelayCommand(() =>
            {
                SelectedItem = null;
                ItemId = ItemName = Category = "";
                StockQuantity = "0";
                Minimum = "10";
                Required = true;
            });

            // 지급·반납
            IssueCommand = Command(() => service.Issue(IssueSoldier?.Id ?? "", IssueItem?.Id ?? "", Positive(IssueQuantity), Serial),
                "장비를 지급하고 재고를 차감했습니다.");
            ReturnCommand = Command(() => service.Return(SelectedIssue?.Id ?? ""),
                "장비 반납 / 확정 취소를 처리하고 재고를 복원했습니다.");

            // 보급 요청
            RequestCommand = Command(() =>
            {
                if (RequestItem == null || string.IsNullOrWhiteSpace(Reason))
                    throw new InvalidOperationException("품목과 요청 사유를 입력하세요.");
                service.Request(RequestItem.Id, Positive(RequestQuantity), Reason);
            }, "보급 요청을 등록했습니다.");
            AdvanceCommand = Command(() => service.Advance(SelectedRequest?.Id ?? ""),
                "요청을 처리했습니다. 입고 완료 시 재고가 증가합니다.");

            // 장비 구성: 끌어 놓기 → 지급 확정 → 지급 완료
            ConfirmLoadoutCommand = new RelayCommand(() => Run(() =>
            {
                if (IssueSoldier == null)
                    throw new InvalidOperationException("병사를 먼저 선택하세요.");

                var list = EquipmentSlots.Where(s => s.HasDraft)
                                         .Select(s => new IssueDraft(s.Draft!.Id, s.Serial))
                                         .ToList();
                service.ConfirmLoadout(IssueSoldier.Id, list);
                drafts.Clear();
                Refresh();
                Status = "지급 확정 · 재고 예약 완료. 장비는 반투명 상태이며 실제 인도 후 지급 완료를 누르세요.";
            }));
            CompleteDeliveryCommand = Command(() => service.CompleteDelivery(IssueSoldier?.Id ?? ""),
                "지급 완료 · 장비가 선명한 장착 상태로 변경되었습니다.");
            ClearLoadoutCommand = new RelayCommand(() =>
            {
                drafts.Clear();
                RefreshEquipment();
                Status = "구성 중인 장비를 비웠습니다. 확정/완료된 장비는 유지됩니다.";
            });
            StageItemCommand = new ParameterCommand(p =>
            {
                if (p is StockItem item)
                    StageItem(item, EquipmentCatalog.SlotFor(item));
            });
            RemoveDraftCommand = new ParameterCommand(p =>
            {
                if (p is EquipmentSlotViewModel slot)
                {
                    drafts.Remove(slot.Key);
                    RefreshEquipment();
                }
            });
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

        private static int Nonnegative(string value) =>
            int.TryParse(value, out int n) && n >= 0
                ? n
                : throw new InvalidOperationException("수량과 최소재고는 0 이상의 정수로 입력하세요.");

        private static int Positive(string value)
        {
            var n = Nonnegative(value);
            return n > 0 ? n : throw new InvalidOperationException("수량은 1 이상으로 입력하세요.");
        }

        // 데이터가 바뀌면 화면에 묶인 목록과 숫자를 다시 읽게 한다
        public void Refresh()
        {
            RefreshEquipment();
            Changed(nameof(RequestItems));
            Changed(nameof(RequestStockSummary));
            if (RequestItem != null)
                RequestItem = service.Data.Items.FirstOrDefault(x => x.Id == RequestItem.Id && (!ShowShortageOnly || x.IsShortage));

            foreach (var name in new[]
            {
                nameof(Soldiers), nameof(Items), nameof(AllSoldiers), nameof(AllItems),
                nameof(Issues), nameof(Stats), nameof(History),
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

        private string SoldierLabel(string id) => service.Data.Soldiers.First(x => x.Id == id).Display;
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

        // 병사·재고 목록 (검색어로 거른다)
        private string search = "";
        public string Search
        {
            get => search;
            set
            {
                if (Set(ref search, value))
                {
                    Changed(nameof(Soldiers));
                    Changed(nameof(Items));
                }
            }
        }

        public IEnumerable<Soldier> Soldiers => service.Data.Soldiers
            .Where(x => (x.Id + x.Display).Contains(Search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        public IEnumerable<StockItem> Items => service.Data.Items
            .Where(x => (x.Id + x.Name + x.Category).Contains(Search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 콤보박스용: 검색과 상관없이 전체 목록
        public IEnumerable<Soldier> AllSoldiers => service.Data.Soldiers;
        public IEnumerable<StockItem> AllItems => service.Data.Items;

        // 표에서 고르면 아래 입력 칸에 채워 넣는다
        private Soldier? selectedSoldier;
        public Soldier? SelectedSoldier
        {
            get => selectedSoldier;
            set
            {
                Set(ref selectedSoldier, value);
                if (value == null) return;

                SoldierId = value.Id;
                SoldierName = value.Name;
                Rank = value.Rank;
                Unit = value.Unit;
            }
        }

        private StockItem? selectedItem;
        public StockItem? SelectedItem
        {
            get => selectedItem;
            set
            {
                Set(ref selectedItem, value);
                if (value == null) return;

                ItemId = value.Id;
                ItemName = value.Name;
                Category = value.Category;
                StockQuantity = value.Quantity.ToString();
                Minimum = value.Minimum.ToString();
                Required = value.Required;
            }
        }

        private string soldierId = "", soldierName = "", rank = "이병", unit = "";
        private string itemId = "", itemName = "", category = "", stockQuantity = "0", minimum = "10";
        private bool required = true;

        public string SoldierId { get => soldierId; set => Set(ref soldierId, value); }
        public string SoldierName { get => soldierName; set => Set(ref soldierName, value); }
        public string Rank { get => rank; set => Set(ref rank, value); }
        public string Unit { get => unit; set => Set(ref unit, value); }

        public string ItemId { get => itemId; set => Set(ref itemId, value); }
        public string ItemName { get => itemName; set => Set(ref itemName, value); }
        public string Category { get => category; set => Set(ref category, value); }
        public string StockQuantity { get => stockQuantity; set => Set(ref stockQuantity, value); }
        public string Minimum { get => minimum; set => Set(ref minimum, value); }
        public bool Required { get => required; set => Set(ref required, value); }

        private Soldier SoldierForm() => new(SoldierId.Trim(), SoldierName.Trim(), Rank.Trim(), Unit.Trim());
        private StockItem ItemForm() => new(ItemId.Trim(), ItemName.Trim(), Category.Trim(), Nonnegative(StockQuantity), Nonnegative(Minimum), Required);

        public ICommand AddSoldierCommand { get; }
        public ICommand UpdateSoldierCommand { get; }
        public ICommand NewSoldierCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand UpdateItemCommand { get; }
        public ICommand NewItemCommand { get; }

        // 지급·반납
        private Soldier? issueSoldier;
        public Soldier? IssueSoldier
        {
            get => issueSoldier;
            set
            {
                // 병사를 바꾸면 구성 중이던 장비는 비운다
                if (Set(ref issueSoldier, value))
                {
                    drafts.Clear();
                    RefreshEquipment();
                }
            }
        }

        private StockItem? issueItem;
        public StockItem? IssueItem { get => issueItem; set => Set(ref issueItem, value); }

        private string issueQuantity = "1", serial = "";
        public string IssueQuantity { get => issueQuantity; set => Set(ref issueQuantity, value); }
        public string Serial { get => serial; set => Set(ref serial, value); }

        private IssueRow? selectedIssue;
        public IssueRow? SelectedIssue { get => selectedIssue; set => Set(ref selectedIssue, value); }

        public IEnumerable<IssueRow> Issues => service.Data.Issues
            .OrderByDescending(x => x.Date)
            .Select(x => new IssueRow(x.Id, SoldierLabel(x.SoldierId), ItemLabel(x.ItemId), x.Quantity, x.Serial, x.Date, IssueStatus(x)))
            .ToList();

        private static string IssueStatus(Issue x)
        {
            if (x.ReturnedAt != null)
                return x.AwaitingDelivery ? "확정 취소" : "반납 완료";
            return x.AwaitingDelivery ? "지급 확정" : "지급 완료";
        }

        // 보급 요청
        private RequestRow? selectedRequest;
        public RequestRow? SelectedRequest { get => selectedRequest; set => Set(ref selectedRequest, value); }

        private StockItem? requestItem;
        public StockItem? RequestItem
        {
            get => requestItem;
            set
            {
                var changedId = requestItem?.Id != value?.Id;
                if (Set(ref requestItem, value))
                {
                    // 품목을 새로 고르면 모자란 만큼을 기본 수량으로
                    if (changedId && value != null)
                        RequestQuantity = (value.IsShortage ? value.Shortage : 1).ToString();
                    Changed(nameof(RequestItemSummary));
                }
            }
        }

        private bool showShortageOnly = true;
        public bool ShowShortageOnly
        {
            get => showShortageOnly;
            set
            {
                if (Set(ref showShortageOnly, value))
                {
                    if (value && RequestItem is { IsShortage: false })
                        RequestItem = null;
                    Changed(nameof(RequestItems));
                }
            }
        }

        public IEnumerable<StockItem> RequestItems => service.Data.Items
            .Where(x => !ShowShortageOnly || x.IsShortage)
            .OrderByDescending(x => x.IsShortage)
            .ThenByDescending(x => x.Shortage)
            .ThenBy(x => x.Name)
            .ToList();

        public string RequestStockSummary =>
            $"부족 품목 {service.Data.Items.Count(x => x.IsShortage)}종 · 최소재고 도달에 필요한 수량 {service.Data.Items.Sum(x => (long)x.Shortage)}개";
        public string RequestItemSummary => RequestItem?.SupplySummary ?? "품목을 선택하면 현재 재고와 부족 수량을 확인할 수 있습니다.";

        private string requestQuantity = "10", reason = "";
        public string RequestQuantity { get => requestQuantity; set => Set(ref requestQuantity, value); }
        public string Reason { get => reason; set => Set(ref reason, value); }

        // 통계, 변경 이력
        public IEnumerable<StatRow> Stats => service.Data.Items.Select(i => new StatRow(
            i.Name, i.Category, i.Quantity,
            service.Data.Issues.Where(x => x.ItemId == i.Id && x.ReturnedAt == null && !x.AwaitingDelivery).Sum(x => x.Quantity),
            service.Data.Issues.Where(x => x.ItemId == i.Id && x.ReturnedAt == null && x.AwaitingDelivery).Sum(x => x.Quantity),
            i.Minimum, i.Status)).ToList();
        public IEnumerable<AuditEntry> History => service.Data.History.OrderByDescending(x => x.Date).ToList();

        public ICommand IssueCommand { get; }
        public ICommand ReturnCommand { get; }
        public ICommand RequestCommand { get; }
        public ICommand AdvanceCommand { get; }

        // 장비 구성: 장착 위치별로 끌어 놓은 품목 ID (확정 전)
        private readonly Dictionary<string, string> drafts = [];

        public List<EquipmentSlotViewModel> EquipmentSlots { get; } =
            EquipmentCatalog.Slots.Select(x => new EquipmentSlotViewModel(x)).ToList();

        public IEnumerable<StockItem> EquipmentItems => service.Data.Items.Where(x => EquipmentCatalog.SlotFor(x) != null).ToList();

        public string LoadoutSummary =>
            $"구성 중 {EquipmentSlots.Count(x => x.HasDraft)}종  /  확정 대기 {EquipmentSlots.Count(x => x.Active?.AwaitingDelivery == true)}종  /  장착 {EquipmentSlots.Count(x => x.Active is { AwaitingDelivery: false })}종";

        // 병사를 골랐고, 맞는 위치이고, 재고가 있고, 그 위치가 비어 있어야 끌어 놓을 수 있다
        public bool CanStageItem(StockItem item, string? target) =>
            IssueSoldier != null
            && target != null
            && EquipmentCatalog.SlotFor(item) == target
            && service.Data.Items.Any(x => x.Id == item.Id && x.Quantity > 0)
            && !service.Data.Issues.Any(x => x.SoldierId == IssueSoldier.Id
                                          && x.ReturnedAt == null
                                          && EquipmentCatalog.SlotFor(service.Data.Items.First(i => i.Id == x.ItemId)) == target);

        public void StageItem(StockItem item, string? target) => Run(() =>
        {
            if (!CanStageItem(item, target))
                throw new InvalidOperationException("병사를 선택하고 재고·장착 위치를 확인하세요. 이미 확정/장착된 위치는 반납 또는 취소 후 교체할 수 있습니다.");

            drafts[target!] = item.Id;
            RefreshEquipment();
            Status = $"{item.Name} 구성 중 · 지급 확정 후 반투명 인도 대기 상태가 됩니다.";
        });

        private void RefreshEquipment()
        {
            foreach (var slot in EquipmentSlots)
            {
                var draft = drafts.TryGetValue(slot.Key, out var id)
                    ? service.Data.Items.FirstOrDefault(x => x.Id == id)
                    : null;
                var active = service.Data.Issues.FirstOrDefault(x => x.SoldierId == IssueSoldier?.Id
                                                                  && x.ReturnedAt == null
                                                                  && EquipmentCatalog.SlotFor(service.Data.Items.First(i => i.Id == x.ItemId)) == slot.Key);
                slot.Update(draft, active, active == null ? null : ItemLabel(active.ItemId));
            }

            Changed(nameof(EquipmentItems));
            Changed(nameof(LoadoutSummary));
        }

        public ICommand ConfirmLoadoutCommand { get; }
        public ICommand CompleteDeliveryCommand { get; }
        public ICommand ClearLoadoutCommand { get; }
        public ICommand StageItemCommand { get; }
        public ICommand RemoveDraftCommand { get; }
    }
}
