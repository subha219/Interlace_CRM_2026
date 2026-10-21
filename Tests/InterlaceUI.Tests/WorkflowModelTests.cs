using Application.Features.WorkflowManager;

namespace InterlaceUI.Tests;

public class WorkflowModelTests
{
    private static readonly Dictionary<string, string?> Lead = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "Big deal", ["amount"] = "25000", ["source"] = "Website", ["owner"] = null
    };

    [Fact]
    public void All_conditions_must_match()
    {
        var conditions = new[] { new WorkflowCondition("amount", "gt", "10000"), new WorkflowCondition("source", "equals", "website") };
        Assert.True(WorkflowModel.Matches(conditions, Lead));
        Assert.False(WorkflowModel.Matches(conditions.Append(new WorkflowCondition("title", "contains", "xyz")), Lead));
    }

    [Fact]
    public void Numeric_operators_need_numbers()
    {
        Assert.False(WorkflowModel.Matches(new[] { new WorkflowCondition("title", "gt", "1") }, Lead));
        Assert.True(WorkflowModel.Matches(new[] { new WorkflowCondition("amount", "lt", "30000") }, Lead));
    }

    [Fact]
    public void Empty_checks_work()
    {
        Assert.True(WorkflowModel.Matches(new[] { new WorkflowCondition("owner", "is_empty", null) }, Lead));
        Assert.True(WorkflowModel.Matches(new[] { new WorkflowCondition("title", "is_not_empty", null) }, Lead));
    }

    [Fact]
    public void Render_replaces_known_placeholders_only()
    {
        Assert.Equal("Call Big deal {unknown}", WorkflowModel.Render("Call {title} {unknown}", Lead));
    }

    [Fact]
    public void Validate_accepts_a_good_rule()
    {
        var actions = "[{\"type\":\"create_activity\",\"subject\":\"Follow up {title}\",\"activityType\":\"Call\",\"dueInDays\":1}]";
        Assert.Null(WorkflowModel.Validate("lead.created", "[{\"field\":\"amount\",\"op\":\"gt\",\"value\":\"100\"}]", actions));
    }

    [Theory]
    [InlineData("nope.created", "[]", "[{\"type\":\"notify\",\"message\":\"x\"}]")]
    [InlineData("lead.created", "[]", "[]")]
    [InlineData("lead.created", "[]", "[{\"type\":\"explode\"}]")]
    [InlineData("lead.created", "[{\"field\":\"a\",\"op\":\"??\",\"value\":\"1\"}]", "[{\"type\":\"notify\",\"message\":\"x\"}]")]
    [InlineData("case.created", "[]", "[{\"type\":\"add_tag\",\"tag\":\"vip\"}]")]
    [InlineData("lead.created", "not json", "[{\"type\":\"notify\",\"message\":\"x\"}]")]
    public void Validate_rejects_bad_rules(string trigger, string conditions, string actions)
    {
        Assert.NotNull(WorkflowModel.Validate(trigger, conditions, actions));
    }

    [Theory]
    [InlineData("lead.won", "Lead")]
    [InlineData("customer.created", "Customer")]
    [InlineData("case.created", "SupportCase")]
    public void Entity_comes_from_the_event_name(string name, string expected)
    {
        Assert.Equal(expected, WorkflowModel.EntityOf(name));
    }
}
