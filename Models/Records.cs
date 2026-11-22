namespace MilitarySupply.Models
{
    public record Soldier(string Id, string Name, string Rank, string Unit)
    {
        public string Display => $"{Name} {Rank} · {Unit}";
    }

    public record StockItem(string Id, string Name, string Category, int Quantity, int Minimum, bool Required)
    {
        public string Status => Quantity < Minimum ? "부족" : "정상";
        public string Requirement => Required ? "필수" : "선택";
        public int Shortage => Math.Max(0, Minimum - Quantity);
        public bool IsShortage => Shortage > 0;

        public string SupplySummary =>
            $"현재 {Quantity}개 / 최소 {Minimum}개 · {(IsShortage ? $"{Shortage}개 부족" : "재고 정상")}";

        public string Display =>
            $"{Name} · 가용 {Quantity}개" + (IsShortage ? $" · {Shortage}개 부족" : "");
    }

    public record Issue(
        string Id,
        string SoldierId,
        string ItemId,
        int Quantity,
        string Serial,
        DateTime Date,
        DateTime? ReturnedAt,
        bool AwaitingDelivery = false);

    public record IssueDraft(string ItemId, string Serial);

    public record SupplyRequest(string Id, string ItemId, int Quantity, string Reason, string Status, DateTime Date);

    public record AuditEntry(DateTime Date, string Action, string Detail);

    public class SupplyData
    {
        public int Version { get; set; } = 1;
        public List<Soldier> Soldiers { get; set; } = [];
        public List<StockItem> Items { get; set; } = [];
        public List<Issue> Issues { get; set; } = [];
        public List<SupplyRequest> Requests { get; set; } = [];
        public List<AuditEntry> History { get; set; } = [];
    }
}
