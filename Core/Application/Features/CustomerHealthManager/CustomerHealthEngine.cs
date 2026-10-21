using Application.Common.Repositories;
using Application.Features.ContractManager;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.CustomerHealthManager;

public class HealthInput
{
    public int? DaysSinceContact { get; init; }
    public int OpenCases { get; init; }
    public int BreachedCases { get; init; }
    public double? CsatAverage { get; init; }
    public int OverdueInvoices { get; init; }
    public string? ContractState { get; init; }   // None, Active, Expiring, Expired
}

public record HealthScore(int Total, int Engagement, int Support, int Payments, int Contract, string Band);

/// <summary>Customer health: four parts worth 25 points each. Pure, so it can be tested.</summary>
public static class HealthRules
{
    public static HealthScore Score(HealthInput i)
    {
        var engagement = i.DaysSinceContact switch { null => 0, <= 30 => 25, <= 90 => 15, <= 180 => 5, _ => 0 };
        var support = 25 - 8 * i.BreachedCases - (i.OpenCases > 3 ? 5 : 0) - (i.CsatAverage != null && i.CsatAverage < 3 ? 10 : 0);
        support = Math.Clamp(support, 0, 25);
        var payments = Math.Clamp(25 - 10 * i.OverdueInvoices, 0, 25);
        var contract = i.ContractState switch { "Active" => 25, "Expiring" => 12, "Expired" => 0, _ => 10 };
        var total = engagement + support + payments + contract;
        return new HealthScore(total, engagement, support, payments, contract, Band(total));
    }

    public static string Band(int total) => total >= 70 ? "Healthy" : total >= 40 ? "At risk" : "Critical";
}

public class CustomerHealthRow
{
    public string? CustomerId { get; init; }
    public string? Name { get; init; }
    public string? OwnerUserId { get; init; }
    public int Total { get; init; }
    public string? Band { get; init; }
    public int Engagement { get; init; }
    public int Support { get; init; }
    public int Payments { get; init; }
    public int Contract { get; init; }
    public int? DaysSinceContact { get; init; }
    public int OpenCases { get; init; }
    public int OverdueInvoices { get; init; }
    public string? ContractState { get; init; }
    public double ActiveContractValue { get; init; }
}

public static class CustomerHealthEngine
{
    /// <summary>Scores every customer in the Customer lifecycle stage (or every customer when none are flagged).</summary>
    public static async Task<List<CustomerHealthRow>> ComputeAsync(IEntityDbSet db, CancellationToken ct)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var customers = await db.Customer.AsNoTracking().Where(x => !x.IsDeleted && x.LifecycleStage == LifecycleStage.Customer)
            .Select(x => new { x.Id, x.Name, x.OwnerUserId }).ToListAsync(ct);
        if (customers.Count == 0) return new();

