using Application.Common.CQS.Queries;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InsightsManager;

public record HealthFactorDto(string Name, int Points, int Max, string Note);

public record DealHealthResult(int Score, string Label, string NextAction, List<HealthFactorDto> Factors);

/// <summary>
/// Explainable deal health (0-100). Five factors, each with its own points and a plain-language note:
/// qualification (BANT) 30, stage progress 20, momentum (recent activity) 20, timeline 15, stakeholders 15.
/// </summary>
public class DealHealthService
{
    public const string Healthy = "Healthy";
    public const string NeedsAttention = "Needs attention";
    public const string AtRisk = "At risk";

    private readonly IQueryContext _context;

    public DealHealthService(IQueryContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<string, DealHealthResult>> EvaluateAsync(IEnumerable<Lead> leads, CancellationToken cancellationToken = default)
    {
        var list = leads.ToList();
        var result = new Dictionary<string, DealHealthResult>();
        if (list.Count == 0)
        {
            return result;
        }

        var contactCounts = await _context.LeadContact.AsNoTracking()
            .Where(x => !x.IsDeleted && x.LeadId != null)
            .GroupBy(x => x.LeadId!)
            .Select(g => new { LeadId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LeadId, x => x.Count, cancellationToken);

        var lastActivities = await _context.LeadActivity.AsNoTracking()
            .Where(x => !x.IsDeleted && x.LeadId != null)
            .GroupBy(x => x.LeadId!)
            .Select(g => new { LeadId = g.Key, Last = g.Max(a => a.FromDate ?? a.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.LeadId, x => x.Last, cancellationToken);

        var now = DateTime.Now;
        foreach (var lead in list)
        {
            contactCounts.TryGetValue(lead.Id, out var contacts);
            lastActivities.TryGetValue(lead.Id, out var last);
            result[lead.Id] = Evaluate(lead, contacts, last, now);
        }

        return result;
    }

    public static DealHealthResult Evaluate(Lead lead, int contactCount, DateTime? lastActivity, DateTime now)
    {
        if (lead.ClosingStatus == ClosingStatus.ClosedWon)
        {
            return new DealHealthResult(100, "Won", "Deal closed. Plan onboarding and ask for a referral.", new List<HealthFactorDto>());
        }

        if (lead.ClosingStatus == ClosingStatus.ClosedLost)
        {
            return new DealHealthResult(0, "Lost", "Record the loss reason and schedule a re-engagement in 90 days.", new List<HealthFactorDto>());
        }

        static double Norm(double? v)
        {
            if (!v.HasValue || v.Value <= 0) return 0;
            return v.Value > 10 ? Math.Min(v.Value, 100) / 100 : v.Value / 10;
        }

        var factors = new List<HealthFactorDto>();

        //>>> 1. qualification (BANT)
        var bantAvg = (Norm(lead.BudgetScore) + Norm(lead.AuthorityScore) + Norm(lead.NeedScore) + Norm(lead.TimelineScore)) / 4;
        var bantPoints = (int)Math.Round(bantAvg * 30);
        factors.Add(new HealthFactorDto("Qualification (BANT)", bantPoints, 30,
            bantAvg >= 0.7 ? "Budget, authority, need and timeline are well qualified." :
            bantAvg >= 0.4 ? "Partly qualified. Confirm the weakest BANT area." :
            "Barely qualified. Budget, authority, need or timeline are unknown."));

        //>>> 2. stage progress
        var stageIndex = lead.PipelineStage.HasValue ? Math.Min((int)lead.PipelineStage.Value, 5) : 0;
        var stagePoints = (int)Math.Round(stageIndex / 5.0 * 20);
        factors.Add(new HealthFactorDto("Stage progress", stagePoints, 20,
            $"Currently in {(lead.PipelineStage ?? PipelineStage.Prospecting)}."));

        //>>> 3. momentum
        int momentum;
        string momentumNote;
        int? daysSince = lastActivity.HasValue ? (int)Math.Max(0, (now.Date - lastActivity.Value.Date).TotalDays) : null;
        if (daysSince == null)
        {
            momentum = 0;
            momentumNote = "No activity has been logged yet.";
        }
        else if (daysSince <= 7)
        {
            momentum = 20;
            momentumNote = $"Last activity {daysSince} day(s) ago.";
        }
        else if (daysSince <= 14)
        {
            momentum = 14;
            momentumNote = $"Last activity {daysSince} days ago.";
        }
        else if (daysSince <= 30)
        {
            momentum = 7;
            momentumNote = $"Last activity {daysSince} days ago. Momentum is fading.";
        }
        else
        {
            momentum = 0;
            momentumNote = $"No activity for {daysSince} days. The deal is going cold.";
        }
        factors.Add(new HealthFactorDto("Momentum", momentum, 20, momentumNote));

        //>>> 4. timeline
        int timeline;
        string timelineNote;
        bool overdue = false;
        if (!lead.DateClosingEstimation.HasValue)
        {
            timeline = 5;
            timelineNote = "No expected closing date is set.";
        }
        else
        {
            var days = (int)(lead.DateClosingEstimation.Value.Date - now.Date).TotalDays;
            if (days < 0)
            {
                overdue = true;
                timeline = 0;
                timelineNote = $"The expected closing date passed {-days} day(s) ago.";
            }
            else if (days <= 30)
            {
                timeline = 15;
                timelineNote = $"Expected to close in {days} day(s).";
            }
            else if (days <= 90)
            {
                timeline = 10;
                timelineNote = $"Expected to close in {days} days.";
            }
            else
            {
                timeline = 5;
                timelineNote = $"Expected to close in {days} days. A long way out.";
            }
        }
        factors.Add(new HealthFactorDto("Timeline", timeline, 15, timelineNote));

        //>>> 5. stakeholders
        var stakeholderPoints = contactCount >= 3 ? 15 : contactCount == 2 ? 10 : contactCount == 1 ? 5 : 0;
        factors.Add(new HealthFactorDto("Stakeholders", stakeholderPoints, 15,
            contactCount == 0 ? "No contacts are linked to this deal." :
            contactCount == 1 ? "Single contact. A second stakeholder lowers the risk." :
            $"{contactCount} contacts involved."));

        var score = Math.Clamp(bantPoints + stagePoints + momentum + timeline + stakeholderPoints, 0, 100);
        var label = score >= 70 ? Healthy : score >= 40 ? NeedsAttention : AtRisk;

        string next;
        if (overdue)
        {
            next = "The closing date has passed. Agree a new date with the buyer or close the deal as lost.";
        }
        else if (daysSince == null || daysSince > 14)
        {
            next = daysSince == null
                ? "Log a first call or meeting to start the activity trail."
                : $"No activity for {daysSince} days. Call the buyer or send a follow-up today.";
        }
        else if (contactCount < 2)
        {
            next = "Single-threaded deal. Add a second stakeholder, ideally the decision maker.";
        }
        else if (bantAvg < 0.4)
        {
            next = "Qualify budget and authority before investing more time.";
        }
        else
        {
            next = lead.PipelineStage switch
            {
                PipelineStage.Prospecting => "Book a discovery call to qualify the need.",
                PipelineStage.Qualification => "Run a needs analysis and confirm the budget.",
                PipelineStage.NeedAnalysis => "Prepare and send a proposal or quotation.",
                PipelineStage.Proposal => "Follow up on the proposal and handle objections.",
                PipelineStage.Negotiation => "Confirm terms and send the final quotation.",
                PipelineStage.DecisionMaking => "Get the decision maker on a call and ask for a date.",
                _ => "Keep momentum: schedule the next step."
            };
        }

        return new DealHealthResult(score, label, next, factors);
    }
}
