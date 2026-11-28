using MilitarySupply.Models;
using MilitarySupply.Services;
using System.IO.Compression;
using System.Text.Json;
using System.IO;
using MilitarySupply.ViewModels;

// 실행: dotnet run --project Tests/MilitarySupply.Tests.csproj
// 테스트는 위에서부터 순서대로 같은 데이터를 이어서 쓰므로 순서를 바꾸면 안 된다.

// ---------- 준비 ----------

// 매번 새 임시 폴더에서 시작한다 (인자로 폴더를 주면 그 아래에 만든다)
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "MilitarySupplyTests");
var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);

int passed = 0;

void Test(string name, Action action)
{
    action();
    passed++;
    Console.WriteLine($"PASS {name}");
}

void Assert(bool test)
{
    if (!test)
        throw new Exception("Assertion failed");
}

// action이 예외를 던져야 통과
void Reject(Action action)
{
    bool failed = false;
    try { action(); }
    catch { failed = true; }
    Assert(failed);
}

var dbPath = Path.Combine(folder, "data.json");
var service = new SupplyService(dbPath);

// ---------- 병사·재고·지급·보급 (SupplyService) ----------

Test("Demo data and exact dashboard rate", () =>
{
    Assert(service.Data.Soldiers.Count == 6);
    Assert(Math.Abs(service.Rate - 100.0 * 5 / 6) < .0001);
    Assert(service.Data.Items.Count(x => x.Status == "부족") == 4);
});

Test("Register soldier and reject duplicate", () =>
{
    service.SaveSoldier(new("TEST-01", "테스트", "이병", "시험소대"), false);
    Reject(() => service.SaveSoldier(new("TEST-01", "테스트", "이병", "시험소대"), false));
});

Test("Update soldier", () =>
{
    service.SaveSoldier(new("TEST-01", "수정시험", "일병", "시험소대"), true);
    Assert(service.Data.Soldiers.Last().Rank == "일병");
});

Test("Register stock and reject negative", () =>
{
    service.SaveItem(new("TEST-ITEM", "시험품목", "시험", 5, 2, false), false);
    Reject(() => service.SaveItem(new("BAD", "불량", "시험", -1, 0, false), false));
});

Test("Issue stock and persist atomically", () =>
{
    service.Issue("TEST-01", "TEST-ITEM", 2, "");
    Assert(service.Data.Items.Last().Quantity == 3);
    Assert(new SupplyService(dbPath).Data.Items.Last().Quantity == 3);
});

Test("Insufficient stock leaves data and disk unchanged", () =>
{
    var before = File.ReadAllText(dbPath);
    Reject(() => service.Issue("TEST-01", "TEST-ITEM", 4, ""));
    Assert(File.ReadAllText(dbPath) == before);
    Assert(service.Data.Items.Last().Quantity == 3);
});

Test("Return restores stock and prevents double return", () =>
{
    var issue = service.Data.Issues.First();
    service.Return(issue.Id);
    Assert(service.Data.Items.Last().Quantity == 5);
    Reject(() => service.Return(issue.Id));
    Assert(service.Data.Items.Last().Quantity == 5);
});

Test("Serial uniqueness and one-unit rule", () =>
{
    service.Issue("TEST-01", "TEST-ITEM", 1, "SER-001");
    Reject(() => service.Issue("TEST-01", "TEST-ITEM", 1, "ser-001"));
    Reject(() => service.Issue("TEST-01", "TEST-ITEM", 2, "SER-002"));
});

Test("Supply approval then receipt increases stock once", () =>
{
    service.Request("TEST-ITEM", 10, "테스트 보급");
    var id = service.Data.Requests.First().Id;
    var q = service.Data.Items.Last().Quantity;

    service.Advance(id);   // 승인: 재고 그대로
    Assert(service.Data.Items.Last().Quantity == q);

    service.Advance(id);   // 입고: 재고 증가
    Assert(service.Data.Items.Last().Quantity == q + 10);

    Reject(() => service.Advance(id));
    Assert(service.Data.Items.Last().Quantity == q + 10);
});

// ---------- XLSX·저장 파일 ----------

Test("XLSX roundtrip preserves all records and Korean text", () =>
{
    var p = Path.Combine(folder, "roundtrip.xlsx");
    WorkbookService.Export(p, service.Data);
    var read = WorkbookService.Import(p);
    Assert(JsonSerializer.Serialize(read) == JsonSerializer.Serialize(service.Data));
});

