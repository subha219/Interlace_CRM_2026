using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ContractManager;

public static class ContractRules
{
    public static readonly string[] Statuses = { "Draft", "Active", "Cancelled", "Renewed" };
    public static readonly string[] Cycles = { "Monthly", "Quarterly", "Yearly", "OneTime" };

    /// <summary>The status shown to people: an active contract past its end date is Expired.</summary>
    public static string Effective(string? status, DateTime? end, DateTime today) =>
        (status ?? "Draft") == "Active" && end != null && end.Value.Date < today.Date ? "Expired" : (status ?? "Draft");

    public static int? DaysLeft(string? status, DateTime? end, DateTime today) =>
        (status ?? "") == "Active" && end != null ? (int)(end.Value.Date - today.Date).TotalDays : null;

    /// <summary>Length of the term, used to start a renewal for the same period.</summary>
    public static (DateTime Start, DateTime End) NextTerm(DateTime? start, DateTime end)
    {
        var days = start == null ? 365 : Math.Max(1, (int)(end.Date - start.Value.Date).TotalDays);
        var ns = end.Date.AddDays(1);
        return (ns, ns.AddDays(days));
    }
}

public class ContractDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Title { get; init; }
    public string? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? Status { get; init; }
    public string? EffectiveStatus { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? DaysLeft { get; init; }
    public double? Value { get; init; }
    public string? BillingCycle { get; init; }
    public bool AutoRenew { get; init; }
    public int RenewalNoticeDays { get; init; }
    public string? Notes { get; init; }
    public string? OwnerUserId { get; init; }
}

public class GetContractsResult
{
    public List<ContractDto> Data { get; init; } = new();
    public double ActiveValue { get; init; }
    public int Expiring30 { get; init; }
    public int Expiring60 { get; init; }
    public int Expiring90 { get; init; }
    public int ExpiredNotRenewed { get; init; }
}

public class GetContractsRequest : IRequest<GetContractsResult> { public string? Filter { get; init; } public string? CustomerId { get; init; } }

public class GetContractsHandler : IRequestHandler<GetContractsRequest, GetContractsResult>
{
    private readonly IQueryContext _context;
    public GetContractsHandler(IQueryContext context) { _context = context; }

    public async Task<GetContractsResult> Handle(GetContractsRequest request, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var all = await _context.Contract.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.EndDate).ToListAsync(cancellationToken);
        var names = await _context.Customer.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var dtos = all.Select(x => new ContractDto
        {
            Id = x.Id, Number = x.Number, Title = x.Title, CustomerId = x.CustomerId, CustomerName = x.CustomerId != null && names.TryGetValue(x.CustomerId, out var n) ? n : null,
            Status = x.Status, EffectiveStatus = ContractRules.Effective(x.Status, x.EndDate, today), StartDate = x.StartDate, EndDate = x.EndDate,
            DaysLeft = ContractRules.DaysLeft(x.Status, x.EndDate, today), Value = x.Value, BillingCycle = x.BillingCycle, AutoRenew = x.AutoRenew,
            RenewalNoticeDays = x.RenewalNoticeDays, Notes = x.Notes, OwnerUserId = x.OwnerUserId
        }).ToList();

        var activeNow = dtos.Where(x => x.EffectiveStatus == "Active").ToList();
        int Within(int d) => activeNow.Count(x => x.DaysLeft != null && x.DaysLeft <= d);

        IEnumerable<ContractDto> filtered = dtos;
        if (!string.IsNullOrEmpty(request.CustomerId)) filtered = filtered.Where(x => x.CustomerId == request.CustomerId);
        filtered = request.Filter switch
        {
            "Active" => filtered.Where(x => x.EffectiveStatus == "Active"),
            "Expiring" => filtered.Where(x => x.EffectiveStatus == "Active" && x.DaysLeft != null && x.DaysLeft <= x.RenewalNoticeDays),
            "Expired" => filtered.Where(x => x.EffectiveStatus == "Expired"),
            "Draft" => filtered.Where(x => x.EffectiveStatus == "Draft"),
            _ => filtered
        };

        return new GetContractsResult
        {
            Data = filtered.ToList(),
            ActiveValue = activeNow.Sum(x => x.Value ?? 0),
            Expiring30 = Within(30), Expiring60 = Within(60), Expiring90 = Within(90),
            ExpiredNotRenewed = dtos.Count(x => x.EffectiveStatus == "Expired")
        };
    }
}

public class SaveContractResult { public string? Id { get; init; } }

public class SaveContractRequest : IRequest<SaveContractResult>
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? CustomerId { get; init; }
    public string? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public double? Value { get; init; }
    public string? BillingCycle { get; init; }
    public bool AutoRenew { get; init; }
    public int RenewalNoticeDays { get; init; } = 60;
    public string? Notes { get; init; }
    public string? UserId { get; set; }
}

