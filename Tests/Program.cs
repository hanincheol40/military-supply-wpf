using MilitarySupply.Models;
using MilitarySupply.Services;
using System.IO.Compression;
using System.Text.Json;
using System.IO;
using MilitarySupply.ViewModels;

var folder = Path.Combine(args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "MilitarySupplyTests"), Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
int passed = 0;
void Test(string name, Action action) { action(); passed++; Console.WriteLine($"PASS {name}"); }
void Assert(bool test) { if (!test) throw new Exception("Assertion failed"); }
void Reject(Action action) { bool failed = false; try { action(); } catch { failed = true; } Assert(failed); }
var dbPath = Path.Combine(folder, "data.json");
var service = new SupplyService(dbPath);
Test("Demo data and exact dashboard rate", () => { Assert(service.Data.Soldiers.Count == 6); Assert(Math.Abs(service.Rate - 100.0 * 5 / 6) < .0001); Assert(service.Data.Items.Count(x => x.Status == "부족") == 4); });
Test("Register soldier and reject duplicate", () => { service.SaveSoldier(new("TEST-01", "테스트", "이병", "시험소대"), false); Reject(() => service.SaveSoldier(new("TEST-01", "테스트", "이병", "시험소대"), false)); });
Test("Update soldier", () => { service.SaveSoldier(new("TEST-01", "수정시험", "일병", "시험소대"), true); Assert(service.Data.Soldiers.Last().Rank == "일병"); });
Test("Register stock and reject negative", () => { service.SaveItem(new("TEST-ITEM", "시험품목", "시험", 5, 2, false), false); Reject(() => service.SaveItem(new("BAD", "불량", "시험", -1, 0, false), false)); });
Test("Issue stock and persist atomically", () => { service.Issue("TEST-01", "TEST-ITEM", 2, ""); Assert(service.Data.Items.Last().Quantity == 3); Assert(new SupplyService(dbPath).Data.Items.Last().Quantity == 3); });
Test("Insufficient stock leaves data and disk unchanged", () => { var before = File.ReadAllText(dbPath); Reject(() => service.Issue("TEST-01", "TEST-ITEM", 4, "")); Assert(File.ReadAllText(dbPath) == before); Assert(service.Data.Items.Last().Quantity == 3); });
Test("Return restores stock and prevents double return", () => { var issue = service.Data.Issues.First(); service.Return(issue.Id); Assert(service.Data.Items.Last().Quantity == 5); Reject(() => service.Return(issue.Id)); Assert(service.Data.Items.Last().Quantity == 5); });
Test("Serial uniqueness and one-unit rule", () => { service.Issue("TEST-01", "TEST-ITEM", 1, "SER-001"); Reject(() => service.Issue("TEST-01", "TEST-ITEM", 1, "ser-001")); Reject(() => service.Issue("TEST-01", "TEST-ITEM", 2, "SER-002")); });
Test("Supply approval then receipt increases stock once", () => { service.Request("TEST-ITEM", 10, "테스트 보급"); var id = service.Data.Requests.First().Id; var q = service.Data.Items.Last().Quantity; service.Advance(id); Assert(service.Data.Items.Last().Quantity == q); service.Advance(id); Assert(service.Data.Items.Last().Quantity == q + 10); Reject(() => service.Advance(id)); Assert(service.Data.Items.Last().Quantity == q + 10); });
Test("XLSX roundtrip preserves all records and Korean text", () => { var p = Path.Combine(folder, "roundtrip.xlsx"); WorkbookService.Export(p, service.Data); var read = WorkbookService.Import(p); Assert(JsonSerializer.Serialize(read) == JsonSerializer.Serialize(service.Data)); });
Test("Reject duplicate and orphan workbook records", () => { var d = SupplyService.Demo(); d.Soldiers.Add(d.Soldiers[0]); Reject(() => SupplyService.Validate(d)); d = SupplyService.Demo(); d.Issues.Add(new("BAD", "MISSING", "EQ-001", 1, "", DateTime.Now, null)); Reject(() => SupplyService.Validate(d)); });
Test("Import replacement produces backup", () => { var old = File.ReadAllText(dbPath); service.Replace(SupplyService.Demo()); Assert(File.ReadAllText(dbPath + ".bak") == old); Assert(service.Data.Soldiers.Count == 6); });
Test("Invalid XLSX header rejected", () => { var p = Path.Combine(folder, "bad-header.xlsx"); WorkbookService.Export(p, service.Data); using (var z = ZipFile.Open(p, ZipArchiveMode.Update)) { var e = z.GetEntry("xl/worksheets/sheet1.xml")!; string xml; using (var r = new StreamReader(e.Open())) xml = r.ReadToEnd(); e.Delete(); using var w = new StreamWriter(z.CreateEntry("xl/worksheets/sheet1.xml").Open()); w.Write(xml.Replace("군번", "잘못된열")); } Reject(() => WorkbookService.Import(p)); });
Test("Corrupt local JSON is preserved", () => { var p = Path.Combine(folder, "corrupt.json"); File.WriteAllText(p, "invalid json"); Reject(() => new SupplyService(p)); Assert(File.ReadAllText(p) == "invalid json"); });
Test("No soldiers or required items returns zero rate", () => { service.Replace(new SupplyData()); Assert(service.Rate == 0); service.Replace(new SupplyData { Soldiers = [new("A", "가", "이병", "소대")] }); Assert(service.Rate == 0); });
service.Replace(SupplyService.Demo());
var dialogs = new FakeDialogs();
var vm = new MainViewModel(service, dialogs);
vm.IssueSoldier = service.Data.Soldiers.Last();
Test("Draft drop is translucent and does not mutate stock", () => { var item = service.Data.Items[0]; vm.StageItem(item, "Head"); Assert(vm.EquipmentSlots[0].HasDraft); Assert(vm.EquipmentSlots[0].Opacity == .30); Assert(service.Data.Items[0].Quantity == item.Quantity); });
Test("Wrong body slot is rejected", () => { var count = dialogs.Errors; vm.StageItem(service.Data.Items[1], "Head"); Assert(dialogs.Errors == count + 1); Assert(vm.EquipmentSlots[0].Draft!.Id == "EQ-001"); });
Test("Confirm reserves stock and stays translucent, excluded from rate", () => { var rate = service.Rate; vm.ConfirmLoadoutCommand.Execute(null); Assert(vm.EquipmentSlots[0].Active!.AwaitingDelivery); Assert(vm.EquipmentSlots[0].Opacity == .48); Assert(service.Data.Items[0].Quantity == 133); Assert(service.Rate == rate); Assert(new SupplyService(dbPath).Data.Issues.First().AwaitingDelivery); });
Test("Confirmed workflow survives XLSX roundtrip", () => { var path = Path.Combine(folder, "confirmed.xlsx"); WorkbookService.Export(path, service.Data); Assert(WorkbookService.Import(path).Issues.First().AwaitingDelivery); });
Test("Completion equips opaque without second stock deduction", () => { vm.CompleteDeliveryCommand.Execute(null); Assert(vm.EquipmentSlots[0].Opacity == 1); Assert(!vm.EquipmentSlots[0].Active!.AwaitingDelivery); Assert(service.Data.Items[0].Quantity == 133); });
Test("Existing equipped slot rejects duplicate drag", () => Assert(!vm.CanStageItem(service.Data.Items[0], "Head")));
Test("Changing soldier clears unconfirmed drafts", () => { vm.StageItem(service.Data.Items[1], "Armor"); Assert(vm.EquipmentSlots[1].HasDraft); vm.IssueSoldier = service.Data.Soldiers.First(); Assert(!vm.EquipmentSlots[1].HasDraft); Assert(vm.EquipmentSlots[1].Opacity == 1); });
Test("Batch reservation is atomic if later item is unavailable", () => { service.Replace(SupplyService.Demo()); service.SaveItem(service.Data.Items[1] with { Quantity = 0 }, true); var before = File.ReadAllText(dbPath); Reject(() => service.ConfirmLoadout("26-10006", [new("EQ-001", ""), new("EQ-002", "")])); Assert(service.Data.Items[0].Quantity == 134); Assert(File.ReadAllText(dbPath) == before); });
Test("Duplicate serial across batch rolls back all reservations", () => { service.Replace(SupplyService.Demo()); Reject(() => service.ConfirmLoadout("26-10006", [new("EQ-001", "TESTSER"), new("EQ-002", "testser")])); Assert(service.Data.Items[0].Quantity == 134); Assert(!service.Data.Issues.Any(x => x.AwaitingDelivery)); });
Test("Cancelling confirmed delivery returns reserved inventory", () => { service.ConfirmLoadout("26-10006", [new("EQ-001", "")]); var id = service.Data.Issues.First().Id; service.Return(id); Assert(service.Data.Items[0].Quantity == 134); Reject(() => service.CompleteDelivery("26-10006")); });
Test("Rate changes only after delivery completion", () => { service.Replace(SupplyService.Demo()); service.ConfirmLoadout("26-10006", service.Data.Items.Where(x => x.Required).Select(x => new IssueDraft(x.Id, "")).ToList()); Assert(service.Rate < 84); service.CompleteDelivery("26-10006"); Assert(service.Rate == 100); Reject(() => service.CompleteDelivery("26-10006")); });
var templates = Path.Combine(folder, "Templates"); Directory.CreateDirectory(templates); WorkbookService.Export(Path.Combine(templates, "기본양식_빈파일.xlsx"), new()); WorkbookService.Export(Path.Combine(templates, "기본양식_데모.xlsx"), SupplyService.Demo()); Console.WriteLine($"{passed} tests passed. Templates: {templates}");  sealed class FakeDialogs : IUserDialogs {     public int Errors { get; private set; }     public string? OpenWorkbook() => null;     public string? SaveWorkbook() => null;     public bool ConfirmImport(string path) => false;     public void ShowError(string message) => Errors++; }
