using MilitarySupply.Models;
using MilitarySupply.Services;
using System.IO;

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
Test("Corrupt local JSON is preserved", () => { var p = Path.Combine(folder, "corrupt.json"); File.WriteAllText(p, "invalid json"); Reject(() => new SupplyService(p)); Assert(File.ReadAllText(p) == "invalid json"); });
Test("No soldiers or required items returns zero rate", () => { service.Replace(new SupplyData()); Assert(service.Rate == 0); service.Replace(new SupplyData { Soldiers = [new("A", "가", "이병", "소대")] }); Assert(service.Rate == 0); });

Console.WriteLine($"{passed} tests passed.");
