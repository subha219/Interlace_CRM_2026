using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.QuotationManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.QuotationApprovalManager;

public static class PolicyReader
{
    public const double DefaultThreshold = 10;

    public static async Task<double> ThresholdAsync(IQueryContext context, CancellationToken ct)
    {
        var row = await context.SalesPolicy.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return row?.DiscountApprovalThresholdPercent ?? DefaultThreshold;
    }
}

// ---------- pricing panel ----------

public class GetQuotationPricingResult
{
    public string? Number { get; init; }
    public double Subtotal { get; init; }
    public double DiscountPercent { get; init; }
    public double DiscountAmount { get; init; }
    public double Net { get; init; }
    public double Total { get; init; }
    public string ApprovalStatus { get; init; } = "None";
    public string? ApprovalNote { get; init; }
    public double Threshold { get; init; }
    public int RevisionNumber { get; init; }
    public bool Locked { get; init; }
}

public class GetQuotationPricingRequest : IRequest<GetQuotationPricingResult> { public string? QuotationId { get; init; } }

public class GetQuotationPricingHandler : IRequestHandler<GetQuotationPricingRequest, GetQuotationPricingResult>
{
    private readonly IQueryContext _context;
    public GetQuotationPricingHandler(IQueryContext context) { _context = context; }

    public async Task<GetQuotationPricingResult> Handle(GetQuotationPricingRequest request, CancellationToken cancellationToken)
    {
        var q = await _context.Quotation.AsNoTracking().Include(x => x.QuotationItemList)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.QuotationId, cancellationToken) ?? throw new Exception("Quotation not found.");
        var subtotal = q.QuotationItemList.Where(i => !i.IsDeleted).Sum(i => i.Total ?? 0);
        return new GetQuotationPricingResult
        {
            Number = q.Number,
            Subtotal = subtotal,
            DiscountPercent = q.DiscountPercent ?? 0,
            DiscountAmount = q.DiscountAmount ?? 0,
            Net = q.BeforeTaxAmount ?? subtotal,
            Total = q.AfterTaxAmount ?? 0,
            ApprovalStatus = q.ApprovalStatus ?? "None",
            ApprovalNote = q.ApprovalNote,
            Threshold = await PolicyReader.ThresholdAsync(_context, cancellationToken),
            RevisionNumber = q.RevisionNumber ?? 1,
            Locked = q.QuotationStatus is QuotationStatus.Converted or QuotationStatus.Cancelled
        };
    }
}

// ---------- discount ----------

public class SetQuotationDiscountResult { public string ApprovalStatus { get; init; } = "None"; }

public class SetQuotationDiscountRequest : IRequest<SetQuotationDiscountResult>
{
    public string? QuotationId { get; init; }
    public double DiscountPercent { get; init; }
    public string? UserId { get; set; }
}

public class SetQuotationDiscountValidator : AbstractValidator<SetQuotationDiscountRequest>
{
    public SetQuotationDiscountValidator()
    {
        RuleFor(x => x.QuotationId).NotEmpty();
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
    }
}

public class SetQuotationDiscountHandler : IRequestHandler<SetQuotationDiscountRequest, SetQuotationDiscountResult>
{
    private readonly ICommandRepository<Quotation> _repository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuotationService _service;

    public SetQuotationDiscountHandler(ICommandRepository<Quotation> repository, IQueryContext context, IUnitOfWork unitOfWork, QuotationService service)
    {
        _repository = repository;
        _context = context;
        _unitOfWork = unitOfWork;
        _service = service;
    }

    public async Task<SetQuotationDiscountResult> Handle(SetQuotationDiscountRequest request, CancellationToken cancellationToken)
    {
        var q = await _repository.GetAsync(request.QuotationId!, cancellationToken) ?? throw new Exception("Quotation not found.");
        if (q.QuotationStatus is QuotationStatus.Converted or QuotationStatus.Cancelled)
            throw new Exception("A converted or cancelled quotation cannot change. Create a revision instead.");

        var threshold = await PolicyReader.ThresholdAsync(_context, cancellationToken);
        q.DiscountPercent = request.DiscountPercent;

        //>>> any change of the discount voids an earlier decision
        q.ApprovalStatus = request.DiscountPercent > threshold ? QuotationPolicy.Required : QuotationPolicy.None;
        q.ApprovalRequestedById = null;
        q.ApprovalDecidedById = null;
        q.ApprovalDecidedAtUtc = null;
        q.ApprovalNote = null;
        q.UpdatedById = request.UserId;

        _repository.Update(q);
        await _unitOfWork.SaveAsync(cancellationToken);
        _service.Recalculate(q.Id);

        return new SetQuotationDiscountResult { ApprovalStatus = q.ApprovalStatus };
    }
}

// ---------- request / decide ----------

public class RequestQuotationApprovalResult { public bool Requested { get; init; } }

