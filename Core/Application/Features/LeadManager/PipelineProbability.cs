using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LeadManager;

/// <summary>
/// Win probability per pipeline stage, used for the weighted forecast and the board totals.
/// </summary>
public static class PipelineProbability
{
    public static double For(PipelineStage? stage, ClosingStatus? closingStatus)
    {
        if (closingStatus == ClosingStatus.ClosedWon) return 100;
        if (closingStatus == ClosingStatus.ClosedLost) return 0;

        return stage switch
        {
            PipelineStage.Prospecting => 10,
            PipelineStage.Qualification => 20,
            PipelineStage.NeedAnalysis => 40,
            PipelineStage.Proposal => 60,
            PipelineStage.Negotiation => 75,
            PipelineStage.DecisionMaking => 90,
            PipelineStage.Closed => 100,
            _ => 0
        };
    }
}

/// <summary>Stage names and probabilities as configured by the company, falling back to the built-in defaults.</summary>
public class PipelineConfig
{
    public static readonly PipelineStage[] OpenStages =
    {
        PipelineStage.Prospecting, PipelineStage.Qualification, PipelineStage.NeedAnalysis,
        PipelineStage.Proposal, PipelineStage.Negotiation, PipelineStage.DecisionMaking
    };

    private readonly Dictionary<PipelineStage, (string? Label, double Probability)> _rows = new();

    public static async Task<PipelineConfig> LoadAsync(IQueryContext context, CancellationToken cancellationToken)
    {
        var config = new PipelineConfig();
        var rows = await context.PipelineStageSetting.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var r in rows) config._rows[r.Stage] = (r.Label, r.Probability);
        return config;
    }

    public double For(PipelineStage? stage, ClosingStatus? closingStatus)
    {
        if (closingStatus == ClosingStatus.ClosedWon) return 100;
        if (closingStatus == ClosingStatus.ClosedLost) return 0;
        if (stage != null && stage != PipelineStage.Closed && _rows.TryGetValue(stage.Value, out var row)) return row.Probability;
        return PipelineProbability.For(stage, closingStatus);
    }

    public string Label(PipelineStage stage) =>
        _rows.TryGetValue(stage, out var row) && !string.IsNullOrWhiteSpace(row.Label) ? row.Label! : stage.ToFriendlyName();
}
