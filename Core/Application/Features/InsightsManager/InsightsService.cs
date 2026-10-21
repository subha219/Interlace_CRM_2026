using Application.Common.CQS.Queries;
using Application.Features.LeadManager;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InsightsManager;

public record KpiDto(string Key, string Label, double Value, double? Previous, string Format, string? Url, List<double> Series);
public record PriorityDto(string Kind, string Severity, string Title, string Detail, string Url, double? Amount);
public record AtRiskDealDto(string? Id, string? Number, string? Title, string? Company, double Amount, int Score, string Label, string NextAction);
public record AgeingBucketDto(string Name, int Count, double Amount);
public record StageBucketDto(string Name, int Count, double Amount, double Weighted);
public record MonthValueDto(string Name, double Value);

public class TodayBriefingResult
{
    public DateTime GeneratedAt { get; init; }
    public List<KpiDto> Kpis { get; init; } = new();
    public List<PriorityDto> Priorities { get; init; } = new();
    public List<AtRiskDealDto> AtRiskDeals { get; init; } = new();
    public List<AgeingBucketDto> Ageing { get; init; } = new();
    public List<StageBucketDto> Pipeline { get; init; } = new();
    public List<MonthValueDto> RevenueByMonth { get; init; } = new();
    public int HealthyDeals { get; init; }
    public int AttentionDeals { get; init; }
    public int AtRiskDealCount { get; init; }
}

/// <summary>
/// One place that turns raw CRM data into the numbers and the prioritised "what should I do now" list.
/// Used by the Insights dashboard and by the AI assistant.
/// </summary>
public class InsightsService
{
    private readonly IQueryContext _context;
    private readonly DealHealthService _dealHealth;

    public InsightsService(IQueryContext context, DealHealthService dealHealth)
    {
        _context = context;
        _dealHealth = dealHealth;
    }

    public async Task<TodayBriefingResult> GetBriefingAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var seriesStart = monthStart.AddMonths(-5);

        //>>> revenue (cash received) by month
        var payments = await _context.Payment.AsNoTracking()
            .Where(x => !x.IsDeleted && x.PaymentDate != null && x.PaymentDate >= seriesStart)
            .Select(x => new { x.PaymentDate, x.Amount })
            .ToListAsync(cancellationToken);

        var revenueByMonth = new List<MonthValueDto>();
        for (int i = 0; i < 6; i++)
        {
            var m = seriesStart.AddMonths(i);
            var total = payments.Where(p => p.PaymentDate!.Value.Year == m.Year && p.PaymentDate.Value.Month == m.Month).Sum(p => p.Amount ?? 0);
            revenueByMonth.Add(new MonthValueDto(m.ToString("MMM"), total));
        }

        var revenueThis = revenueByMonth[5].Value;
        var revenuePrev = revenueByMonth[4].Value;

        //>>> receivables
        var openInvoices = await _context.Invoice.AsNoTracking()
            .Where(x => !x.IsDeleted && (x.BalanceDue ?? 0) > 0
                        && x.InvoiceStatus != InvoiceStatus.Cancelled && x.InvoiceStatus != InvoiceStatus.Draft)
            .Select(x => new { x.Id, x.Number, x.DueDate, Balance = x.BalanceDue ?? 0, Customer = x.Customer != null ? x.Customer.Name : null })
            .ToListAsync(cancellationToken);

        var receivables = openInvoices.Sum(x => x.Balance);
        var overdueInvoices = openInvoices.Where(x => x.DueDate.HasValue && x.DueDate.Value.Date < today).ToList();
        var overdueAmount = overdueInvoices.Sum(x => x.Balance);

        double Ageing(int from, int to) => openInvoices
            .Where(x => x.DueDate.HasValue && (today - x.DueDate.Value.Date).TotalDays >= from && (today - x.DueDate.Value.Date).TotalDays <= to)
            .Sum(x => x.Balance);
        int AgeingCount(int from, int to) => openInvoices
            .Count(x => x.DueDate.HasValue && (today - x.DueDate.Value.Date).TotalDays >= from && (today - x.DueDate.Value.Date).TotalDays <= to);

        var notDue = openInvoices.Where(x => !x.DueDate.HasValue || x.DueDate.Value.Date >= today).ToList();
        var ageing = new List<AgeingBucketDto>
        {
            new("Not due", notDue.Count, notDue.Sum(x => x.Balance)),
            new("1-30 days", AgeingCount(1, 30), Ageing(1, 30)),
            new("31-60 days", AgeingCount(31, 60), Ageing(31, 60)),
            new("60+ days", AgeingCount(61, 100000), Ageing(61, 100000))
        };