public class RequestQuotationApprovalRequest : IRequest<RequestQuotationApprovalResult>
{
    public string? QuotationId { get; init; }
    public string? Note { get; init; }
    public string? UserId { get; set; }
}

public class RequestQuotationApprovalHandler : IRequestHandler<RequestQuotationApprovalRequest, RequestQuotationApprovalResult>
{
    private readonly ICommandRepository<Quotation> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public RequestQuotationApprovalHandler(ICommandRepository<Quotation> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<RequestQuotationApprovalResult> Handle(RequestQuotationApprovalRequest request, CancellationToken cancellationToken)
    {
        var q = await _repository.GetAsync(request.QuotationId ?? "", cancellationToken) ?? throw new Exception("Quotation not found.");
        if (q.ApprovalStatus != QuotationPolicy.Required && q.ApprovalStatus != QuotationPolicy.Rejected)
            throw new Exception("This quotation does not need approval right now.");

        q.ApprovalStatus = QuotationPolicy.Pending;
        q.ApprovalRequestedById = request.UserId;
        q.ApprovalNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        q.ApprovalDecidedById = null;
        q.ApprovalDecidedAtUtc = null;
        q.UpdatedById = request.UserId;
        _repository.Update(q);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new RequestQuotationApprovalResult { Requested = true };
    }
}

public record PendingApprovalDto(string? Id, string? Number, string? Customer, double Subtotal, double DiscountPercent, double DiscountAmount, double Total, string? Note, string? RequestedById, DateTime? RequestedAtUtc);
public class ListPendingApprovalsResult { public List<PendingApprovalDto> Data { get; init; } = new(); public double Threshold { get; init; } }
public class ListPendingApprovalsRequest : IRequest<ListPendingApprovalsResult> { }

public class ListPendingApprovalsHandler : IRequestHandler<ListPendingApprovalsRequest, ListPendingApprovalsResult>
{
    private readonly IQueryContext _context;
    public ListPendingApprovalsHandler(IQueryContext context) { _context = context; }

    public async Task<ListPendingApprovalsResult> Handle(ListPendingApprovalsRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.Quotation.AsNoTracking().Include(x => x.Customer).Include(x => x.QuotationItemList)
            .Where(x => !x.IsDeleted && x.ApprovalStatus == QuotationPolicy.Pending)
            .OrderBy(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);
        return new ListPendingApprovalsResult
        {
            Threshold = await PolicyReader.ThresholdAsync(_context, cancellationToken),
            Data = rows.Select(q => new PendingApprovalDto(q.Id, q.Number, q.Customer?.Name,
                q.QuotationItemList.Where(i => !i.IsDeleted).Sum(i => i.Total ?? 0), q.DiscountPercent ?? 0, q.DiscountAmount ?? 0, q.AfterTaxAmount ?? 0,
                q.ApprovalNote, q.ApprovalRequestedById, q.UpdatedAtUtc)).ToList()
        };
    }
}

public class DecideQuotationApprovalResult { public string ApprovalStatus { get; init; } = ""; }

public class DecideQuotationApprovalRequest : IRequest<DecideQuotationApprovalResult>
{
    public string? QuotationId { get; init; }
    public bool Approve { get; init; }
    public string? Note { get; init; }
    public string? UserId { get; set; }
}

public class DecideQuotationApprovalHandler : IRequestHandler<DecideQuotationApprovalRequest, DecideQuotationApprovalResult>
{
    private readonly ICommandRepository<Quotation> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DecideQuotationApprovalHandler(ICommandRepository<Quotation> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<DecideQuotationApprovalResult> Handle(DecideQuotationApprovalRequest request, CancellationToken cancellationToken)
    {
        var q = await _repository.GetAsync(request.QuotationId ?? "", cancellationToken) ?? throw new Exception("Quotation not found.");
        if (q.ApprovalStatus != QuotationPolicy.Pending) throw new Exception("This quotation is not waiting for a decision.");

        //>>> separation of duties
        if (!string.IsNullOrEmpty(q.ApprovalRequestedById) && q.ApprovalRequestedById == request.UserId)
            throw new Exception("You cannot decide on your own approval request.");

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Note))
            throw new Exception("Give a reason when rejecting.");

        q.ApprovalStatus = request.Approve ? QuotationPolicy.Approved : QuotationPolicy.Rejected;
        q.ApprovalDecidedById = request.UserId;
        q.ApprovalDecidedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note)) q.ApprovalNote = request.Note.Trim();
        q.UpdatedById = request.UserId;
        _repository.Update(q);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DecideQuotationApprovalResult { ApprovalStatus = q.ApprovalStatus };
    }
}

// ---------- policy ----------

public class GetSalesPolicyResult { public double DiscountApprovalThresholdPercent { get; init; } }
public class GetSalesPolicyRequest : IRequest<GetSalesPolicyResult> { }

