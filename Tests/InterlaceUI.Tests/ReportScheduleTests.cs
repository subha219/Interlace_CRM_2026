using Application.Features.ReportManager;

namespace InterlaceUI.Tests;

public class ReportScheduleTests
{
    // Wednesday 2026-06-10 10:30
    private static readonly DateTime Now = new(2026, 6, 10, 10, 30, 0);

    [Fact]
    public void Daily_slot_is_today_after_the_hour_and_yesterday_before_it()
    {
        Assert.Equal(new DateTime(2026, 6, 10, 8, 0, 0), ReportScheduleRules.LatestSlot("Daily", 0, 8, Now));
        Assert.Equal(new DateTime(2026, 6, 9, 18, 0, 0), ReportScheduleRules.LatestSlot("Daily", 0, 18, Now));
    }

    [Fact]
    public void Weekly_slot_is_the_most_recent_matching_weekday()
    {
        Assert.Equal(new DateTime(2026, 6, 8, 9, 0, 0), ReportScheduleRules.LatestSlot("Weekly", 1, 9, Now));   // Monday
        Assert.Equal(new DateTime(2026, 6, 10, 9, 0, 0), ReportScheduleRules.LatestSlot("Weekly", 3, 9, Now));  // today (Wednesday), already passed
        Assert.Equal(new DateTime(2026, 6, 3, 12, 0, 0), ReportScheduleRules.LatestSlot("Weekly", 3, 12, Now)); // today not yet reached
    }

    [Fact]
    public void Monthly_slot_falls_back_to_last_month_when_not_reached()
    {
        Assert.Equal(new DateTime(2026, 6, 5, 8, 0, 0), ReportScheduleRules.LatestSlot("Monthly", 5, 8, Now));
        Assert.Equal(new DateTime(2026, 5, 20, 8, 0, 0), ReportScheduleRules.LatestSlot("Monthly", 20, 8, Now));
    }

    [Fact]
    public void A_schedule_fires_once_per_slot()
    {
        var lastRunBefore = new DateTime(2026, 6, 9, 7, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var lastRunAfter = new DateTime(2026, 6, 10, 8, 5, 0, DateTimeKind.Local).ToUniversalTime();
        Assert.True(ReportScheduleRules.IsDue("Daily", 0, 8, lastRunBefore, Now));
        Assert.False(ReportScheduleRules.IsDue("Daily", 0, 8, lastRunAfter, Now));
        Assert.True(ReportScheduleRules.IsDue("Daily", 0, 8, null, Now));
    }

    [Fact]
    public void Recipients_are_split_deduplicated_and_checked()
    {
        var list = ReportScheduleRules.ParseRecipients("a@x.com; b@x.com, A@X.com\nc@x.com");
        Assert.Equal(3, list.Count);
        Assert.True(ReportScheduleRules.IsEmail("a@x.com"));
        Assert.False(ReportScheduleRules.IsEmail("not-an-email"));
    }

    [Fact]
    public void Saved_page_definition_becomes_a_run_request()
    {
        var json = "{\"entity\":\"Lead\",\"mode\":\"list\",\"columns\":[\"title\",\"stage\"],\"filters\":[{\"field\":\"stage\",\"op\":\"equals\",\"value\":\"Closed\"}],\"sortBy\":\"title\",\"sortDescending\":true}";
        var r = ReportScheduleRules.ToRequest(json, "Lead");
        Assert.NotNull(r);
        Assert.Equal(new[] { "title", "stage" }, r!.Columns);
        Assert.Single(r.Filters!);
        Assert.True(r.SortDescending);
        Assert.Null(ReportScheduleRules.ToRequest("{bad", "Lead"));
    }

    [Fact]
    public void Html_body_encodes_values()
    {
        var res = new RunReportResult { Total = 1, Columns = new() { new ReportColumnDto { Key = "n", Label = "Name" } }, Rows = new() { new() { "<script>x</script>" } } };
        var html = ReportScheduleRules.BuildHtml("R&D", res);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("R&amp;D", html);
    }
}