        var since = DateTime.UtcNow.AddDays(-400);
        var acts = (await db.Activity.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedType == "Customer" && x.RelatedId != null)
            .Select(x => new { x.RelatedId, When = x.CompletedAt ?? x.StartAt ?? x.CreatedAtUtc }).ToListAsync(ct))
            .Where(x => x.When != null).GroupBy(x => x.RelatedId!).ToDictionary(g => g.Key, g => g.Max(x => x.When!.Value));
        var orders = (await db.SalesOrder.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId != null && x.OrderDate != null)
            .Select(x => new { x.CustomerId, x.OrderDate }).ToListAsync(ct))
            .GroupBy(x => x.CustomerId!).ToDictionary(g => g.Key, g => g.Max(x => x.OrderDate!.Value));
        var cases = (await db.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId != null)
            .Select(x => new { x.CustomerId, x.Status, x.DueDate, x.CsatScore }).ToListAsync(ct)).GroupBy(x => x.CustomerId!).ToDictionary(g => g.Key, g => g.ToList());
        var invoices = (await db.Invoice.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId != null && x.DueDate != null && x.DueDate < today && (x.BalanceDue ?? 0) > 0
                && x.InvoiceStatus != InvoiceStatus.Paid && x.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(x => x.CustomerId).ToListAsync(ct)).GroupBy(x => x!).ToDictionary(g => g.Key, g => g.Count());
        var contracts = (await db.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.CustomerId != null && x.Status != "Draft" && x.Status != "Cancelled")
            .ToListAsync(ct)).GroupBy(x => x.CustomerId!).ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<CustomerHealthRow>();
        foreach (var c in customers)
        {
            DateTime? last = null;
            if (acts.TryGetValue(c.Id, out var a)) last = a;
            if (orders.TryGetValue(c.Id, out var o) && (last == null || o > last)) last = o;
            int? days = last == null ? null : Math.Max(0, (int)(today - last.Value.Date).TotalDays);

            cases.TryGetValue(c.Id, out var cs);
            cs ??= new();
            var open = cs.Where(x => x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed).ToList();
            var breached = open.Count(x => x.DueDate != null && x.DueDate < now);
            var scores = cs.Where(x => x.CsatScore != null).Select(x => (double)x.CsatScore!.Value).ToList();

            var state = "None";
            double value = 0;
            if (contracts.TryGetValue(c.Id, out var ct2))
            {
                var live = ct2.Where(x => x.Status == "Active" && x.EndDate != null && x.EndDate.Value.Date >= today).ToList();
                value = live.Sum(x => x.Value ?? 0);
                if (live.Count > 0) state = live.Any(x => !x.AutoRenew && (x.EndDate!.Value.Date - today).TotalDays <= x.RenewalNoticeDays) && live.Count == live.Count(x => (x.EndDate!.Value.Date - today).TotalDays <= x.RenewalNoticeDays) ? "Expiring" : "Active";
                else if (ct2.Any(x => x.Status == "Active")) state = "Expired";
            }

            var s = HealthRules.Score(new HealthInput
            {
                DaysSinceContact = days, OpenCases = open.Count, BreachedCases = breached, CsatAverage = scores.Count == 0 ? null : scores.Average(),
                OverdueInvoices = invoices.TryGetValue(c.Id, out var inv) ? inv : 0, ContractState = state
            });
            rows.Add(new CustomerHealthRow
            {
                CustomerId = c.Id, Name = c.Name, OwnerUserId = c.OwnerUserId, Total = s.Total, Band = s.Band, Engagement = s.Engagement, Support = s.Support,
                Payments = s.Payments, Contract = s.Contract, DaysSinceContact = days, OpenCases = open.Count, OverdueInvoices = invoices.TryGetValue(c.Id, out var inv2) ? inv2 : 0,
                ContractState = state, ActiveContractValue = value
            });
        }
        return rows.OrderBy(x => x.Total).ThenBy(x => x.Name).ToList();
    }
}

public class GetCustomerHealthResult
{
    public List<CustomerHealthRow> Data { get; init; } = new();
    public int Healthy { get; init; }
    public int AtRisk { get; init; }
    public int Critical { get; init; }
    public double ValueAtRisk { get; init; }
}

public class GetCustomerHealthRequest : IRequest<GetCustomerHealthResult> { }

public class GetCustomerHealthHandler : IRequestHandler<GetCustomerHealthRequest, GetCustomerHealthResult>
{
    private readonly Application.Common.CQS.Queries.IQueryContext _context;
    public GetCustomerHealthHandler(Application.Common.CQS.Queries.IQueryContext context) { _context = context; }

    public async Task<GetCustomerHealthResult> Handle(GetCustomerHealthRequest request, CancellationToken cancellationToken)
    {
        var rows = await CustomerHealthEngine.ComputeAsync(_context, cancellationToken);
        return new GetCustomerHealthResult
        {
            Data = rows,
            Healthy = rows.Count(x => x.Band == "Healthy"),
            AtRisk = rows.Count(x => x.Band == "At risk"),
            Critical = rows.Count(x => x.Band == "Critical"),
            ValueAtRisk = rows.Where(x => x.Band != "Healthy").Sum(x => x.ActiveContractValue)
        };
    }
}
