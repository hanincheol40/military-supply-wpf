using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using MilitarySupply.Models;

namespace MilitarySupply.Services
{
    // Office 설치나 외부 라이브러리 없이 XLSX(OOXML) 파일을 직접 읽고 쓴다
    public static class WorkbookService
    {
        static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";

        // 시트별 열 제목 (Names와 같은 순서)
        static readonly string[][] Headers =
        [
            ["군번", "이름", "계급", "소속"],
            ["품목코드", "품목명", "구분", "가용재고", "최소재고", "필수장비"],
            ["지급ID", "군번", "품목코드", "수량", "일련번호", "지급일시", "반납일시", "지급상태"],
            ["요청ID", "품목코드", "수량", "사유", "상태", "요청일시"],
            ["일시", "작업", "상세"]
        ];

        static readonly string[] Names = ["병사", "재고", "지급이력", "보급요청", "변경이력"];

        public static void Export(string path, SupplyData d)
        {
            SupplyService.Validate(d);

            var rows = new List<IEnumerable<string[]>>
            {
                d.Soldiers.Select(x => new[] { x.Id, x.Name, x.Rank, x.Unit }),
                d.Items.Select(x => new[] { x.Id, x.Name, x.Category, N(x.Quantity), N(x.Minimum), x.Required ? "예" : "아니오" }),
                d.Issues.Select(x => new[]
                {
                    x.Id, x.SoldierId, x.ItemId, N(x.Quantity), x.Serial, D(x.Date),
                    x.ReturnedAt is { } t ? D(t) : "",
                    x.AwaitingDelivery ? "지급 확정" : "지급 완료"
                }),
                d.Requests.Select(x => new[] { x.Id, x.ItemId, N(x.Quantity), x.Reason, x.Status, D(x.Date) }),
                d.History.Select(x => new[] { D(x.Date), x.Action, x.Detail })
            };

            var temporary = path + ".tmp";
            try
            {
                using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                {
                    XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
                    Write(zip, "[Content_Types].xml", new XElement(ct + "Types",
                        new XElement(ct + "Default",
                            new XAttribute("Extension", "rels"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                        new XElement(ct + "Default",
                            new XAttribute("Extension", "xml"),
                            new XAttribute("ContentType", "application/xml")),
                        new XElement(ct + "Override",
                            new XAttribute("PartName", "/xl/workbook.xml"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                        Names.Select((_, i) => new XElement(ct + "Override",
                            new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));

                    Write(zip, "_rels/.rels",
                        new XElement(P + "Relationships", Rel("rId1", "officeDocument", "xl/workbook.xml")));

                    Write(zip, "xl/workbook.xml", new XElement(S + "workbook",
                        new XAttribute(XNamespace.Xmlns + "r", R),
                        new XElement(S + "sheets", Names.Select((n, i) => new XElement(S + "sheet",
                            new XAttribute("name", n),
                            new XAttribute("sheetId", i + 1),
                            new XAttribute(R + "id", $"rId{i + 1}"))))));

                    Write(zip, "xl/_rels/workbook.xml.rels", new XElement(P + "Relationships",
                        Names.Select((_, i) => Rel($"rId{i + 1}", "worksheet", $"worksheets/sheet{i + 1}.xml"))));

                    for (int i = 0; i < Names.Length; i++)
                    {
                        var all = new[] { Headers[i] }.Concat(rows[i]).ToList();
                        Write(zip, $"xl/worksheets/sheet{i + 1}.xml", new XElement(S + "worksheet",
                            // 첫 줄(열 제목) 고정
                            new XElement(S + "sheetViews", new XElement(S + "sheetView",
                                new XAttribute("workbookViewId", 0),
                                new XElement(S + "pane",
                                    new XAttribute("ySplit", 1),
                                    new XAttribute("topLeftCell", "A2"),
                                    new XAttribute("state", "frozen")))),
                            new XElement(S + "cols", new XElement(S + "col",
                                new XAttribute("min", 1),
                                new XAttribute("max", Headers[i].Length),
                                new XAttribute("width", 24),
                                new XAttribute("customWidth", 1))),
                            new XElement(S + "sheetData", all.Select((row, ri) => new XElement(S + "row",
                                new XAttribute("r", ri + 1),
                                row.Select((value, ci) => new XElement(S + "c",
                                    new XAttribute("r", $"{(char)('A' + ci)}{ri + 1}"),
                                    new XAttribute("t", "inlineStr"),
                                    new XElement(S + "is", new XElement(S + "t",
                                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                                        value))))))),
                            new XElement(S + "autoFilter",
                                new XAttribute("ref", $"A1:{(char)('A' + Headers[i].Length - 1)}{all.Count}"))));
                    }
                }

                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        static XElement Rel(string id, string type, string target) =>
            new(P + "Relationship",
                new XAttribute("Id", id),
                new XAttribute("Type", R.NamespaceName + "/" + type),
                new XAttribute("Target", target));

        static void Write(ZipArchive zip, string path, XElement root)
        {
            using var stream = zip.CreateEntry(path).Open();
            new XDocument(root).Save(stream);
        }

        static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        static string D(DateTime d) => d.ToString("O", CultureInfo.InvariantCulture);

        public static SupplyData Import(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Sum(x => x.Length) > 40_000_000)
                throw new InvalidDataException("양식 크기가 40MB를 초과합니다.");

            XDocument Read(string name)
            {
                using var st = (zip.GetEntry(name) ?? throw new InvalidDataException($"XLSX 내부 파일 누락: {name}")).Open();
                return XDocument.Load(st);
            }

            var book = Read("xl/workbook.xml");
            var rels = Read("xl/_rels/workbook.xml.rels");
            var shared = zip.GetEntry("xl/sharedStrings.xml") == null
                ? []
                : Read("xl/sharedStrings.xml").Descendants(S + "si")
                    .Select(x => string.Concat(x.Descendants(S + "t").Select(t => t.Value)))
                    .ToArray();

            List<string[]> Sheet(int index, bool optional = false)
            {
                var sheet = book.Descendants(S + "sheet")
                    .FirstOrDefault(x => (string?)x.Attribute("name") == Names[index]);
                if (sheet == null && optional)
                    return [];
                if (sheet == null)
                    throw new InvalidDataException($"'{Names[index]}' 시트가 없습니다.");

                var target = (string?)rels.Descendants(P + "Relationship")
                    .FirstOrDefault(x => (string?)x.Attribute("Id") == (string?)sheet.Attribute(R + "id"))
                    ?.Attribute("Target")
                    ?? throw new InvalidDataException("시트 연결 정보가 없습니다.");
                var resolved = new Uri(new Uri("http://local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');

                var result = new List<string[]>();
                foreach (var row in Read(resolved).Descendants(S + "sheetData").Elements(S + "row"))
                {
                    var values = Enumerable.Repeat("", Headers[index].Length).ToArray();
                    foreach (var c in row.Elements(S + "c"))
                    {
                        var reference = (string?)c.Attribute("r") ?? throw new InvalidDataException("셀 주소가 없습니다.");

                        // "B3" 같은 셀 주소에서 열 번호 계산 (A=1, B=2, ...)
                        int col = 0;
                        foreach (char ch in reference.TakeWhile(char.IsLetter))
                            col = col * 26 + char.ToUpperInvariant(ch) - 'A' + 1;

                        if (col < 1 || col > values.Length)
                            continue;
                        if (c.Element(S + "f") != null)
                            throw new InvalidDataException($"{Names[index]}!{reference}: 수식 대신 값으로 붙여넣으세요.");

                        var v = c.Element(S + "v")?.Value ?? "";
                        values[col - 1] = (string?)c.Attribute("t") switch
                        {
                            "inlineStr" => string.Concat(c.Descendants(S + "t").Select(t => t.Value)),
                            "s" => shared[int.Parse(v, CultureInfo.InvariantCulture)],
                            _ => v
                        };
                    }

                    if (values.Any(x => !string.IsNullOrWhiteSpace(x)))
                        result.Add(values);
                }

                // 지급상태 열이 없는 예전 양식도 읽을 수 있게 채워 준다
                if (index == 2 && result.Count > 0 && result[0].Take(7).SequenceEqual(Headers[2].Take(7)) && result[0][7] == "")
                {
                    result[0][7] = "지급상태";
                    foreach (var row in result.Skip(1))
                        row[7] = "지급 완료";
                }

                if (result.Count == 0 || !result[0].SequenceEqual(Headers[index]))
                    throw new InvalidDataException($"'{Names[index]}'의 열 제목 또는 순서가 기본 양식과 다릅니다.");

                return result.Skip(1).ToList();
            }

            static int Number(string s) =>
                int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                    ? n
                    : throw new InvalidDataException($"정수가 아닙니다: {s}");

            static DateTime Date(string s)
            {
                if (DateTime.TryParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d))
                    return d;
                if (DateTime.TryParseExact(s, new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    return d;

                // 엑셀 기본(1900년 기준) 날짜 숫자. 1904 기준 파일은 아래에서 막는다
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    return DateTime.FromOADate(n);

                throw new InvalidDataException($"날짜 형식 오류: {s}");
            }

            if (new[] { "1", "true" }.Contains((string?)book.Root?.Element(S + "workbookPr")?.Attribute("date1904")))
                throw new InvalidDataException("1904 날짜 체계는 지원하지 않습니다. 기본 양식을 사용하세요.");

            var data = new SupplyData
            {
                Soldiers = Sheet(0).Select(x => new Soldier(x[0], x[1], x[2], x[3])).ToList(),
                Items = Sheet(1).Select(x => new StockItem(x[0], x[1], x[2], Number(x[3]), Number(x[4]), x[5] switch
                {
                    "예" => true,
                    "아니오" => false,
                    _ => throw new InvalidDataException("필수장비는 예/아니오로 입력하세요.")
                })).ToList(),
                Issues = Sheet(2).Select(x => new Issue(x[0], x[1], x[2], Number(x[3]), x[4], Date(x[5]),
                    string.IsNullOrWhiteSpace(x[6]) ? null : Date(x[6]),
                    x[7] switch
                    {
                        "지급 확정" => true,
                        "지급 완료" => false,
                        _ => throw new InvalidDataException("지급상태는 지급 확정/지급 완료로 입력하세요.")
                    })).ToList(),
                Requests = Sheet(3).Select(x => new SupplyRequest(x[0], x[1], Number(x[2]), x[3], x[4], Date(x[5]))).ToList(),
                History = Sheet(4, true).Select(x => new AuditEntry(Date(x[0]), x[1], x[2])).ToList()
            };

            SupplyService.Validate(data);
            return data;
        }
    }
}
