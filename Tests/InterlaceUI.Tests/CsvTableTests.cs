using Application.Features.ImportManager.Commands;

namespace InterlaceUI.Tests;

public class CsvTableTests
{
    [Fact]
    public void Header_names_are_matched_ignoring_case_spaces_and_punctuation()
    {
        var table = new CsvTable("Job Title,E-mail\r\nBoss,a@b.com\r\n");
        Assert.Equal(0, table.Col("jobtitle"));
        Assert.Equal(1, table.Col("email"));
        Assert.Equal(-1, table.Col("phone"));
    }

    [Fact]
    public void Quoted_fields_with_commas_and_semicolon_delimiter_are_read()
    {
        var comma = new CsvTable("Name,Notes\r\nAcme,\"Hello, world\"\r\n");
        Assert.Equal("Hello, world", CsvTable.Cell(comma.Rows[1], 1));

        var semi = new CsvTable("Name;City\nAcme;Pune\n");
        Assert.Equal("Pune", CsvTable.Cell(semi.Rows[1], 1));
    }

    [Fact]
    public void File_without_data_rows_is_rejected()
    {
        Assert.Throws<Exception>(() => new CsvTable("Name,Email\r\n"));
    }

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("opted out", false)]
    [InlineData("", null)]
    [InlineData("maybe", null)]
    public void Consent_values_are_parsed(string input, bool? expected)
    {
        Assert.Equal(expected, CsvTable.ParseConsent(input));
    }
}

public class EmailRendererTests
{
    private static readonly Dictionary<string, string?> Values = new() { ["name"] = "Acme <b>", ["sender"] = "Sam" };

    [Fact]
    public void Plain_text_is_encoded_and_line_breaks_kept()
    {
        var html = Application.Features.EmailManager.EmailRenderer.Render("Hi {{name}},\nThanks, {{ sender }}", Values);
        Assert.Equal("Hi Acme &lt;b&gt;,<br>Thanks, Sam", html);
    }

    [Fact]
    public void Html_keeps_markup_but_encodes_merged_values()
    {
        var html = Application.Features.EmailManager.EmailRenderer.Render("<p>Hi {{name}} {{missing}}</p>", Values);
        Assert.Equal("<p>Hi Acme &lt;b&gt; </p>", html);
    }

    [Fact]
    public void Subject_is_not_encoded()
    {
        Assert.Equal("Offer for Acme <b>", Application.Features.EmailManager.EmailRenderer.RenderSubject("Offer for {{name}}", Values));
    }
}
