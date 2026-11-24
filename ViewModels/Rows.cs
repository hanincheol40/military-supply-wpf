namespace MilitarySupply.ViewModels
{
    // 메뉴, 대시보드 카드, 표(DataGrid)의 한 줄
    public record MenuEntry(string Icon, string Title);
    public record Metric(string Icon, string Title, string Value, string Note, string Color);
    public record RequestRow(string Id, string Item, int Quantity, string Reason, string Status, DateTime Date);
    public record IssueRow(string Id, string Soldier, string Item, int Quantity, string Serial, DateTime Date, string Status);
    public record StatRow(string Item, string Category, int Available, int Issued, int Reserved, int Minimum, string Status);
}