public class GetSalesPolicyHandler : IRequestHandler<GetSalesPolicyRequest, GetSalesPolicyResult>
{
    private readonly IQueryContext _context;
    public GetSalesPolicyHandler(IQueryContext context) { _context = context; }

    public async Task<GetSalesPolicyResult> Handle(GetSalesPolicyRequest request, CancellationToken cancellationToken) =>
        new() { DiscountApprovalThresholdPercent = await PolicyReader.ThresholdAsync(_context, cancellationToken) };
}

public class SaveSalesPolicyResult { public bool Saved { get; init; } }

public class SaveSalesPolicyRequest : IRequest<SaveSalesPolicyResult>
{
    public double DiscountApprovalThresholdPercent { get; init; }
    public string? UserId { get; set; }
}

public class SaveSalesPolicyValidator : AbstractValidator<SaveSalesPolicyRequest>
{
    public SaveSalesPolicyValidator() { RuleFor(x => x.DiscountApprovalThresholdPercent).InclusiveBetween(0, 100); }
}

public class SaveSalesPolicyHandler : IRequestHandler<SaveSalesPolicyRequest, SaveSalesPolicyResult>
{
    private readonly ICommandRepository<SalesPolicy> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveSalesPolicyHandler(ICommandRepository<SalesPolicy> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveSalesPolicyResult> Handle(SaveSalesPolicyRequest request, CancellationToken cancellationToken)
    {
        var row = await _repository.GetQuery().Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (row == null)
        {
            await _repository.CreateAsync(new SalesPolicy { CreatedById = request.UserId, DiscountApprovalThresholdPercent = request.DiscountApprovalThresholdPercent }, cancellationToken);
        }
        else
        {
            row.DiscountApprovalThresholdPercent = request.DiscountApprovalThresholdPercent;
            row.UpdatedById = request.UserId;
            _repository.Update(row);
        }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveSalesPolicyResult { Saved = true };
    }
}

// ---------- revisions ----------

public class CreateQuotationRevisionResult { public string? Id { get; init; } public string? Number { get; init; } }

public class CreateQuotationRevisionRequest : IRequest<CreateQuotationRevisionResult>
{
    public string? QuotationId { get; init; }
    public string? UserId { get; set; }
}

/// <summary>Copies a quotation and its items into a new draft revision and cancels the old version.</summary>
public class CreateQuotationRevisionHandler : IRequestHandler<CreateQuotationRevisionRequest, CreateQuotationRevisionResult>
{
    private readonly ICommandRepository<Quotation> _quotations;
    private readonly ICommandRepository<QuotationItem> _items;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuotationService _service;

    public CreateQuotationRevisionHandler(ICommandRepository<Quotation> quotations, ICommandRepository<QuotationItem> items, IUnitOfWork unitOfWork, QuotationService service)
    {
        _quotations = quotations;
        _items = items;
        _unitOfWork = unitOfWork;
        _service = service;
    }

    public async Task<CreateQuotationRevisionResult> Handle(CreateQuotationRevisionRequest request, CancellationToken cancellationToken)
    {
        var old = await _quotations.GetQuery().Include(x => x.QuotationItemList)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.QuotationId, cancellationToken) ?? throw new Exception("Quotation not found.");
        if (old.QuotationStatus == QuotationStatus.Converted) throw new Exception("A converted quotation cannot be revised.");

        var revision = (old.RevisionNumber ?? 1) + 1;
        var baseNumber = (old.Number ?? "").Split("-R")[0];

        var copy = new Quotation
        {
            CreatedById = request.UserId,
            OwnerUserId = old.OwnerUserId,
            Number = $"{baseNumber}-R{revision}",
            QuotationDate = DateTime.Now,
            QuotationStatus = QuotationStatus.Draft,
            ValidUntil = old.ValidUntil,
            Description = old.Description,
            CustomerId = old.CustomerId,
            TaxId = old.TaxId,
            DiscountPercent = old.DiscountPercent,
            ApprovalStatus = null,
            RevisionNumber = revision,
            ParentQuotationId = old.Id
        };
        await _quotations.CreateAsync(copy, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        foreach (var item in old.QuotationItemList.Where(i => !i.IsDeleted))
        {
            await _items.CreateAsync(new QuotationItem
            {
                CreatedById = request.UserId,
                QuotationId = copy.Id,
                ProductId = item.ProductId,
                Summary = item.Summary,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                Total = item.Total
            }, cancellationToken);
        }
        await _unitOfWork.SaveAsync(cancellationToken);

        old.QuotationStatus = QuotationStatus.Cancelled;
        old.UpdatedById = request.UserId;
        _quotations.Update(old);
        await _unitOfWork.SaveAsync(cancellationToken);

        _service.Recalculate(copy.Id);
        return new CreateQuotationRevisionResult { Id = copy.Id, Number = copy.Number };
    }
}
