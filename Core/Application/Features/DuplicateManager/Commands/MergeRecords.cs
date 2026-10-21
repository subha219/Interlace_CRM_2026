using System.Reflection;
using Application.Common.Repositories;
using Domain.Common;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DuplicateManager.Commands;

public class MergeRecordsResult
{
    public string? SurvivorId { get; init; }
    public int Merged { get; init; }
    public int Moved { get; init; }
}

public class MergeRecordsRequest : IRequest<MergeRecordsResult>
{
    /// <summary>Customer or Lead.</summary>
    public string? Entity { get; init; }
    public string? SurvivorId { get; init; }
    public List<string>? DuplicateIds { get; init; }
    public string? UpdatedById { get; init; }
}

public class MergeRecordsValidator : AbstractValidator<MergeRecordsRequest>
{
    public MergeRecordsValidator()
    {
        RuleFor(x => x.Entity).Must(x => x == "Customer" || x == "Lead").WithMessage("Entity must be Customer or Lead.");
        RuleFor(x => x.SurvivorId).NotEmpty();
        RuleFor(x => x.DuplicateIds).NotEmpty();
    }
}

public class MergeRecordsHandler : IRequestHandler<MergeRecordsRequest, MergeRecordsResult>
{
    private static readonly HashSet<string> Skipped = new() { nameof(BaseEntity.Id), nameof(BaseEntity.CreatedById), nameof(BaseEntity.UpdatedById), "Number", "OwnerUserId" };

    private readonly ICommandRepository<Customer> _customers;
    private readonly ICommandRepository<CustomerContact> _customerContacts;
    private readonly ICommandRepository<Invoice> _invoices;
    private readonly ICommandRepository<Quotation> _quotations;
    private readonly ICommandRepository<SalesOrder> _salesOrders;
    private readonly ICommandRepository<SupportCase> _supportCases;
    private readonly ICommandRepository<Lead> _leads;
    private readonly ICommandRepository<LeadContact> _leadContacts;
    private readonly ICommandRepository<LeadActivity> _leadActivities;
    private readonly IUnitOfWork _unitOfWork;

    public MergeRecordsHandler(
        ICommandRepository<Customer> customers,
        ICommandRepository<CustomerContact> customerContacts,
        ICommandRepository<Invoice> invoices,
        ICommandRepository<Quotation> quotations,
        ICommandRepository<SalesOrder> salesOrders,
        ICommandRepository<SupportCase> supportCases,
        ICommandRepository<Lead> leads,
        ICommandRepository<LeadContact> leadContacts,
        ICommandRepository<LeadActivity> leadActivities,
        IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _customerContacts = customerContacts;
        _invoices = invoices;
        _quotations = quotations;
        _salesOrders = salesOrders;
        _supportCases = supportCases;
        _leads = leads;
        _leadContacts = leadContacts;
        _leadActivities = leadActivities;
        _unitOfWork = unitOfWork;
    }

    public async Task<MergeRecordsResult> Handle(MergeRecordsRequest request, CancellationToken cancellationToken)
    {
        var survivorId = request.SurvivorId!;
        var duplicateIds = request.DuplicateIds!.Where(x => !string.IsNullOrEmpty(x) && x != survivorId).Distinct().ToList();
        if (duplicateIds.Count == 0)
        {
            throw new Exception("Choose at least one other record to merge into the one you keep.");
        }

        var moved = request.Entity == "Customer"
            ? await MergeCustomers(survivorId, duplicateIds, request.UpdatedById, cancellationToken)
            : await MergeLeads(survivorId, duplicateIds, request.UpdatedById, cancellationToken);

        await _unitOfWork.SaveAsync(cancellationToken);

        return new MergeRecordsResult { SurvivorId = survivorId, Merged = duplicateIds.Count, Moved = moved };
    }

