using Application.Features.LeadManager;
using Domain.Enums;

namespace InterlaceUI.Tests;

public class PipelineConfigTests
{
    [Fact]
    public void Defaults_apply_without_company_settings()
    {
        var config = new PipelineConfig();
        Assert.Equal(10, config.For(PipelineStage.Prospecting, ClosingStatus.OnProgress));
        Assert.Equal(75, config.For(PipelineStage.Negotiation, ClosingStatus.OnProgress));
    }

    [Fact]
    public void Won_and_lost_ignore_the_stage()
    {
        var config = new PipelineConfig();
        Assert.Equal(100, config.For(PipelineStage.Prospecting, ClosingStatus.ClosedWon));
        Assert.Equal(0, config.For(PipelineStage.DecisionMaking, ClosingStatus.ClosedLost));
    }

    [Fact]
    public void Label_falls_back_to_the_standard_name()
    {
        Assert.False(string.IsNullOrWhiteSpace(new PipelineConfig().Label(PipelineStage.Proposal)));
    }
}
