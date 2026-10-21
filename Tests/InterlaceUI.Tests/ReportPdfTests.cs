using System.Text;
using Application.Features.ReportManager;

namespace InterlaceUI.Tests;

public class ReportPdfTests
{
    private static string Text(byte[] b) => Encoding.Latin1.GetString(b);

    private static byte[] Make(int rows, string cell = "x")
    {
        var data = Enumerable.Range(0, rows).Select(i => (IReadOnlyList<string>)new List<string> { cell + i, "b" }).ToList();
        return ReportPdf.Build("Title", "sub", new List<string> { "A", "B" }, data);
    }

    [Fact]
    public void Output_is_a_wellformed_pdf_shell()
    {
        var t = Text(Make(3));
        Assert.StartsWith("%PDF-1.4", t);
        Assert.EndsWith("%%EOF\n", t);
        Assert.Contains("/Type /Catalog", t);
        Assert.Contains("/Count 1", t);
    }

    [Fact]
    public void Startxref_points_at_the_xref_table()
    {
        var b = Make(5);
        var t = Text(b);
        var at = long.Parse(t[(t.LastIndexOf("startxref\n") + 10)..].Split('\n')[0]);
        Assert.Equal("xref", t.Substring((int)at, 4));
    }

    [Fact]
    public void Many_rows_make_several_pages_and_the_header_repeats()
    {
        var rows = ReportPdf.RowsPerPage * 2 + 1;
        var t = Text(Make(rows));
        Assert.Contains("/Count 3", t);
        Assert.Contains("Page 3 of 3", t);
    }

    [Fact]
    public void Rows_over_the_limit_are_cut_with_a_note()
    {
        var t = Text(Make(ReportPdf.MaxRows + 5));
        Assert.Contains("first " + ReportPdf.MaxRows + " of " + (ReportPdf.MaxRows + 5), t);
    }

    [Fact]
    public void Text_is_escaped_and_unsupported_characters_become_question_marks()
    {
        Assert.Equal("a\\(b\\)c\\\\d", ReportPdf.Encode("a(b)c\\d"));
        Assert.Equal("??", ReportPdf.Encode("\u0939\u093f"));
        Assert.Equal("caf\u00e9", ReportPdf.Encode("caf\u00e9"));
        Assert.Equal("a b", ReportPdf.Encode("a\nb"));
    }

    [Fact]
    public void Long_text_is_shortened()
    {
        Assert.Equal("abcdefg...", ReportPdf.Fit("abcdefghijklmnop", 10));
        Assert.Equal("short", ReportPdf.Fit("short", 10));
    }

    [Theory]
    [InlineData("Csv", "Csv")]
    [InlineData("pdf", "Pdf")]
    [InlineData("BOTH", "Both")]
    [InlineData(null, "Csv")]
    [InlineData("exe", "Csv")]
    public void Attach_choice_is_normalised(string? input, string expected) => Assert.Equal(expected, ReportScheduleRules.NormalizeAttach(input));

    [Fact]
    public void Pdf_file_name_uses_the_report_name()
    {
        Assert.Equal("My_report.pdf", ReportScheduleRules.SafeFileName("My report", ".pdf"));
    }
}