Test("Reject duplicate and orphan workbook records", () =>
{
    var d = SupplyService.Demo();
    d.Soldiers.Add(d.Soldiers[0]);
    Reject(() => SupplyService.Validate(d));

    d = SupplyService.Demo();
    d.Issues.Add(new("BAD", "MISSING", "EQ-001", 1, "", DateTime.Now, null));
    Reject(() => SupplyService.Validate(d));
});

Test("Import replacement produces backup", () =>
{
    var old = File.ReadAllText(dbPath);
    service.Replace(SupplyService.Demo());
    Assert(File.ReadAllText(dbPath + ".bak") == old);
    Assert(service.Data.Soldiers.Count == 6);
});

Test("Invalid XLSX header rejected", () =>
{
    var p = Path.Combine(folder, "bad-header.xlsx");
    WorkbookService.Export(p, service.Data);

    // 첫 시트의 "군번" 열 이름을 일부러 바꾼다
    using (var z = ZipFile.Open(p, ZipArchiveMode.Update))
    {
        var e = z.GetEntry("xl/worksheets/sheet1.xml")!;
        string xml;
        using (var r = new StreamReader(e.Open()))
            xml = r.ReadToEnd();
        e.Delete();
        using var w = new StreamWriter(z.CreateEntry("xl/worksheets/sheet1.xml").Open());
        w.Write(xml.Replace("군번", "잘못된열"));
    }

    Reject(() => WorkbookService.Import(p));
});

Test("Corrupt local JSON is preserved", () =>
{
    var p = Path.Combine(folder, "corrupt.json");
    File.WriteAllText(p, "invalid json");
    Reject(() => new SupplyService(p));
    Assert(File.ReadAllText(p) == "invalid json");
});

Test("No soldiers or required items returns zero rate", () =>
{
    service.Replace(new SupplyData());
    Assert(service.Rate == 0);

    service.Replace(new SupplyData { Soldiers = [new("A", "가", "이병", "소대")] });
    Assert(service.Rate == 0);
});

// ---------- 장비 구성 → 지급 확정 → 지급 완료 (MainViewModel) ----------

service.Replace(SupplyService.Demo());
var dialogs = new FakeDialogs();
var vm = new MainViewModel(service, dialogs);
vm.IssueSoldier = service.Data.Soldiers.Last();

Test("Draft drop is translucent and does not mutate stock", () =>
{
    var item = service.Data.Items[0];
    vm.StageItem(item, "Head");
    Assert(vm.EquipmentSlots[0].HasDraft);
    Assert(vm.EquipmentSlots[0].Opacity == .30);
    Assert(service.Data.Items[0].Quantity == item.Quantity);
});

Test("Wrong body slot is rejected", () =>
{
    var count = dialogs.Errors;
    vm.StageItem(service.Data.Items[1], "Head");
    Assert(dialogs.Errors == count + 1);
    Assert(vm.EquipmentSlots[0].Draft!.Id == "EQ-001");
});

Test("Confirm reserves stock and stays translucent, excluded from rate", () =>
{
    var rate = service.Rate;
    vm.ConfirmLoadoutCommand.Execute(null);
    Assert(vm.EquipmentSlots[0].Active!.AwaitingDelivery);
    Assert(vm.EquipmentSlots[0].Opacity == .48);
    Assert(service.Data.Items[0].Quantity == 133);
    Assert(service.Rate == rate);
    Assert(new SupplyService(dbPath).Data.Issues.First().AwaitingDelivery);
});

Test("Confirmed workflow survives XLSX roundtrip", () =>
{
    var path = Path.Combine(folder, "confirmed.xlsx");
    WorkbookService.Export(path, service.Data);
    Assert(WorkbookService.Import(path).Issues.First().AwaitingDelivery);
});

Test("Completion equips opaque without second stock deduction", () =>
{
    vm.CompleteDeliveryCommand.Execute(null);
    Assert(vm.EquipmentSlots[0].Opacity == 1);
    Assert(!vm.EquipmentSlots[0].Active!.AwaitingDelivery);
    Assert(service.Data.Items[0].Quantity == 133);
});

Test("Existing equipped slot rejects duplicate drag", () =>
    Assert(!vm.CanStageItem(service.Data.Items[0], "Head")));

Test("Changing soldier clears unconfirmed drafts", () =>
{
    vm.StageItem(service.Data.Items[1], "Armor");
    Assert(vm.EquipmentSlots[1].HasDraft);

    vm.IssueSoldier = service.Data.Soldiers.First();
    Assert(!vm.EquipmentSlots[1].HasDraft);
    Assert(vm.EquipmentSlots[1].Opacity == 1);
});

