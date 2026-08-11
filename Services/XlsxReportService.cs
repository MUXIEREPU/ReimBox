using System.Globalization;
using System.IO.Compression;
using System.Security;
using ReimbursementAssistant.Models;

namespace ReimbursementAssistant.Services;

public sealed class XlsxReportService
{
    public void Write(string filePath, IReadOnlyList<InvoiceRecord> records)
    {
        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        AddText(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>
""");
        AddText(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
""");
        AddText(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets><sheet name="报销清单" sheetId="1" r:id="rId1"/></sheets>
</workbook>
""");
        AddText(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
""");
        AddText(archive, "xl/styles.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="2"><font><sz val="11"/><name val="Microsoft YaHei UI"/></font><font><b/><sz val="11"/><name val="Microsoft YaHei UI"/></font></fonts>
  <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0"/></cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""");
        AddText(archive, "xl/worksheets/sheet1.xml", BuildSheet(records));
    }

    private static string BuildSheet(IReadOnlyList<InvoiceRecord> records)
    {
        var headers = new[]
        {
            "序号", "分类", "商品/说明", "销售方", "发票号码", "开票日期", "金额", "材料状态", "缺失材料", "附件数", "原文件名"
        };

        var rows = new List<string> { Row(1, headers.Select((x, i) => TextCell(Column(i + 1), 1, x, bold: true))) };
        var rowIndex = 2;
        foreach (var record in records.OrderBy(x => x.DisplayIndex))
        {
            var missing = string.Join("、", record.MissingTypes().Select(InvoiceRecord.DisplayAttachment));
            var values = new string[]
            {
                record.DisplayIndex.ToString(CultureInfo.InvariantCulture),
                record.CategoryDisplay,
                record.ItemDescription ?? "",
                record.SellerName ?? "",
                record.InvoiceNumber ?? "",
                record.InvoiceDate?.ToString("yyyy-MM-dd") ?? "",
                (record.TotalAmount ?? 0).ToString("0.00", CultureInfo.InvariantCulture),
                record.MaterialStatusLabel,
                missing,
                record.AttachedDocuments.Count(x => x.AttachmentType != AttachmentType.Invoice).ToString(CultureInfo.InvariantCulture),
                record.OriginalFileName
            };

            rows.Add(Row(rowIndex, values.Select((x, i) => i == 6 ? NumberCell(Column(i + 1), rowIndex, x) : TextCell(Column(i + 1), rowIndex, x))));
            rowIndex++;
        }

        rows.Add(Row(rowIndex + 1, [TextCell("A", rowIndex + 1, "总计", bold: true), NumberCell("G", rowIndex + 1, records.Sum(x => x.TotalAmount ?? 0).ToString("0.00", CultureInfo.InvariantCulture))]));

        return $$"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <cols>
    <col min="1" max="1" width="8" customWidth="1"/>
    <col min="2" max="2" width="14" customWidth="1"/>
    <col min="3" max="3" width="32" customWidth="1"/>
    <col min="4" max="4" width="24" customWidth="1"/>
    <col min="5" max="5" width="24" customWidth="1"/>
    <col min="6" max="6" width="14" customWidth="1"/>
    <col min="7" max="7" width="12" customWidth="1"/>
    <col min="8" max="8" width="16" customWidth="1"/>
    <col min="9" max="9" width="24" customWidth="1"/>
    <col min="10" max="10" width="10" customWidth="1"/>
    <col min="11" max="11" width="28" customWidth="1"/>
  </cols>
  <sheetData>
{{string.Join(Environment.NewLine, rows)}}
  </sheetData>
</worksheet>
""";
    }

    private static string Row(int index, IEnumerable<string> cells) => $"    <row r=\"{index}\">{string.Join("", cells)}</row>";

    private static string TextCell(string column, int row, string text, bool bold = false)
    {
        var style = bold ? " s=\"1\"" : "";
        return $"<c r=\"{column}{row}\" t=\"inlineStr\"{style}><is><t>{Escape(text)}</t></is></c>";
    }

    private static string NumberCell(string column, int row, string number) => $"<c r=\"{column}{row}\"><v>{number}</v></c>";

    private static string Column(int index)
    {
        var dividend = index;
        var column = "";
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            column = Convert.ToChar('A' + modulo) + column;
            dividend = (dividend - modulo) / 26;
        }
        return column;
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? "";

    private static void AddText(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
        writer.Write(content);
    }
}
