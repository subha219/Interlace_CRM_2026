using Domain.Entities;
using Domain.Enums;

namespace Application.Features.LeadManager;

/// <summary>
/// Lead score from 0 to 100, computed on the fly (no schema change).
/// 60% BANT qualification, 25% pipeline progress, 15% freshness of the closing estimate.
/// BANT scores are expected on a 0-10 scale; values above 10 are treated as 0-100.
/// </summary>
public static class LeadScoring
{
    public static int Calculate(Lead lead, DateTime now)
    {
        static double Norm(double? v)
        {
            if (!v.HasValue || v.Value <= 0) return 0;
            return v.Value > 10 ? Math.Min(v.Value, 100) / 100 : v.Value / 10;
        }

        var bant = (Norm(lead.BudgetScore) + Norm(lead.AuthorityScore) + Norm(lead.NeedScore) + Norm(lead.TimelineScore)) / 4;

        if (lead.ClosingStatus == ClosingStatus.ClosedWon) return 100;
        if (lead.ClosingStatus == ClosingStatus.ClosedLost) return 0;

        var stageIndex = lead.PipelineStage.HasValue ? (int)lead.PipelineStage.Value : 0;
        var progress = Math.Min(stageIndex, 5) / 5.0;

        double freshness;
        if (!lead.DateClosingEstimation.HasValue)
        {
            freshness = 0.3;
        }
        else
        {
            var days = (lead.DateClosingEstimation.Value.Date - now.Date).TotalDays;
            freshness = days < 0 ? 0 : days <= 30 ? 1 : days <= 90 ? 0.6 : 0.3;
        }

        return (int)Math.Round(bant * 60 + progress * 25 + freshness * 15);
    }
}
