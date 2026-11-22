using System.IO;
using System.Text.Json;
using MilitarySupply.Models;

namespace MilitarySupply.Services
{
    public sealed class SupplyService
    {
        public SupplyData Data { get; private set; }
        public string FilePath { get; }

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public SupplyService(string path)
        {
            FilePath = path;

            // 저장 파일이 있으면 불러오고, 없으면 데모 데이터로 시작
            Data = File.Exists(path)
                ? JsonSerializer.Deserialize<SupplyData>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("저장 파일이 비어 있습니다.")
                : Demo();
            Validate(Data);
        }

        public static void Validate(SupplyData d)
        {
            if (d.Version != 1)
                throw new InvalidDataException("지원하지 않는 데이터 버전입니다.");

            if (d.Soldiers == null || d.Items == null || d.Issues == null || d.Requests == null || d.History == null)
                throw new InvalidDataException("필수 데이터 목록이 누락되었습니다.");

            static void Unique(IEnumerable<string> ids, string label)
            {
                var list = ids.ToList();
                if (list.Any(string.IsNullOrWhiteSpace) || list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count)
                    throw new InvalidDataException($"{label}: 빈 ID 또는 중복 ID가 있습니다.");
            }

            Unique(d.Soldiers.Select(x => x.Id), "병사");
            Unique(d.Items.Select(x => x.Id), "재고");
            Unique(d.Issues.Select(x => x.Id), "지급");
            Unique(d.Requests.Select(x => x.Id), "요청");

            if (d.Soldiers.Any(x => string.IsNullOrWhiteSpace(x.Name)
                                 || string.IsNullOrWhiteSpace(x.Rank)
                                 || string.IsNullOrWhiteSpace(x.Unit)))
                throw new InvalidDataException("병사 이름, 계급, 소속은 필수입니다.");

            if (d.Items.Any(x => string.IsNullOrWhiteSpace(x.Name)
                              || string.IsNullOrWhiteSpace(x.Category)
                              || x.Quantity < 0
                              || x.Minimum < 0))
                throw new InvalidDataException("품목명·구분은 필수이며 재고와 기준은 0 이상이어야 합니다.");

            if (d.Issues.Any(x => !d.Soldiers.Any(s => s.Id == x.SoldierId)
                               || !d.Items.Any(i => i.Id == x.ItemId)
                               || x.Quantity <= 0
                               || x.Date == default
                               || x.ReturnedAt < x.Date
                               || (!string.IsNullOrEmpty(x.Serial) && x.Quantity != 1)))
                throw new InvalidDataException("지급 이력의 병사·품목·수량·날짜·일련번호를 확인하세요.");

            var serials = d.Issues
                .Where(x => x.ReturnedAt == null && !string.IsNullOrEmpty(x.Serial))
                .Select(x => x.Serial);
            if (serials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serials.Count())
                throw new InvalidDataException("지급 중인 일련번호가 중복됩니다.");

            if (d.Requests.Any(x => !d.Items.Any(i => i.Id == x.ItemId)
                                 || x.Quantity <= 0
                                 || string.IsNullOrWhiteSpace(x.Reason)
                                 || x.Date == default
                                 || !new[] { "승인 대기", "승인 완료", "입고 완료" }.Contains(x.Status)))
                throw new InvalidDataException("보급 요청의 품목·수량·사유·상태·날짜를 확인하세요.");
        }

        // 복사본을 고치고, 검사를 통과하면 저장한 뒤 바꿔 끼운다
        private void Commit(Action<SupplyData> change, string action, string detail)
        {
            var next = JsonSerializer.Deserialize<SupplyData>(JsonSerializer.Serialize(Data))!;
            change(next);
            next.History.Insert(0, new(DateTime.Now, action, detail));
            Validate(next);
            Persist(next);
            Data = next;
        }

        private void Persist(SupplyData next)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);

            // 임시 파일에 먼저 쓰고 바꿔치기 (기존 파일은 .bak으로 남김)
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next, JsonOptions));

            if (File.Exists(FilePath))
                File.Replace(temp, FilePath, FilePath + ".bak");
            else
                File.Move(temp, FilePath);
        }

        public void Save() => Persist(Data);

        public static SupplyData Demo()
        {
            var d = new SupplyData
            {
                Soldiers =
                [
                    new("26-10001", "김민준", "일병", "1소대"),
                    new("26-10002", "박지훈", "상병", "1소대"),
                    new("26-10003", "이서준", "상병", "2소대"),
                    new("26-10004", "최도윤", "일병", "2소대"),
                    new("26-10005", "정시우", "이병", "3소대"),
                    new("26-10006", "강하준", "병장", "3소대")
                ],
                Items =
                [
                    new("EQ-001", "방탄헬멧", "개인장구", 134, 30, true),
                    new("EQ-002", "방탄복", "개인장구", 48, 30, true),
                    new("EQ-003", "전투조끼", "개인장구", 57, 30, true),
                    new("EQ-004", "탄알집", "탄약/부속", 24, 30, false),
                    new("EQ-005", "전투화", "피복류", 265, 50, true),
                    new("EQ-006", "군장", "개인장구", 28, 30, true),
                    new("EQ-007", "K2", "무기", 27, 30, false),
                    new("EQ-008", "K2C1", "무기", 19, 20, false)
                ]
            };

            // 앞의 5명은 필수 장비를 받은 상태로 시작
            foreach (var s in d.Soldiers.Take(5))
            {
                foreach (var i in d.Items.Where(x => x.Required))
                    d.Issues.Add(new(Guid.NewGuid().ToString("N"), s.Id, i.Id, 1, "", DateTime.Today.AddDays(-2), null));
            }

            d.Requests =
            [
                new("REQ-DEMO-01", "EQ-004", 30, "훈련 대비 예비 수량 확보", "승인 대기", DateTime.Today),
                new("REQ-DEMO-02", "EQ-006", 10, "신규 전입 인원 지급", "승인 완료", DateTime.Today.AddDays(-1))
            ];
            d.History.Add(new(DateTime.Now, "데모 데이터", "가상 병사 6명 · 실제 군 정보 아님"));
            return d;
        }
    }
}
