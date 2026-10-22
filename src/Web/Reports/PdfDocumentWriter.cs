using System.Globalization;
using System.Text;

namespace AnalisisSentimiento.Web.Reports;

public sealed class PdfDocumentWriter
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 42;
    private const double ContentWidth = PageWidth - Margin * 2;

    private readonly List<PdfPage> _pages = [];
    private PdfPage _current = null!;
    private double _cursorY;

    public PdfDocumentWriter()
    {
        NewPage();
    }

    public void Title(string text)
    {
        Paragraph(text, 18, bold: true, color: (0.06, 0.30, 0.51), spacingAfter: 6);
    }

    public void Heading(string text)
    {
        EnsureSpace(30);
        _cursorY -= 6;
        Paragraph(text, 13, bold: true, color: (0.06, 0.30, 0.51), spacingAfter: 2);
        Rule();
    }

    public void Paragraph(
        string text,
        double size = 10,
        bool bold = false,
        (double R, double G, double B)? color = null,
        double spacingAfter = 4,
        double indent = 0
    )
    {
        var lineHeight = size * 1.35;
        foreach (var line in Wrap(text, size, ContentWidth - indent, bold))
        {
            EnsureSpace(lineHeight);
            _cursorY -= lineHeight;
            DrawText(Margin + indent, _cursorY, line, size, bold, color ?? (0.12, 0.12, 0.14));
        }

        _cursorY -= spacingAfter;
    }

    public void Link(string label, string url, double size = 9, double indent = 0)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var lineHeight = size * 1.35;
        EnsureSpace(lineHeight);
        _cursorY -= lineHeight;
        var text = Truncate(label, size, ContentWidth - indent, false);
        DrawText(Margin + indent, _cursorY, text, size, false, (0.02, 0.39, 0.76));
        var width = MeasureWidth(text, size, false);
        _current.Links.Add(
            new PdfLink(
                Margin + indent,
                _cursorY - 2,
                Margin + indent + width,
                _cursorY + size,
                url
            )
        );
        _cursorY -= 2;
    }

    public void StackedBar(string label, int positive, int neutral, int negative)
    {
        const double barHeight = 11;
        const double labelWidth = 120;
        EnsureSpace(barHeight + 10);
        _cursorY -= barHeight + 6;

        var total = Math.Max(1, positive + neutral + negative);
        DrawText(
            Margin,
            _cursorY + 2,
            Truncate(label, 9, labelWidth - 6, true),
            9,
            true,
            (0.12, 0.12, 0.14)
        );

        var x = Margin + labelWidth;
        var available = ContentWidth - labelWidth - 120;
        foreach (
            var (value, color) in new[]
            {
                (positive, (0.13, 0.62, 0.40)),
                (neutral, (0.55, 0.58, 0.63)),
                (negative, (0.85, 0.25, 0.27)),
            }
        )
        {
            var width = available * value / total;
            if (width > 0)
            {
                _current.Content.Append(
                    CultureInfo.InvariantCulture,
                    $"{color.Item1:0.###} {color.Item2:0.###} {color.Item3:0.###} rg {x:0.##} {_cursorY:0.##} {width:0.##} {barHeight:0.##} re f\n"
                );
                x += width;
            }
        }

        DrawText(
            Margin + labelWidth + available + 8,
            _cursorY + 2,
            $"{positive} pos · {neutral} neu · {negative} neg",
            8.5,
            false,
            (0.3, 0.3, 0.33)
        );
    }

    public void Spacer(double height = 8)
    {
        _cursorY -= height;
    }

    public void Rule()
    {
        EnsureSpace(6);
        _cursorY -= 3;
        _current.Content.Append(
            CultureInfo.InvariantCulture,
            $"0.82 0.86 0.9 RG 0.6 w {Margin:0.##} {_cursorY:0.##} m {PageWidth - Margin:0.##} {_cursorY:0.##} l S\n"
        );
        _cursorY -= 4;
    }

    public byte[] Build()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        };
        var pageIds = new List<int>();

        for (var index = 0; index < _pages.Count; index++)
        {
            var page = _pages[index];
            AddFooter(page, index + 1, _pages.Count);

            var annotationIds = new List<int>();
            foreach (var link in page.Links)
            {
                objects.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"<< /Type /Annot /Subtype /Link /Rect [{link.X1:0.##} {link.Y1:0.##} {link.X2:0.##} {link.Y2:0.##}] /Border [0 0 0] /A << /S /URI /URI ({EscapeString(link.Url)}) >> >>"
                    )
                );
                annotationIds.Add(objects.Count);
            }

            var content = page.Content.ToString();
            objects.Add($"<< /Length {Latin1Length(content)} >>\nstream\n{content}endstream");
            var contentId = objects.Count;

            var annots =
                annotationIds.Count == 0
                    ? string.Empty
                    : $" /Annots [{string.Join(" ", annotationIds.Select(id => $"{id} 0 R"))}]";
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R{annots} >>"
            );
            pageIds.Add(objects.Count);
        }

        objects[1] =
            $"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageIds.Count} >>";

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write(output, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefPosition = output.Position;
        var xref = new StringBuilder();
        xref.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            xref.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n");
        }

        xref.Append(
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF\n"
        );
        Write(output, xref.ToString());
        return output.ToArray();
    }

    private void AddFooter(PdfPage page, int number, int total)
    {
        var text = $"Observatorio Territorial ULEAM · Sentia Global · Página {number} de {total}";
        page.Content.Append(
            CultureInfo.InvariantCulture,
            $"BT /F1 8 Tf 0.45 0.45 0.5 rg {Margin:0.##} 24 Td ({EncodeText(text)}) Tj ET\n"
        );
    }

    private void NewPage()
    {
        _current = new PdfPage();
        _pages.Add(_current);
        _cursorY = PageHeight - Margin;
    }

    private void EnsureSpace(double height)
    {
        if (_cursorY - height < Margin + 18)
        {
            NewPage();
        }
    }

    private void DrawText(
        double x,
        double y,
        string text,
        double size,
        bool bold,
        (double R, double G, double B) color
    )
    {
        _current.Content.Append(
            CultureInfo.InvariantCulture,
            $"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {color.R:0.###} {color.G:0.###} {color.B:0.###} rg {x:0.##} {y:0.##} Td ({EncodeText(text)}) Tj ET\n"
        );
    }

    private static IEnumerable<string> Wrap(string text, double size, double width, bool bold)
    {
        foreach (var paragraph in Normalize(text).Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (MeasureWidth(candidate, size, bold) <= width)
                {
                    line.Clear().Append(candidate);
                    continue;
                }

                if (line.Length > 0)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                var remaining = word;
                while (MeasureWidth(remaining, size, bold) > width && remaining.Length > 1)
                {
                    var cut = Truncate(remaining, size, width, bold, ellipsis: false);
                    yield return cut;
                    remaining = remaining[cut.Length..];
                }

                line.Append(remaining);
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }

    private static string Truncate(
        string text,
        double size,
        double width,
        bool bold,
        bool ellipsis = true
    )
    {
        var normalized = Normalize(text).Replace('\n', ' ');
        if (MeasureWidth(normalized, size, bold) <= width)
        {
            return normalized;
        }

        var suffix = ellipsis ? "..." : string.Empty;
        var length = normalized.Length;
        while (length > 1 && MeasureWidth(normalized[..length] + suffix, size, bold) > width)
        {
            length--;
        }

        return normalized[..length] + suffix;
    }

    private static double MeasureWidth(string text, double size, bool bold)
    {
        double units = 0;
        foreach (var c in text)
        {
            units += c switch
            {
                'i' or 'l' or 'j' or '\'' or '.' or ',' or ':' or ';' or '!' or '|' or 'í' => 240,
                'f' or 't' or 'r' or ' ' or '(' or ')' or '-' or '/' => 320,
                'm' or 'w' or 'M' or 'W' or '@' => 840,
                >= 'A' and <= 'Z' or 'Á' or 'É' or 'Í' or 'Ó' or 'Ú' or 'Ñ' => 680,
                >= '0' and <= '9' => 556,
                _ => 530,
            };
        }

        return units / 1000 * size * (bold ? 1.06 : 1);
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            if (char.IsSurrogate(c))
            {
                continue;
            }

            sb.Append(
                c switch
                {
                    '\t' => ' ',
                    '→' => '>',
                    _ when c < 0x20 && c != '\n' => ' ',
                    _ => c,
                }
            );
        }

        return sb.ToString();
    }

    private static string EncodeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in Normalize(text))
        {
            var code = ToWinAnsi(c);
            if (code is null)
            {
                continue;
            }

            var ch = (char)code.Value;
            switch (ch)
            {
                case '(':
                case ')':
                case '\\':
                    sb.Append('\\').Append(ch);
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string EscapeString(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static byte? ToWinAnsi(char c) =>
        c switch
        {
            '€' => 0x80,
            '‘' => 0x91,
            '’' => 0x92,
            '“' => 0x93,
            '”' => 0x94,
            '•' or '·' => 0xB7,
            '–' => 0x96,
            '—' => 0x97,
            '…' => 0x85,
            '\n' => 0x20,
            _ when c >= 0x20 && c <= 0x7E => (byte)c,
            _ when c >= 0xA0 && c <= 0xFF => (byte)c,
            _ => null,
        };

    private static int Latin1Length(string content) => content.Length;

    private static void Write(Stream stream, string text)
    {
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            bytes[i] = text[i] <= 0xFF ? (byte)text[i] : (byte)'?';
        }

        stream.Write(bytes);
    }

    private sealed class PdfPage
    {
        public StringBuilder Content { get; } = new();
        public List<PdfLink> Links { get; } = [];
    }

    private sealed record PdfLink(double X1, double Y1, double X2, double Y2, string Url);
}
