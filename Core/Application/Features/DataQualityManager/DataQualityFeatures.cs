using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.DuplicateManager.Queries;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DataQualityManager;

public record DataQualitySample(string? Id, string? Number, string? Name);

public class DataQualityCheckDto
{
    public string Key { get; init; } = "";
    public string Entity { get; init; } = "";
    public string Label { get; init; } = "";

    /// <summary>high, medium or low.</summary>
    public string Severity { get; init; } = "medium";
    public int Count { get; init; }
    public int Total { get; init; }
    public int Percent { get; init; }
    public string? FixUrl { get; init; }
    public bool CanAssignOwner { get; init; }
    public List<DataQualitySample> Samples { get; init; } = new();
}

public class GetDataQualityResult
{
    public int Score { get; init; }
    public List<DataQualityCheckDto> Checks { get; init; } = new();
}

public class GetDataQualityRequest : IRequest<GetDataQualityResult> { }

public class GetDataQualityHandler : IRequestHandler<GetDataQualityRequest, GetDataQualityResult>
{
    private const int SampleSize = 8;
    private readonly IQueryContext _context;

    public GetDataQualityHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetDataQualityResult> Handle(GetDataQualityRequest request, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var now = DateTime.Now;
        var today = now.Date;
        var stale = now.AddDays(-60);
        var checks = new List<DataQualityCheckDto>();

        async Task Add(string key, string entity, string label, string severity, string? fixUrl, int total, IQueryable<DataQualitySample> rows, bool assign = false)
        {
            var count = await rows.CountAsync(ct);
            var samples = count == 0 ? new List<DataQualitySample>() : await rows.Take(SampleSize).ToListAsync(ct);
            checks.Add(new DataQualityCheckDto
            {
                Key = key, Entity = entity, Label = label, Severity = severity, Count = count, Total = total,
                Percent = total == 0 ? 100 : (int)Math.Round(100.0 * (total - count) / total),
                FixUrl = fixUrl, CanAssignOwner = assign, Samples = samples
            });
        }

        // ---- customers
        var customers = _context.Customer.AsNoTracking().Where(x => !x.IsDeleted);
        var customerTotal = await customers.CountAsync(ct);
        IQueryable<DataQualitySample> CustomerRows(IQueryable<Customer> q) => q.OrderBy(x => x.Name).Select(x => new DataQualitySample(x.Id, x.Number, x.Name));

        await Add("customer-email", "Customers", "Customers without an e-mail address", "high", "/Customers/CustomerList", customerTotal,
            CustomerRows(customers.Where(x => x.EmailAddress == null || x.EmailAddress == "")));
        await Add("customer-phone", "Customers", "Customers without a phone number", "medium", "/Customers/CustomerList", customerTotal,
            CustomerRows(customers.Where(x => x.PhoneNumber == null || x.PhoneNumber == "")));
        await Add("customer-owner", "Customers", "Customers without an owner", "medium", "/Customers/CustomerList", customerTotal,
            CustomerRows(customers.Where(x => x.OwnerUserId == null)), true);
        await Add("customer-contacts", "Customers", "Customers with no contact person", "medium", "/CustomerContacts/CustomerContactList", customerTotal,
            CustomerRows(customers.Where(x => !_context.CustomerContact.Any(c => c.CustomerId == x.Id && !c.IsDeleted))));
        await Add("customer-group", "Customers", "Customers without a group or category", "low", "/Customers/CustomerList", customerTotal,
            CustomerRows(customers.Where(x => x.CustomerGroupId == null || x.CustomerCategoryId == null)));

        // ---- leads (open ones: converted and closed leads are history)
        var leads = _context.Lead.AsNoTracking().Where(x => !x.IsDeleted && x.ConvertedCustomerId == null
            && x.ClosingStatus != ClosingStatus.ClosedWon && x.ClosingStatus != ClosingStatus.ClosedLost);
        var leadTotal = await leads.CountAsync(ct);
        IQueryable<DataQualitySample> LeadRows(IQueryable<Lead> q) => q.OrderBy(x => x.Number).Select(x => new DataQualitySample(x.Id, x.Number, x.CompanyName ?? x.Title));

        await Add("lead-email", "Leads", "Open leads without an e-mail address", "high", "/Leads/LeadList", leadTotal,
            LeadRows(leads.Where(x => x.CompanyEmail == null || x.CompanyEmail == "")));
        await Add("lead-phone", "Leads", "Open leads without a phone number", "medium", "/Leads/LeadList", leadTotal,
            LeadRows(leads.Where(x => x.CompanyPhoneNumber == null || x.CompanyPhoneNumber == "")));
        await Add("lead-source", "Leads", "Open leads without a source", "medium", "/Leads/LeadList", leadTotal,
            LeadRows(leads.Where(x => x.LeadSource == null || x.LeadSource == "")));
        await Add("lead-owner", "Leads", "Open leads without an owner", "high", "/Leads/LeadList", leadTotal,
            LeadRows(leads.Where(x => x.OwnerUserId == null)), true);
        await Add("lead-consent", "Leads", "Open leads with no marketing consent recorded", "low", "/Leads/LeadList", leadTotal,
            LeadRows(leads.Where(x => x.ConsentMarketing == null)));
        await Add("lead-stale", "Leads", "Open leads not touched for 60 days", "high", "/Pipelines/PipelineBoard", leadTotal,
            LeadRows(leads.Where(x => (x.UpdatedAtUtc ?? x.CreatedAtUtc) < stale.ToUniversalTime())));
        await Add("lead-overdue", "Leads", "Open leads past their expected closing date", "high", "/Pipelines/PipelineBoard", leadTotal,
            LeadRows(leads.Where(x => x.DateClosingEstimation != null && x.DateClosingEstimation < today)));

        // ---- support cases
        var openCases = _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed);
        var caseTotal = await openCases.CountAsync(ct);
        IQueryable<DataQualitySample> CaseRows(IQueryable<SupportCase> q) => q.OrderBy(x => x.Number).Select(x => new DataQualitySample(x.Id, x.Number, x.Title));

