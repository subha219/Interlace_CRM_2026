using System.Globalization;
using System.Text;

namespace Application.Features.ReportManager;

/// <summary>
/// Writes a plain table as a PDF with no outside library: A4 landscape, built-in Helvetica, header repeated on every page.
/// The built-in fonts cover Western European letters only; any other character prints as "?" (the CSV keeps every character).
/// </summary>
public static class ReportPdf
{
    public const int MaxRows = 2000;
    private const double PageW = 842, PageH = 595, Margin = 30, FontSize = 7.5, RowH = 11, CharW = 0.5;

    private static string Cell(object? v) => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";

    /// <summary>Bytes for a PDF string: Latin-1 range kept, anything else becomes '?', and ( ) \ are escaped.</summary>
    public static string Encode(string? text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text ?? "")
        {
            var c = ch;
            if (c is '\r' or '\n' or '\t') c = ' ';
            if (c < 0x20 || c == 0x7F || (c > 0x7E && c < 0xA0) || c > 0xFF) c = '?';
            if (c is '(' or ')' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static string Fit(string? text, int maxChars)
    {
        var s = (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        return s.Length <= maxChars ? s : maxChars <= 3 ? s[..Math.Max(0, maxChars)] : s[..(maxChars - 3)] + "...";
    }

    public static int RowsPerPage => (int)((PageH - 2 * Margin - 50) / RowH);

    public static byte[] Build(string title, string subtitle, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var shown = rows.Take(MaxRows).ToList();
        var usable = PageW - 2 * Margin;

        var widths = new double[headers.Count];
        for (var i = 0; i < headers.Count; i++)
        {
            var longest = Math.Max(headers[i].Length, shown.Take(200).Select(r => i < r.Count ? r[i].Length : 0).DefaultIfEmpty(0).Max());
            widths[i] = Math.Clamp(longest, 4, 40) * FontSize * CharW + 8;
        }
        var total = widths.Sum();
        if (total > usable) for (var i = 0; i < widths.Length; i++) widths[i] *= usable / total;

        var perPage = RowsPerPage;
        var pageCount = Math.Max(1, (int)Math.Ceiling(shown.Count / (double)perPage));
        var pages = new List<string>();
        for (var p = 0; p < pageCount; p++)
        {
            var sb = new StringBuilder();
            double y = PageH - Margin;
            sb.Append($"BT /F2 12 Tf {F(Margin)} {F(y - 10)} Td ({Encode(Fit(title, 90))}) Tj ET\n");
            sb.Append($"BT /F1 8 Tf {F(Margin)} {F(y - 22)} Td ({Encode(Fit(subtitle, 160))}) Tj ET\n");
            y -= 42;
            void Row(IReadOnlyList<string> cells, bool head)
            {
                if (head) sb.Append($"0.93 g {F(Margin)} {F(y - 3)} {F(widths.Sum())} {F(RowH)} re f 0 g\n");
                double x = Margin;
                for (var i = 0; i < widths.Count(); i++)
                {
                    var max = Math.Max(1, (int)((widths[i] - 6) / (FontSize * CharW)));
                    var text = Fit(i < cells.Count ? cells[i] : "", max);
                    sb.Append($"BT /{(head ? "F2" : "F1")} {F(FontSize)} Tf {F(x + 3)} {F(y)} Td ({Encode(text)}) Tj ET\n");
                    x += widths[i];
                }
                y -= RowH;
            }
            Row(headers, true);
            foreach (var r in shown.Skip(p * perPage).Take(perPage)) Row(r, false);
            var footer = $"Page {p + 1} of {pageCount}" + (rows.Count > shown.Count ? $" - first {shown.Count} of {rows.Count} rows; the CSV or the CRM has all of them" : "");
            sb.Append($"BT /F1 7 Tf {F(Margin)} {F(Margin - 14)} Td ({Encode(footer)}) Tj ET\n");
            pages.Add(sb.ToString());
        }

        //>>> objects: 1 catalog, 2 pages, 3 and 4 fonts, then (page, content) pairs
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [" + string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{5 + 2 * i} 0 R")) + $"] /Count {pageCount} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };
        for (var i = 0; i < pageCount; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(PageW)} {F(PageH)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + 2 * i} 0 R >>");
            var content = pages[i];
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}endstream");
        }

        using var ms = new MemoryStream();
        void Write(string s) { var b = Encoding.Latin1.GetBytes(s); ms.Write(b, 0, b.Length); }
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Write(o.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    public static byte[] Build(string title, RunReportResult result)
    {
        var rows = result.Rows.Select(r => (IReadOnlyList<string>)r.Select(Cell).ToList()).ToList();
        var sub = $"{result.Total} record(s) - {DateTime.Now:yyyy-MM-dd HH:mm}";
        return Build(title, sub, result.Columns.Select(c => c.Label ?? "").ToList(), rows);
    }

    private static string F(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
}
