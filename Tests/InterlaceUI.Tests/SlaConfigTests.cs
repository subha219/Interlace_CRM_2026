using Application.Features.SupportCaseManager;
using Domain.Enums;

namespace InterlaceUI.Tests;

public class SlaConfigTests
{
    [Fact]
    public void First_response_defaults_get_longer_with_lower_priority()
    {
        var c = new SlaConfig();
        Assert.Equal(1, c.FirstResponseHours(CasePriority.Critical));
        Assert.True(c.FirstResponseHours(CasePriority.Low) > c.FirstResponseHours(CasePriority.High));
    }

    [Fact]
    public void Without_policy_there_is_no_escalation_contact()
    {
        Assert.Null(new SlaConfig().EscalationUser(CasePriority.Critical));
    }

    [Fact]
    public void Due_dates_add_the_target_hours()
    {
        var c = new SlaConfig();
        var from = new DateTime(2026, 1, 1, 9, 0, 0);
        Assert.Equal(from.AddHours(c.ResolutionHours(CasePriority.High)), c.ResolutionDue(CasePriority.High, from));
    }
}
