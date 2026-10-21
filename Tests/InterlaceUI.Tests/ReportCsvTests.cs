using Application.Features.ReportManager;

namespace InterlaceUI.Tests;

public class ReportCsvTests
{
    private static RunReportResult Result(params object?[][] rows) => new()
    {
        Columns = new() { new ReportColumnDto { Key = "a", Label = "Name" }, new ReportColumnDto { Key = "b", Label = "Amount" } },
        Rows = rows.Select(r => r.ToList()).ToList()
    };

    [Fact]
    public void Csv_has_header_and_quotes_special_characters()
    {
        var csv = ReportScheduleRules.BuildCsv(Result(new object?[] { "Acme, Inc", 12.5 }, new object?[] { "He said \"hi\"", null }));
        Assert.Equal("Name,Amount\r\n\"Acme, Inc\",12.5\r\n\"He said \"\"hi\"\"\",\r\n", csv);
    }

    [Theory]
    [InlineData("=cmd|' /C calc'!A0")]
    [InlineData("+1+1x")]
    [InlineData("@SUM(A1)")]
    [InlineData("-2+3x")]
    public void Formula_like_text_is_defused(string text)
    {
        var csv = ReportScheduleRules.BuildCsv(Result(new object?[] { text, 1 }));
        Assert.Contains("'" + text.Split(',')[0], csv.Replace("\"", ""));
        Assert.DoesNotContain("\r\n" + text[0], csv);
    }

    [Fact]
    public void Negative_numbers_stay_numbers() =>
        Assert.Contains("\r\nx,-5\r\n", ReportScheduleRules.BuildCsv(Result(new object?[] { "x", -5 })));

    [Theory]
    [InlineData("Q1 Sales / Leads?", "Q1_Sales__Leads.csv")]
    [InlineData("", "report.csv")]
    [InlineData("../../etc", "etc.csv")]
    public void File_names_are_safe(string input, string expected) => Assert.Equal(expected, ReportScheduleRules.SafeFileName(input));
}
