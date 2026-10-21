using Application.Features.ContractManager;
using Application.Features.CustomerHealthManager;

namespace InterlaceUI.Tests;

public class ContractAndHealthTests
{
    [Fact]
    public void Active_contract_past_its_end_is_expired()
    {
        var today = new DateTime(2026, 6, 1);
        Assert.Equal("Expired", ContractRules.Effective("Active", new DateTime(2026, 5, 31), today));
        Assert.Equal("Active", ContractRules.Effective("Active", new DateTime(2026, 6, 1), today));
        Assert.Equal("Renewed", ContractRules.Effective("Renewed", new DateTime(2020, 1, 1), today));
    }

    [Fact]
    public void Renewal_starts_the_next_day_for_the_same_length()
    {
        var (s, e) = ContractRules.NextTerm(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        Assert.Equal(new DateTime(2027, 1, 1), s);
        Assert.Equal(s.AddDays(364), e);
    }

    [Fact]
    public void Perfect_customer_scores_100()
    {
        var s = HealthRules.Score(new HealthInput { DaysSinceContact = 5, ContractState = "Active" });
        Assert.Equal(100, s.Total);
        Assert.Equal("Healthy", s.Band);
    }

    [Fact]
    public void Silent_customer_with_problems_is_critical()
    {
        var s = HealthRules.Score(new HealthInput { DaysSinceContact = null, BreachedCases = 3, OverdueInvoices = 2, ContractState = "Expired" });
        Assert.Equal("Critical", s.Band);
        Assert.True(s.Total < 40);
    }

    [Fact]
    public void Parts_never_go_below_zero()
    {
        var s = HealthRules.Score(new HealthInput { DaysSinceContact = 999, OpenCases = 9, BreachedCases = 9, CsatAverage = 1, OverdueInvoices = 9, ContractState = "Expired" });
        Assert.Equal(0, s.Total);
    }
}
