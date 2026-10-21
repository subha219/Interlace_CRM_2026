using Application.Common.CQS.Queries;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MyWorkManager;

public record MyWorkItem(string? Id, string? Number, string? Title, string? Sub, DateTime? When, bool Overdue, string? Url);

public class MyWorkSection
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public double? Amount { get; init; }
    public string? Url { get; init; }
    public List<MyWorkItem> Items { get; init; } = new();
}

public class GetMyWorkResult { public List<MyWorkSection> Sections { get; init; } = new(); }

/// <summary>Role flags decide which sections the caller may see.</summary>
public class GetMyWorkRequest : IRequest<GetMyWorkResult>
{
    public string? UserId { get; set; }
    public bool Leads { get; set; }
    public bool Customers { get; set; }
    public bool Support { get; set; }
    public bool Sales { get; set; }
    public bool Activities { get; set; }
}

public class GetMyWorkHandler : IRequestHandler<GetMyWorkRequest, GetMyWorkResult>
{
    private const int Top = 8;
    private readonly IQueryContext _context;
    public GetMyWorkHandler(IQueryContext context) { _context = context; }

    public async Task<GetMyWorkResult> Handle(GetMyWorkRequest r, CancellationToken ct)
    {
        var me = r.UserId;
        var now = DateTime.UtcNow;
        var sections = new List<MyWorkSection>();

        if (r.Activities)
        {
            var q = _context.Activity.AsNoTracking().Where(x => !x.IsDeleted && x.Status == ActivityStatus.Open && (x.AssignedToUserId == me || (x.AssignedToUserId == null && x.OwnerUserId == me)));
            var items = await q.OrderBy(x => x.StartAt).Take(Top).ToListAsync(ct);
            sections.Add(new MyWorkSection
            {
                Key = "activities", Label = "My open activities", Count = await q.CountAsync(ct), Url = "/Activities/ActivityList",
                Items = items.Select(x => new MyWorkItem(x.Id, x.Number, x.Subject, x.RelatedName, x.StartAt, x.StartAt != null && x.StartAt < now, "/Activities/ActivityList")).ToList()
            });
        }

        if (r.Leads)
        {
            var q = _context.Lead.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerUserId == me && x.ClosingStatus == ClosingStatus.OnProgress && x.ConvertedCustomerId == null);
            var items = await q.OrderBy(x => x.DateClosingEstimation).Take(Top).ToListAsync(ct);
            sections.Add(new MyWorkSection
            {
                Key = "leads", Label = "My open leads", Count = await q.CountAsync(ct), Amount = await q.SumAsync(x => x.AmountTargeted ?? 0, ct), Url = "/Leads/LeadList",
                Items = items.Select(x => new MyWorkItem(x.Id, x.Number, x.Title, x.PipelineStage.ToString(), x.DateClosingEstimation, x.DateClosingEstimation != null && x.DateClosingEstimation < now, "/Leads/LeadList")).ToList()
            });
        }

        if (r.Support)
        {
            var q = _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerUserId == me && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed);
            var items = await q.OrderBy(x => x.DueDate).Take(Top).ToListAsync(ct);
            sections.Add(new MyWorkSection
            {
                Key = "cases", Label = "My open support cases", Count = await q.CountAsync(ct), Url = "/SupportCases/SupportCaseList",
                Items = items.Select(x => new MyWorkItem(x.Id, x.Number, x.Title, x.Priority.ToString(), x.DueDate, x.DueDate != null && x.DueDate < now, "/SupportCases/SupportCaseList")).ToList()
            });
        }

        if (r.Sales)
        {
            var q = _context.Quotation.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerUserId == me && (x.QuotationStatus == QuotationStatus.Draft || x.QuotationStatus == QuotationStatus.Sent));
            var items = await q.OrderBy(x => x.ValidUntil).Take(Top).ToListAsync(ct);
            sections.Add(new MyWorkSection
            {
                Key = "quotes", Label = "My quotations awaiting a decision", Count = await q.CountAsync(ct), Amount = await q.SumAsync(x => x.AfterTaxAmount ?? 0, ct), Url = "/Quotations/QuotationList",
                Items = items.Select(x => new MyWorkItem(x.Id, x.Number, x.Description, x.QuotationStatus.ToString(), x.ValidUntil, x.ValidUntil != null && x.ValidUntil < now, "/Quotations/QuotationList")).ToList()
            });
        }

        if (r.Customers)
        {
            var q = _context.Customer.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerUserId == me);
            sections.Add(new MyWorkSection { Key = "customers", Label = "Customers I own", Count = await q.CountAsync(ct), Url = "/Customers/CustomerList" });
        }

        return new GetMyWorkResult { Sections = sections };
    }
}