    private async Task<int> MergeCustomers(string survivorId, List<string> duplicateIds, string? userId, CancellationToken ct)
    {
        var survivor = await _customers.GetAsync(survivorId, ct) ?? throw new Exception("The record to keep was not found.");
        var duplicates = await _customers.GetQuery().Where(x => duplicateIds.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);
        if (duplicates.Count == 0) throw new Exception("The duplicate records were not found.");
        var ids = duplicates.Select(x => x.Id).ToList();
        var moved = 0;

        var survivorEmails = (await _customerContacts.GetQuery()
            .Where(x => x.CustomerId == survivorId && !x.IsDeleted && x.EmailAddress != null)
            .Select(x => x.EmailAddress!).ToListAsync(ct))
            .Select(x => x.Trim().ToLowerInvariant()).ToHashSet();

        foreach (var contact in await _customerContacts.GetQuery().Where(x => ids.Contains(x.CustomerId!) && !x.IsDeleted).ToListAsync(ct))
        {
            var email = contact.EmailAddress?.Trim().ToLowerInvariant();
            if (email != null && survivorEmails.Contains(email))
            {
                contact.UpdatedById = userId;
                _customerContacts.Delete(contact);
            }
            else
            {
                if (email != null) survivorEmails.Add(email);
                contact.CustomerId = survivorId;
                contact.UpdatedById = userId;
                _customerContacts.Update(contact);
            }
            moved++;
        }

        foreach (var x in await _invoices.GetQuery().Where(x => ids.Contains(x.CustomerId!)).ToListAsync(ct)) { x.CustomerId = survivorId; _invoices.Update(x); moved++; }
        foreach (var x in await _quotations.GetQuery().Where(x => ids.Contains(x.CustomerId!)).ToListAsync(ct)) { x.CustomerId = survivorId; _quotations.Update(x); moved++; }
        foreach (var x in await _salesOrders.GetQuery().Where(x => ids.Contains(x.CustomerId!)).ToListAsync(ct)) { x.CustomerId = survivorId; _salesOrders.Update(x); moved++; }
        foreach (var x in await _supportCases.GetQuery().Where(x => ids.Contains(x.CustomerId!)).ToListAsync(ct)) { x.CustomerId = survivorId; _supportCases.Update(x); moved++; }
        foreach (var x in await _leads.GetQuery().Where(x => ids.Contains(x.ConvertedCustomerId!)).ToListAsync(ct)) { x.ConvertedCustomerId = survivorId; _leads.Update(x); moved++; }

        foreach (var duplicate in duplicates)
        {
            FillBlanks(survivor, duplicate);
            survivor.Tags = MergeTags(survivor.Tags, duplicate.Tags);
            duplicate.UpdatedById = userId;
            _customers.Delete(duplicate);
        }

        survivor.UpdatedById = userId;
        _customers.Update(survivor);
        return moved;
    }

    private async Task<int> MergeLeads(string survivorId, List<string> duplicateIds, string? userId, CancellationToken ct)
    {
        var survivor = await _leads.GetAsync(survivorId, ct) ?? throw new Exception("The record to keep was not found.");
        var duplicates = await _leads.GetQuery().Where(x => duplicateIds.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);
        if (duplicates.Count == 0) throw new Exception("The duplicate records were not found.");
        var ids = duplicates.Select(x => x.Id).ToList();
        var moved = 0;

        foreach (var x in await _leadContacts.GetQuery().Where(x => ids.Contains(x.LeadId!) && !x.IsDeleted).ToListAsync(ct)) { x.LeadId = survivorId; x.UpdatedById = userId; _leadContacts.Update(x); moved++; }
        foreach (var x in await _leadActivities.GetQuery().Where(x => ids.Contains(x.LeadId!) && !x.IsDeleted).ToListAsync(ct)) { x.LeadId = survivorId; x.UpdatedById = userId; _leadActivities.Update(x); moved++; }
        foreach (var x in await _customers.GetQuery().Where(x => ids.Contains(x.SourceLeadId!)).ToListAsync(ct)) { x.SourceLeadId = survivorId; _customers.Update(x); moved++; }

        foreach (var duplicate in duplicates)
        {
            FillBlanks(survivor, duplicate);
            survivor.Tags = MergeTags(survivor.Tags, duplicate.Tags);
            if (string.IsNullOrEmpty(survivor.ConvertedCustomerId) && !string.IsNullOrEmpty(duplicate.ConvertedCustomerId))
            {
                survivor.ConvertedCustomerId = duplicate.ConvertedCustomerId;
                survivor.ConvertedAtUtc = duplicate.ConvertedAtUtc;
            }
            duplicate.UpdatedById = userId;
            _leads.Delete(duplicate);
        }

        survivor.UpdatedById = userId;
        _leads.Update(survivor);
        return moved;
    }

    /// <summary>Copies text values the kept record is missing from a duplicate.</summary>
    private static void FillBlanks<T>(T survivor, T duplicate) where T : class
    {
        foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.PropertyType != typeof(string) || !p.CanWrite || Skipped.Contains(p.Name) || p.Name == "Tags") continue;
            if (string.IsNullOrWhiteSpace((string?)p.GetValue(survivor)) && !string.IsNullOrWhiteSpace((string?)p.GetValue(duplicate)))
            {
                p.SetValue(survivor, p.GetValue(duplicate));
            }
        }
    }

    private static string? MergeTags(string? a, string? b)
    {
        var all = (a ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat((b ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return all.Count == 0 ? null : string.Join(", ", all);
    }
}
