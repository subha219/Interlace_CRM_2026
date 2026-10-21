using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.QuotationManager.Commands;

public class UpdateQuotationResult
{
    public Quotation? Data { get; set; }
}

public class UpdateQuotationRequest : IRequest<UpdateQuotationResult>
{
    public string? Id { get; init; }
    public DateTime? QuotationDate { get; init; }
    public string? QuotationStatus { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? Description { get; init; }
    public string? CustomerId { get; init; }
    public string? TaxId { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateQuotationValidator : AbstractValidator<UpdateQuotationRequest>
{
    public UpdateQuotationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.QuotationDate).NotEmpty();
        RuleFor(x => x.QuotationStatus).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty();
    }
}

public class UpdateQuotationHandler : IRequestHandler<UpdateQuotationRequest, UpdateQuotationResult>
{
    private readonly ICommandRepository<Quotation> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuotationService _quotationService;

    public UpdateQuotationHandler(
        ICommandRepository<Quotation> repository,
        QuotationService quotationService,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _quotationService = quotationService;
    }

    public async Task<UpdateQuotationResult> Handle(UpdateQuotationRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        var newStatus = (QuotationStatus)int.Parse(request.QuotationStatus!);
        if (newStatus != entity.QuotationStatus)
        {
            QuotationPolicy.EnsureCanProceed(entity, newStatus);
        }

        entity.UpdatedById = request.UpdatedById;

        entity.QuotationDate = request.QuotationDate;
        entity.QuotationStatus = (QuotationStatus)int.Parse(request.QuotationStatus!);
        if (request.ValidUntil != null)
        {
            entity.ValidUntil = request.ValidUntil;
        }

        entity.Description = request.Description;
        entity.CustomerId = request.CustomerId;
        entity.TaxId = request.TaxId;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        _quotationService.Recalculate(entity.Id);

        return new UpdateQuotationResult
        {
            Data = entity
        };
    }
}