Test("Batch reservation is atomic if later item is unavailable", () =>
{
    service.Replace(SupplyService.Demo());
    service.SaveItem(service.Data.Items[1] with { Quantity = 0 }, true);
    var before = File.ReadAllText(dbPath);

    Reject(() => service.ConfirmLoadout("26-10006", [new("EQ-001", ""), new("EQ-002", "")]));
    Assert(service.Data.Items[0].Quantity == 134);
    Assert(File.ReadAllText(dbPath) == before);
});

Test("Duplicate serial across batch rolls back all reservations", () =>
{
    service.Replace(SupplyService.Demo());
    Reject(() => service.ConfirmLoadout("26-10006", [new("EQ-001", "TESTSER"), new("EQ-002", "testser")]));
    Assert(service.Data.Items[0].Quantity == 134);
    Assert(!service.Data.Issues.Any(x => x.AwaitingDelivery));
});

Test("Cancelling confirmed delivery returns reserved inventory", () =>
{
    service.ConfirmLoadout("26-10006", [new("EQ-001", "")]);
    var id = service.Data.Issues.First().Id;
    service.Return(id);
    Assert(service.Data.Items[0].Quantity == 134);
    Reject(() => service.CompleteDelivery("26-10006"));
});

Test("Rate changes only after delivery completion", () =>
{
    service.Replace(SupplyService.Demo());
    var required = service.Data.Items.Where(x => x.Required).Select(x => new IssueDraft(x.Id, "")).ToList();

    service.ConfirmLoadout("26-10006", required);
    Assert(service.Rate < 84);

    service.CompleteDelivery("26-10006");
    Assert(service.Rate == 100);
    Reject(() => service.CompleteDelivery("26-10006"));
});

// ---------- 보급 요청 화면 (MainViewModel) ----------

service.Replace(SupplyService.Demo());
var requestVm = new MainViewModel(service, dialogs);

Test("Request filter shows only shortages in shortage order", () =>
{
    Assert(requestVm.ShowShortageOnly);
    Assert(requestVm.RequestItems.Count() == 4);
    Assert(requestVm.RequestItems.First().Shortage == 6);
    Assert(requestVm.RequestItems.Sum(x => x.Shortage) == 12);
    Assert(!(new StockItem("E", "동일", "시험", 10, 10, false)).IsShortage);
});

Test("Selecting shortage prefills amount and filter clears hidden selection", () =>
{
    requestVm.RequestItem = requestVm.RequestItems.First();
    Assert(requestVm.RequestQuantity == "6");

    requestVm.ShowShortageOnly = false;
    Assert(requestVm.RequestItems.Count() == 8);

    requestVm.RequestItem = service.Data.Items.First();
    Assert(requestVm.RequestQuantity == "1");

    requestVm.ShowShortageOnly = true;
    Assert(requestVm.RequestItem == null);
});

Test("Manual request amount is retained and replenished item leaves shortage filter", () =>
{
    requestVm.RequestItem = requestVm.RequestItems.First();
    var id = requestVm.RequestItem.Id;
    requestVm.RequestQuantity = "20";
    requestVm.Reason = "부족 보충 및 예비";
    requestVm.RequestCommand.Execute(null);
    Assert(service.Data.Requests.First().Quantity == 20);
    Assert(requestVm.RequestQuantity == "20");

    // 재고를 최소 수량까지 채우면 부족 목록에서 빠진다
    var item = service.Data.Items.First(x => x.Id == id);
    service.SaveItem(item with { Quantity = item.Minimum }, true);
    requestVm.Refresh();
    Assert(!requestVm.RequestItems.Any(x => x.Id == id));
    Assert(requestVm.RequestItem == null);
});

// ---------- 끝: XLSX 빈 양식과 데모 양식을 만들어 둔다 ----------

var templates = Path.Combine(folder, "Templates");
Directory.CreateDirectory(templates);
WorkbookService.Export(Path.Combine(templates, "기본양식_빈파일.xlsx"), new());
WorkbookService.Export(Path.Combine(templates, "기본양식_데모.xlsx"), SupplyService.Demo());
Console.WriteLine($"{passed} tests passed. Templates: {templates}");

// 창을 띄우지 않고 ViewModel을 시험하기 위한 가짜 대화상자
sealed class FakeDialogs : IUserDialogs
{
    public int Errors { get; private set; }
    public string? OpenWorkbook() => null;
    public string? SaveWorkbook() => null;
    public bool ConfirmImport(string path) => false;
    public void ShowError(string message) => Errors++;
}