public class SaveContractValidator : AbstractValidator<SaveContractRequest>
{
    public SaveContractValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Status).Must(s => ContractRules.Statuses.Contains(s ?? "")).WithMessage("Unknown status.");
        RuleFor(x => x.BillingCycle).Must(s => string.IsNullOrEmpty(s) || ContractRules.Cycles.Contains(s)).WithMessage("Unknown billing cycle.");
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0).When(x => x.Value != null);
        RuleFor(x => x.RenewalNoticeDays).InclusiveBetween(0, 365);
        RuleFor(x => x.EndDate).NotNull().When(x => x.Status == "Active").WithMessage("An active contract needs an end date.");
        RuleFor(x => x).Must(x => x.StartDate == null || x.EndDate == null || x.EndDate >= x.StartDate).WithMessage("The end date cannot be before the start date.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class SaveContractHandler : IRequestHandler<SaveContractRequest, SaveContractResult>
{
    private readonly ICommandRepository<Contract> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;
    public SaveContractHandler(ICommandRepository<Contract> repository, IUnitOfWork unitOfWork, NumberSequenceService numbers) { _repository = repository; _unitOfWork = unitOfWork; _numbers = numbers; }

    public async Task<SaveContractResult> Handle(SaveContractRequest request, CancellationToken cancellationToken)
    {
        var isNew = string.IsNullOrEmpty(request.Id);
        var c = isNew ? new Contract { CreatedById = request.UserId, Number = _numbers.GenerateNumber(nameof(Contract), "", "CTR") }
                      : await _repository.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Contract not found.");
        c.Title = request.Title!.Trim();
        c.CustomerId = request.CustomerId;
        c.Status = request.Status;
        c.StartDate = request.StartDate;
        c.EndDate = request.EndDate;
        c.Value = request.Value;
        c.BillingCycle = string.IsNullOrEmpty(request.BillingCycle) ? null : request.BillingCycle;
        c.AutoRenew = request.AutoRenew;
        c.RenewalNoticeDays = request.RenewalNoticeDays;
        c.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        c.UpdatedById = request.UserId;
        if (isNew) await _repository.CreateAsync(c, cancellationToken); else _repository.Update(c);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveContractResult { Id = c.Id };
    }
}

public class DeleteContractRequest : IRequest<bool> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteContractHandler : IRequestHandler<DeleteContractRequest, bool>
{
    private readonly ICommandRepository<Contract> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteContractHandler(ICommandRepository<Contract> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(DeleteContractRequest request, CancellationToken cancellationToken)
    {
        var c = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Contract not found.");
        c.UpdatedById = request.UserId;
        _repository.Delete(c);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

public class RenewContractResult { public string? NewId { get; init; } public string? Number { get; init; } }

public class RenewContractRequest : IRequest<RenewContractResult>
{
    public string? Id { get; init; }

    /// <summary>Optional new value (for example a price increase).</summary>
    public double? Value { get; init; }
    public string? UserId { get; set; }
}

/// <summary>Creates the follow-on contract for the same period and marks the old one as Renewed.</summary>
public class RenewContractHandler : IRequestHandler<RenewContractRequest, RenewContractResult>
{
    private readonly ICommandRepository<Contract> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;
    public RenewContractHandler(ICommandRepository<Contract> repository, IUnitOfWork unitOfWork, NumberSequenceService numbers) { _repository = repository; _unitOfWork = unitOfWork; _numbers = numbers; }

    public async Task<RenewContractResult> Handle(RenewContractRequest request, CancellationToken cancellationToken)
    {
        var old = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Contract not found.");
        if (old.Status == "Renewed") throw new Exception("This contract was already renewed.");
        if (old.Status == "Cancelled" || old.Status == "Draft") throw new Exception("Only an active contract can be renewed.");
        if (old.EndDate == null) throw new Exception("The contract has no end date.");

        var (start, end) = ContractRules.NextTerm(old.StartDate, old.EndDate.Value);
        var next = new Contract
        {
            CreatedById = request.UserId, Number = _numbers.GenerateNumber(nameof(Contract), "", "CTR"), Title = old.Title, CustomerId = old.CustomerId,
            Status = "Active", StartDate = start, EndDate = end, Value = request.Value ?? old.Value, BillingCycle = old.BillingCycle, AutoRenew = old.AutoRenew,
            RenewalNoticeDays = old.RenewalNoticeDays, Notes = old.Notes, OwnerUserId = old.OwnerUserId, RenewedFromId = old.Id
        };
        old.Status = "Renewed";
        old.UpdatedById = request.UserId;
        _repository.Update(old);
        await _repository.CreateAsync(next, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new RenewContractResult { NewId = next.Id, Number = next.Number };
    }
}
