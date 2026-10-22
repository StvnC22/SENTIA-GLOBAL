using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace AnalisisSentimiento.Web.Reports;

public sealed record XlsxSheet(
    string Name,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyList<double>? ColumnWidths = null
);

public sealed record XlsxLink(string Url, string Text);

public static class XlsxWorkbookWriter
{
    public static byte[] Build(IReadOnlyList<XlsxSheet> sheets)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
            Add(
                zip,
                "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>"""
            );
            Add(zip, "xl/workbook.xml", Workbook(sheets));
            Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Count));
            Add(zip, "xl/styles.xml", Styles);
            for (var i = 0; i < sheets.Count; i++)
            {
                Add(zip, $"xl/worksheets/sheet{i + 1}.xml", Sheet(sheets[i]));
            }
        }

        return buffer.ToArray();
    }

    private const string Styles =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font><font><u/><sz val="11"/><color rgb="FF0563C1"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF0F4C81"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment wrapText="1" vertical="top"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";

    private static string ContentTypes(int sheetCount)
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>"""
        );
        for (var i = 1; i <= sheetCount; i++)
        {
            sb.Append(
                $"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>"""
            );
        }

        return sb.Append("</Types>").ToString();
    }

    private static string Workbook(IReadOnlyList<XlsxSheet> sheets)
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>"""
        );
        for (var i = 0; i < sheets.Count; i++)
        {
            var name = Escape(SafeSheetName(sheets[i].Name, i));
            sb.Append($"""<sheet name="{name}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        }

        return sb.Append("</sheets></workbook>").ToString();
    }

    private static string WorkbookRels(int sheetCount)
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">"""
        );
        for (var i = 1; i <= sheetCount; i++)
        {
            sb.Append(
                $"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>"""
            );
        }

        sb.Append(
            $"""<Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>"""
        );
        return sb.Append("</Relationships>").ToString();
    }

    private static string Sheet(XlsxSheet sheet)
    {
        var sb = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>"""
        );

        if (sheet.ColumnWidths is { Count: > 0 })
        {
            sb.Append("<cols>");
            for (var i = 0; i < sheet.ColumnWidths.Count; i++)
            {
                sb.Append(
                    CultureInfo.InvariantCulture,
                    $"""<col min="{i + 1}" max="{i + 1}" width="{sheet.ColumnWidths[i]}" customWidth="1"/>"""
                );
            }

            sb.Append("</cols>");
        }

        sb.Append("<sheetData>");
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            sb.Append($"""<row r="{rowIndex + 1}">""");
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                var reference = $"{ColumnName(columnIndex)}{rowIndex + 1}";
                AppendCell(sb, reference, row[columnIndex], isHeader: rowIndex == 0);
            }

            sb.Append("</row>");
        }

        return sb.Append("</sheetData></worksheet>").ToString();
    }

    private static void AppendCell(StringBuilder sb, string reference, object? value, bool isHeader)
    {
        switch (value)
        {
            case null:
                return;
            case int or long or double or decimal or float:
                sb.Append(
                    CultureInfo.InvariantCulture,
                    $"""<c r="{reference}"><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>"""
                );
                return;
            case XlsxLink link when link.Url.Length < 240 && !string.IsNullOrWhiteSpace(link.Url):
                var url = Escape(link.Url.Replace("\"", "\"\"", StringComparison.Ordinal));
                var text = Escape(link.Text.Replace("\"", "\"\"", StringComparison.Ordinal));
                sb.Append(
                    $"""<c r="{reference}" s="2" t="str"><f>HYPERLINK("{url}","{text}")</f><v>{text}</v></c>"""
                );
                return;
            case XlsxLink link:
                AppendCell(sb, reference, link.Url, isHeader);
                return;
            default:
                var style = isHeader ? "1" : "3";
                sb.Append(
                    $"""<c r="{reference}" s="{style}" t="inlineStr"><is><t xml:space="preserve">{Escape(Convert.ToString(value, CultureInfo.InvariantCulture))}</t></is></c>"""
                );
                return;
        }
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        index++;
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            name = (char)('A' + remainder) + name;
            index = (index - 1) / 26;
        }

        return name;
    }

    private static string SafeSheetName(string name, int index)
    {
        var cleaned = new string(name.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = $"Hoja{index + 1}";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
            {
                continue;
            }

            sb.Append(
                c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '"' => "&quot;",
                    _ => c.ToString(),
                }
            );
        }

        return sb.ToString();
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes);
    }
}
