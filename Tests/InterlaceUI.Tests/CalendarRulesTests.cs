using Application.Features.CalendarManager;

namespace InterlaceUI.Tests;

public class CalendarRulesTests
{
    private static readonly DateTime Start = new(2026, 7, 1, 10, 0, 0);

    private static CalendarActivity Act(string id, bool open = true, bool deleted = false, bool mine = true, DateTime? start = null, string subject = "Call Acme") =>
        new(id, subject, null, start ?? Start, null, open, deleted, mine);

    [Fact]
    public void Outlook_event_is_sent_in_utc_with_a_title_and_end()
    {
        var e = CalendarRules.BuildOutlookEvent(Act("a1", subject: ""));
        Assert.Equal("(no title)", e["subject"]);
        var start = (Dictionary<string, object?>)e["start"]!;
        var end = (Dictionary<string, object?>)e["end"]!;
        Assert.Equal("UTC", start["timeZone"]);
        Assert.Equal("UTC", end["timeZone"]);
        Assert.True(DateTime.Parse((string)end["dateTime"]!) - DateTime.Parse((string)start["dateTime"]!) == TimeSpan.FromMinutes(CalendarRules.DefaultMinutes));
    }

    [Fact]
    public void A_new_open_activity_is_created()
    {
        var plan = CalendarRules.Plan(new[] { Act("a1") }, Array.Empty<CalendarActivity>(), Array.Empty<CalendarLink>());
        Assert.Single(plan.Upserts);
        Assert.Null(plan.Upserts[0].ExistingEventId);
        Assert.Empty(plan.Deletes);
    }

    [Fact]
    public void An_unchanged_activity_is_not_sent_again_but_a_changed_one_is_updated()
    {
        var a = Act("a1");
        var link = new CalendarLink("l1", "a1", "ev1", CalendarRules.Hash(a));
        Assert.Empty(CalendarRules.Plan(new[] { a }, new[] { a }, new[] { link }).Upserts);

        var changed = Act("a1", subject: "Call Acme about renewal");
        var plan = CalendarRules.Plan(new[] { changed }, new[] { changed }, new[] { link });
        Assert.Single(plan.Upserts);
        Assert.Equal("ev1", plan.Upserts[0].ExistingEventId);
    }

    [Theory]
    [InlineData(false, false, true)]   // done or cancelled
    [InlineData(true, true, true)]     // deleted
    [InlineData(true, false, false)]   // reassigned to someone else
    public void Events_are_removed_when_the_activity_no_longer_applies(bool open, bool deleted, bool mine)
    {
        var a = Act("a1", open, deleted, mine);
        var link = new CalendarLink("l1", "a1", "ev1", "x");
        var plan = CalendarRules.Plan(Array.Empty<CalendarActivity>(), new[] { a }, new[] { link });
        Assert.Single(plan.Deletes);
        Assert.Equal("ev1", plan.Deletes[0].EventId);
    }

    [Fact]
    public void A_link_to_a_missing_activity_is_removed()
    {
        var plan = CalendarRules.Plan(Array.Empty<CalendarActivity>(), Array.Empty<CalendarActivity>(), new[] { new CalendarLink("l1", "gone", "ev1", "x") });
        Assert.Single(plan.Deletes);
    }

    [Fact]
    public void One_run_sends_at_most_the_limit()
    {
        var many = Enumerable.Range(0, 80).Select(i => Act("a" + i)).ToList();
        Assert.Equal(CalendarRules.MaxUpsertsPerRun, CalendarRules.Plan(many, Array.Empty<CalendarActivity>(), Array.Empty<CalendarLink>()).Upserts.Count);
    }

    [Fact]
    public void Event_has_a_default_length_and_an_offset_in_its_times()
    {
        var ev = CalendarRules.BuildEvent(Act("a1"));
        var start = (Dictionary<string, object?>)ev["start"]!;
        var end = (Dictionary<string, object?>)ev["end"]!;
        Assert.Matches(@"^2026-07-01T10:00:00[+-]\d\d:\d\d$", (string)start["dateTime"]!);
        Assert.Matches(@"T10:30:00[+-]\d\d:\d\d$", (string)end["dateTime"]!);
        Assert.Equal("Call Acme", ev["summary"]);
    }
}