        await Add("case-customer", "Support", "Open cases without a customer", "medium", "/SupportCases/SupportCaseList", caseTotal,
            CaseRows(openCases.Where(x => x.CustomerId == null)));
        await Add("case-sla", "Support", "Open cases past their due date", "high", "/SupportCases/SupportCaseList", caseTotal,
            CaseRows(openCases.Where(x => x.DueDate != null && x.DueDate < now)));

        // ---- activities
        var openActivities = _context.Activity.AsNoTracking().Where(x => !x.IsDeleted && x.Status == ActivityStatus.Open);
        var activityTotal = await openActivities.CountAsync(ct);
        await Add("activity-overdue", "Activities", "Open activities that are overdue", "medium", "/Activities/ActivityList", activityTotal,
            openActivities.Where(x => (x.EndAt ?? x.StartAt) != null && (x.EndAt ?? x.StartAt) < now).OrderBy(x => x.StartAt).Select(x => new DataQualitySample(x.Id, x.Number, x.Subject)));

        // ---- duplicates
        var customerItems = await customers.Select(x => new DuplicateItemDto { Id = x.Id, Number = x.Number, Name = x.Name, Email = x.EmailAddress, Phone = x.PhoneNumber, CreatedAtUtc = x.CreatedAtUtc }).ToListAsync(ct);
        var customerDuplicates = DuplicateFinder.Group(customerItems);
        checks.Add(new DataQualityCheckDto
        {
            Key = "customer-duplicates", Entity = "Customers", Label = "Customers that look like duplicates", Severity = "high",
            Count = customerDuplicates.Sum(g => g.Items.Count - 1), Total = customerTotal,
            Percent = customerTotal == 0 ? 100 : (int)Math.Round(100.0 * (customerTotal - customerDuplicates.Sum(g => g.Items.Count - 1)) / customerTotal),
            FixUrl = "/Customers/Duplicates",
            Samples = customerDuplicates.Take(SampleSize).Select(g => new DataQualitySample(g.Items[0].Id, g.Items[0].Number, string.Join(" / ", g.Items.Select(i => i.Name).Distinct().Take(3)))).ToList()
        });

