using Application.Features.AgentManager;
using Domain.Enums;

namespace InterlaceUI.Tests;

public class AgentRulesTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Lead_is_stale_after_a_week_without_activity()
    {
        Assert.Null(AgentRules.StaleLeadReason(Now.AddDays(-3), null, Now));
        Assert.Null(AgentRules.StaleLeadReason(Now.AddDays(-30), Now.AddDays(-2), Now));
        Assert.Contains("10 days", AgentRules.StaleLeadReason(Now.AddDays(-30), Now.AddDays(-10), Now));
        Assert.Contains("since the lead was created", AgentRules.StaleLeadReason(Now.AddDays(-9), null, Now));
    }

    [Fact]
    public void Lead_priority_is_high_when_close_is_near() =>
        Assert.Equal(new[] { "High", "Normal", "Normal" }, new[] { AgentRules.LeadPriority(Now.AddDays(5), Now), AgentRules.LeadPriority(Now.AddDays(60), Now), AgentRules.LeadPriority(null, Now) });

    [Theory]
    [InlineData(10, 60, 10)]
    [InlineData(60, 60, 60)]
    [InlineData(61, 60, null)]
    [InlineData(45, 15, null)]
    [InlineData(25, 15, 25)]
    [InlineData(-1, 60, null)]
    public void Renewal_window(int daysAhead, int notice, int? expected) =>
        Assert.Equal(expected, AgentRules.RenewalDaysLeft(Now.Date.AddDays(daysAhead), notice, Now));

    [Fact]
    public void Contact_gaps_are_listed()
    {
        Assert.Equal(new[] { "e-mail", "phone number" }, AgentRules.ContactGaps(null, " "));
        Assert.Empty(AgentRules.ContactGaps("a@b.com", "123"));
    }

    [Theory]
    [InlineData("Site is down", "Our production down since noon", CasePriority.Critical)]
    [InlineData("Invoice question", "I cannot open the invoice", CasePriority.High)]
    [InlineData("Feature request", "It would be nice to have dark mode", CasePriority.Low)]
    [InlineData("Hello", "Please update my address", CasePriority.Normal)]
    public void Keyword_triage(string title, string text, CasePriority expected) => Assert.Equal(expected, AgentRules.RuleTriage(title, text).Priority);

    [Theory]
    [InlineData("{\"priority\":\"High\",\"reason\":\"blocked\"}", CasePriority.High)]
    [InlineData("Sure! {\"priority\":\"critical\"} done", CasePriority.Critical)]
    public void Model_answer_is_parsed_strictly(string answer, CasePriority expected) => Assert.Equal(expected, AgentRules.ParseTriage(answer)!.Priority);

    [Theory]
    [InlineData("")]
    [InlineData("I think it is high")]
    [InlineData("{\"priority\":\"Urgent\"}")]
    [InlineData("{\"priority\":7}")]
    [InlineData("{\"priority\":\"99\"}")]
    public void Bad_model_answers_are_ignored(string answer) => Assert.Null(AgentRules.ParseTriage(answer));

    [Fact]
    public void Payloads_round_trip_and_reject_damage()
    {
        Assert.Equal((2, "High"), AgentRules.ParseTaskPayload(AgentRules.TaskPayload(2, "High")));
        Assert.Null(AgentRules.ParseTaskPayload("{\"dueInDays\":999,\"priority\":\"High\"}"));
        Assert.Null(AgentRules.ParseTaskPayload("{\"dueInDays\":1,\"priority\":\"Urgent\"}"));
        Assert.Null(AgentRules.ParseTaskPayload("nonsense"));
        Assert.Equal((1, 2), AgentRules.ParsePriorityPayload(AgentRules.PriorityPayload(1, 2)));
        Assert.Null(AgentRules.ParsePriorityPayload("{\"from\":1,\"to\":42}"));
    }

    [Fact]
    public void Catalog_is_complete_and_keys_are_unique()
    {
        Assert.Equal(4, AgentRules.Catalog.Count);
        Assert.Equal(AgentRules.Catalog.Count, AgentRules.Catalog.Select(a => a.Key).Distinct().Count());
        Assert.All(AgentRules.Catalog, a => Assert.False(string.IsNullOrEmpty(a.RequiredRole)));
        Assert.Equal("support-triage:SupportCase:1", AgentRules.DedupKey("support-triage", "SupportCase", "1"));
    }

    private static readonly List<(string Id, string Name)> Cats = new() { ("1", "Billing"), ("2", "Billing disputes"), ("3", "IT") };

    [Fact]
    public void Category_is_matched_by_name_and_the_longest_name_wins()
    {
        Assert.Equal("2", AgentRules.MatchCategory("Question", "I have a billing disputes issue", Cats)?.Id);
        Assert.Equal("1", AgentRules.MatchCategory("Billing question", null, Cats)?.Id);
        Assert.Null(AgentRules.MatchCategory("My printer", "is broken", Cats));   // "IT" is too short to match by name
        Assert.Null(AgentRules.MatchCategory("x", "y", new List<(string, string)>()));
    }

    [Fact]
    public void Triage_answer_may_carry_a_category()
    {
        var t = AgentRules.ParseTriage("{\"priority\":\"High\",\"reason\":\"Blocked\",\"category\":\" Billing \"}");
        Assert.Equal("Billing", t!.Category);
        Assert.Null(AgentRules.ParseTriage("{\"priority\":\"High\",\"reason\":\"x\"}")!.Category);
        Assert.Null(AgentRules.ParseTriage("{\"priority\":\"High\",\"reason\":\"x\",\"category\":\"\"}")!.Category);
    }

    [Fact]
    public void Category_payload_round_trips_and_rejects_damage()
    {
        Assert.Equal(("c1", "Billing"), AgentRules.ParseCategoryPayload(AgentRules.CategoryPayload("c1", "Billing")));
        Assert.Null(AgentRules.ParseCategoryPayload("{\"id\":\"\",\"name\":\"x\"}"));
        Assert.Null(AgentRules.ParseCategoryPayload("nonsense"));
    }

    [Fact]
    public void A_category_suggestion_does_not_block_a_priority_suggestion_for_the_same_case()
    {
        Assert.NotEqual(AgentRules.DedupKey("support-triage", "SupportCase", "1", "SetPriority"), AgentRules.DedupKey("support-triage", "SupportCase", "1", "SetCategory"));
        Assert.Equal(AgentRules.DedupKey("support-triage", "SupportCase", "1"), AgentRules.DedupKey("support-triage", "SupportCase", "1", "SetPriority"));
    }

    [Theory]
    [InlineData("Hello, just checking in on our proposal. Could we talk this week?", true)]
    [InlineData("Too short", false)]
    [InlineData("Please visit https://evil.example to continue with your order today", false)]
    [InlineData("Please open www.evil.example to continue with your order today", false)]
    [InlineData("Hello <b>friend</b>, just checking in on our proposal this week", false)]
    public void An_ai_draft_must_be_plain_text_without_links(string text, bool ok) => Assert.Equal(ok, AgentRules.CleanDraft(text) != null);

    [Fact]
    public void Drafts_travel_in_the_task_payload_and_templates_name_the_subject()
    {
        var payload = AgentRules.TaskPayload(1, "High", "Hello there, checking in.");
        Assert.Equal("Hello there, checking in.", AgentRules.ParseDraft(payload));
        Assert.Equal((1, "High"), AgentRules.ParseTaskPayload(payload));
        Assert.Null(AgentRules.ParseDraft(AgentRules.TaskPayload(1, "High")));
        Assert.Contains("Acme", AgentRules.LeadDraft("Big deal", "Acme"));
        Assert.Contains("01 Mar 2027", AgentRules.RenewalDraft("C-1", "Support", new DateTime(2027, 3, 1)));
        Assert.True(AgentRules.LeadDraft(null, null).Length <= AgentRules.DraftMaxChars);
    }

    [Fact]
    public void Automatic_run_is_due_about_once_a_day()
    {
        Assert.True(AgentRules.IsAutoDue(null, Now));
        Assert.False(AgentRules.IsAutoDue(Now.AddHours(-1), Now));
        Assert.False(AgentRules.IsAutoDue(Now.AddHours(-22), Now));
        Assert.True(AgentRules.IsAutoDue(Now.AddHours(-23), Now));
        Assert.True(AgentRules.IsAutoDue(Now.AddDays(-3), Now));
    }
}
