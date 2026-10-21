namespace Application.Features.ImportManager.Commands;

public class ImportResult
{
    public bool Preview { get; init; }
    public int TotalRows { get; init; }
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public List<ImportRowIssue> Issues { get; init; } = new();
}

/// <summary>Header lookup and cell cleaning shared by the CSV imports.</summary>
public sealed class CsvTable
{
    private readonly List<string> _header;
    public List<List<string>> Rows { get; }

    public CsvTable(string? csv)
    {
        Rows = CsvReader.Parse(csv ?? string.Empty);
        if (Rows.Count < 2)
        {
            throw new Exception("The file needs a header row and at least one data row.");
        }
        _header = Rows[0].Select(Normalize).ToList();
    }

    public int Col(params string[] names)
    {
        foreach (var n in names)
        {
            var i = _header.IndexOf(n);
            if (i >= 0) return i;
        }
        return -1;
    }

    public static string Cell(List<string> row, int col, int max = 255) =>
        col >= 0 && col < row.Count ? Truncate(row[col].Trim(), max) : string.Empty;

    public static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    public static bool? ParseConsent(string s)
    {
        var v = s.Trim().ToLowerInvariant();
        if (v is "yes" or "y" or "true" or "1" or "agreed" or "opted in") return true;
        if (v is "no" or "n" or "false" or "0" or "opted out" or "unsubscribed") return false;
        return null;
    }

    private static string Normalize(string h) =>
        new string((h ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string Truncate(string s, int max) => s.Length > max ? s.Substring(0, max) : s;
}