        var leadItems = await leads.Select(x => new DuplicateItemDto { Id = x.Id, Number = x.Number, Name = x.CompanyName ?? x.Title, Email = x.CompanyEmail, Phone = x.CompanyPhoneNumber, CreatedAtUtc = x.CreatedAtUtc }).ToListAsync(ct);
        var leadDuplicates = DuplicateFinder.Group(leadItems);
        checks.Add(new DataQualityCheckDto
        {
            Key = "lead-duplicates", Entity = "Leads", Label = "Open leads that look like duplicates", Severity = "high",
            Count = leadDuplicates.Sum(g => g.Items.Count - 1), Total = leadTotal,
            Percent = leadTotal == 0 ? 100 : (int)Math.Round(100.0 * (leadTotal - leadDuplicates.Sum(g => g.Items.Count - 1)) / leadTotal),
            FixUrl = "/Leads/Duplicates",
            Samples = leadDuplicates.Take(SampleSize).Select(g => new DataQualitySample(g.Items[0].Id, g.Items[0].Number, string.Join(" / ", g.Items.Select(i => i.Name).Distinct().Take(3)))).ToList()
        });

        var scored = checks.Where(c => c.Total > 0).ToList();
        var score = scored.Count == 0 ? 100 : (int)Math.Round(scored.Average(c => (double)c.Percent));

        return new GetDataQualityResult { Score = score, Checks = checks };
    }
}

public class AssignOwnerResult { public int Assigned { get; init; } }

public class AssignOwnerRequest : IRequest<AssignOwnerResult>
{
    /// <summary>Customer or Lead.</summary>
    public string? Entity { get; init; }

    /// <summary>The user who becomes owner; empty means the signed-in user.</summary>
    public string? OwnerUserId { get; init; }
    public string? UserId { get; set; }
}

public class AssignOwnerValidator : AbstractValidator<AssignOwnerRequest>
{
    public AssignOwnerValidator()
    {
        RuleFor(x => x.Entity).Must(x => x == "Customer" || x == "Lead").WithMessage("Entity must be Customer or Lead.");
    }
}

/// <summary>Gives every record that has no owner to one user.</summary>
public class AssignOwnerHandler : IRequestHandler<AssignOwnerRequest, AssignOwnerResult>
{
    private readonly ICommandRepository<Customer> _customers;
    private readonly ICommandRepository<Lead> _leads;
    private readonly IUnitOfWork _unitOfWork;

    public AssignOwnerHandler(ICommandRepository<Customer> customers, ICommandRepository<Lead> leads, IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _leads = leads;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssignOwnerResult> Handle(AssignOwnerRequest request, CancellationToken cancellationToken)
    {
        var owner = string.IsNullOrEmpty(request.OwnerUserId) ? request.UserId : request.OwnerUserId;
        var count = 0;

        if (request.Entity == "Customer")
        {
            foreach (var x in await _customers.GetQuery().Where(x => !x.IsDeleted && x.OwnerUserId == null).ToListAsync(cancellationToken))
            {
                x.OwnerUserId = owner;
                x.UpdatedById = request.UserId;
                _customers.Update(x);
                count++;
            }
        }
        else
        {
            foreach (var x in await _leads.GetQuery().Where(x => !x.IsDeleted && x.OwnerUserId == null).ToListAsync(cancellationToken))
            {
                x.OwnerUserId = owner;
                x.UpdatedById = request.UserId;
                _leads.Update(x);
                count++;
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new AssignOwnerResult { Assigned = count };
    }
}
