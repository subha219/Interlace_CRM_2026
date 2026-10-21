using System.Globalization;

namespace Application.Features.ReportManager;

public record ReportField(string Key, string Label, string Type);
public record ReportFilter(string Field, string Op, string? Value);

/// <summary>Filtering, grouping and totals over plain row dictionaries. No database access, so it is easy to test.</summary>
public static class ReportEngine
{
    public static readonly string[] Operators = { "equals", "not_equals", "contains", "gt", "lt", "is_empty", "is_not_empty" };
    public const int MaxRows = 20000;

    public static bool Matches(IReadOnlyDictionary<string, object?> row, ReportFilter f, string type)
    {
        row.TryGetValue(f.Field, out var raw);
        var text = Text(raw, type);
        var expected = f.Value ?? "";

        switch (f.Op)
        {
            case "is_empty": return string.IsNullOrWhiteSpace(text);
            case "is_not_empty": return !string.IsNullOrWhiteSpace(text);
            case "contains": return text.Contains(expected, StringComparison.OrdinalIgnoreCase);
            case "equals": return Equal(text, expected, type);
            case "not_equals": return !Equal(text, expected, type);
            case "gt":
            case "lt":
                var cmp = Compare(raw, expected, type);
                if (cmp == null) return false;
                return f.Op == "gt" ? cmp > 0 : cmp < 0;
            default: return false;
        }
    }

    private static bool Equal(string text, string expected, string type) =>
        type == "number" && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            ? a == b
            : string.Equals(text, expected, StringComparison.OrdinalIgnoreCase);

    private static int? Compare(object? raw, string expected, string type)
    {
        if (type == "number")
        {
            return Number(raw) is double a && double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? a.CompareTo(b) : null;
        }
        if (type == "date")
        {
            return raw is DateTime d && DateTime.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) ? d.Date.CompareTo(e.Date) : null;
        }
        return null;
    }

    public static double? Number(object? v) => v switch
    {
        null => null,
        double d => d,
        int i => i,
        long l => l,
        decimal m => (double)m,
        float f => f,
        _ => double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : null
    };

    public static string Text(object? v, string type) => v switch
    {
        null => "",
        DateTime d => type == "date" ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
    };

    /// <summary>Group key: dates fall into months, empty values into "(none)".</summary>
    public static string GroupKey(object? v, string type)
    {
        if (v is DateTime d && type == "date") return d.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var t = Text(v, type);
        return string.IsNullOrWhiteSpace(t) ? "(none)" : t;
    }

    /// <summary>metric is "count", "sum:field" or "avg:field".</summary>
    public static double Aggregate(IEnumerable<IReadOnlyDictionary<string, object?>> rows, string metric)
    {
        var list = rows.ToList();
        if (metric == "count" || string.IsNullOrWhiteSpace(metric)) return list.Count;

        var parts = metric.Split(':', 2);
        var values = list.Select(r => r.TryGetValue(parts[1], out var v) ? Number(v) : null).Where(x => x != null).Select(x => x!.Value).ToList();
        return parts[0] switch
        {
            "sum" => Math.Round(values.Sum(), 2),
            "avg" => values.Count == 0 ? 0 : Math.Round(values.Average(), 2),
            "min" => values.Count == 0 ? 0 : values.Min(),
            "max" => values.Count == 0 ? 0 : values.Max(),
            _ => list.Count
        };
    }
}
