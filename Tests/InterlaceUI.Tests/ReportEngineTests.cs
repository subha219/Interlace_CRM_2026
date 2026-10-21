using Application.Features.ReportManager;

namespace InterlaceUI.Tests;

public class ReportEngineTests
{
    private static Dictionary<string, object?> Row(string stage, double amount, DateTime? date = null) =>
        new() { ["stage"] = stage, ["amount"] = amount, ["created"] = date };

    [Fact]
    public void Filters_compare_by_type()
    {
        var row = Row("Proposal", 500, new DateTime(2026, 3, 10));
        Assert.True(ReportEngine.Matches(row, new ReportFilter("amount", "gt", "100"), "number"));
        Assert.False(ReportEngine.Matches(row, new ReportFilter("amount", "lt", "100"), "number"));
        Assert.True(ReportEngine.Matches(row, new ReportFilter("created", "gt", "2026-03-01"), "date"));
        Assert.False(ReportEngine.Matches(row, new ReportFilter("created", "lt", "2026-03-01"), "date"));
        Assert.True(ReportEngine.Matches(row, new ReportFilter("stage", "equals", "proposal"), "text"));
        Assert.True(ReportEngine.Matches(row, new ReportFilter("stage", "contains", "ropos"), "text"));
    }

    [Fact]
    public void Empty_checks_and_unknown_operators()
    {
        var row = Row("A", 1);
        Assert.True(ReportEngine.Matches(row, new ReportFilter("created", "is_empty", null), "date"));
        Assert.False(ReportEngine.Matches(row, new ReportFilter("stage", "is_empty", null), "text"));
        Assert.False(ReportEngine.Matches(row, new ReportFilter("stage", "bogus", "x"), "text"));
    }

    [Fact]
    public void Aggregates_count_sum_and_average()
    {
        var rows = new IReadOnlyDictionary<string, object?>[] { Row("A", 10), Row("A", 30), Row("B", 5) };
        Assert.Equal(3, ReportEngine.Aggregate(rows, "count"));
        Assert.Equal(45, ReportEngine.Aggregate(rows, "sum:amount"));
        Assert.Equal(15, ReportEngine.Aggregate(rows, "avg:amount"));
        Assert.Equal(0, ReportEngine.Aggregate(Array.Empty<IReadOnlyDictionary<string, object?>>(), "avg:amount"));
    }

    [Fact]
    public void Group_keys_bucket_dates_by_month_and_name_empty_values()
    {
        Assert.Equal("2026-03", ReportEngine.GroupKey(new DateTime(2026, 3, 31), "date"));
        Assert.Equal("(none)", ReportEngine.GroupKey(null, "text"));
        Assert.Equal("(none)", ReportEngine.GroupKey("  ", "text"));
        Assert.Equal("Won", ReportEngine.GroupKey("Won", "text"));
    }

    [Fact]
    public void Every_entity_field_list_has_unique_keys()
    {
        foreach (var (entity, fields) in ReportCatalog.Fields)
        {
            Assert.Equal(fields.Count, fields.Select(f => f.Key).Distinct().Count());
            Assert.True(ReportCatalog.ModuleRole.ContainsKey(entity));
            Assert.True(ReportCatalog.Labels.ContainsKey(entity));
        }
    }
}
