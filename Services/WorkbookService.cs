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
    }
}