        //>>> support
        var openCases = await _context.SupportCase.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed)
            .Select(x => new { x.Id, x.Number, x.Title, x.DueDate, x.Priority })
            .ToListAsync(cancellationToken);
        var breached = openCases.Where(x => x.DueDate.HasValue && x.DueDate.Value < now).ToList();
        var dueSoon = openCases.Where(x => x.DueDate.HasValue && x.DueDate.Value >= now && x.DueDate.Value <= now.AddHours(24)).ToList();

        //>>> quotations
        var expiring = await _context.Quotation.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ValidUntil != null && x.ValidUntil >= today && x.ValidUntil <= today.AddDays(3)
                        && (x.QuotationStatus == QuotationStatus.Draft || x.QuotationStatus == QuotationStatus.Sent))
            .Select(x => new { x.Number, x.ValidUntil, x.AfterTaxAmount })
            .ToListAsync(cancellationToken);
        var pendingQuotes = await _context.Quotation.AsNoTracking()
            .CountAsync(x => !x.IsDeleted && (x.QuotationStatus == QuotationStatus.Draft || x.QuotationStatus == QuotationStatus.Sent), cancellationToken);

        //>>> pipeline and deal health
        var leads = await _context.Lead.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        var open = leads.Where(x => x.ClosingStatus != ClosingStatus.ClosedWon && x.ClosingStatus != ClosingStatus.ClosedLost && x.PipelineStage != PipelineStage.Closed).ToList();
        var won = leads.Count(x => x.ClosingStatus == ClosingStatus.ClosedWon);
        var lost = leads.Count(x => x.ClosingStatus == ClosingStatus.ClosedLost);
        var winRate = won + lost == 0 ? 0 : Math.Round((double)won / (won + lost) * 100, 1);

        var health = await _dealHealth.EvaluateAsync(open, cancellationToken);

        var pipelineConfig = await PipelineConfig.LoadAsync(_context, cancellationToken);
        double Amt(Lead x) => x.AmountTargeted ?? 0;
        double Wtd(Lead x) => Amt(x) * pipelineConfig.For(x.PipelineStage, x.ClosingStatus) / 100;

        var pipeline = open
            .GroupBy(x => x.PipelineStage ?? PipelineStage.Prospecting)
            .OrderBy(g => (int)g.Key)
            .Select(g => new StageBucketDto(g.Key.ToString(), g.Count(), g.Sum(Amt), g.Sum(Wtd)))
            .ToList();

        var atRisk = open
            .Where(x => health[x.Id].Label != DealHealthService.Healthy)
            .OrderBy(x => health[x.Id].Score)
            .ThenByDescending(Amt)
            .Take(8)
            .Select(x => new AtRiskDealDto(x.Id, x.Number, x.Title, x.CompanyName, Amt(x), health[x.Id].Score, health[x.Id].Label, health[x.Id].NextAction))
            .ToList();

        //>>> prioritised actions
        var priorities = new List<PriorityDto>();
        foreach (var x in overdueInvoices.OrderByDescending(i => i.Balance).Take(4))
        {
            var days = (int)(today - x.DueDate!.Value.Date).TotalDays;
            priorities.Add(new PriorityDto("Invoice", "High", $"Collect {x.Number}",
                $"{x.Customer ?? "Customer"} is {days} day(s) overdue.", "/Invoices/InvoiceList", x.Balance));
        }
        foreach (var x in breached.OrderBy(c => c.DueDate).Take(4))
        {
            priorities.Add(new PriorityDto("Support", "High", $"SLA breached: {x.Number}", x.Title ?? "", "/SupportCases/SupportCaseList", null));
        }
        foreach (var x in open.Where(l => l.DateClosingEstimation.HasValue && l.DateClosingEstimation.Value.Date < today).OrderByDescending(Amt).Take(3))
        {
            priorities.Add(new PriorityDto("Deal", "Medium", $"Reschedule {x.Number}", $"{x.Title}: the closing date has passed.", "/Pipelines/PipelineBoard", Amt(x)));
        }
        foreach (var x in atRisk.Where(d => d.Label == DealHealthService.AtRisk).Take(3))
        {
            priorities.Add(new PriorityDto("Deal", "Medium", $"Rescue {x.Number}", x.NextAction, "/Pipelines/PipelineBoard", x.Amount));
        }
        foreach (var x in expiring.Take(3))
        {
            priorities.Add(new PriorityDto("Quote", "Medium", $"Quotation {x.Number} expires {x.ValidUntil:MMM d}", "Follow up before it lapses.", "/Quotations/QuotationList", x.AfterTaxAmount));
        }
        foreach (var x in dueSoon.Take(3))
        {
            priorities.Add(new PriorityDto("Support", "Low", $"SLA due soon: {x.Number}", x.Title ?? "", "/SupportCases/SupportCaseList", null));
        }
        var rank = new Dictionary<string, int> { ["High"] = 0, ["Medium"] = 1, ["Low"] = 2 };
        priorities = priorities.OrderBy(p => rank[p.Severity]).ThenByDescending(p => p.Amount ?? 0).Take(12).ToList();

        var kpis = new List<KpiDto>
        {
            new("revenue", "Revenue this month", revenueThis, revenuePrev, "money", "/Payments/PaymentList", revenueByMonth.Select(m => m.Value).ToList()),
            new("pipeline", "Open pipeline", open.Sum(Amt), null, "money", "/Pipelines/PipelineBoard", new List<double>()),
            new("forecast", "Weighted forecast", open.Sum(Wtd), null, "money", "/Pipelines/SalesForecast", new List<double>()),
            new("winrate", "Win rate", winRate, null, "percent", "/Pipelines/SalesForecast", new List<double>()),
            new("receivables", "Receivables", receivables, null, "money", "/Invoices/InvoiceList", new List<double>()),
            new("overdue", "Overdue amount", overdueAmount, null, "money", "/Invoices/InvoiceList", new List<double>()),
            new("cases", "Open cases", openCases.Count, null, "number", "/SupportCases/SupportCaseList", new List<double>()),
            new("quotes", "Pending quotations", pendingQuotes, null, "number", "/Quotations/QuotationList", new List<double>())
        };

        return new TodayBriefingResult
        {
            GeneratedAt = now,
            Kpis = kpis,
            Priorities = priorities,
            AtRiskDeals = atRisk,
            Ageing = ageing,
            Pipeline = pipeline,
            RevenueByMonth = revenueByMonth,
            HealthyDeals = health.Values.Count(h => h.Label == DealHealthService.Healthy),
            AttentionDeals = health.Values.Count(h => h.Label == DealHealthService.NeedsAttention),
            AtRiskDealCount = health.Values.Count(h => h.Label == DealHealthService.AtRisk)
        };
    }
}
