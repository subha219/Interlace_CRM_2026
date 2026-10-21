using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.QuotationManager.Commands;

public class CreateQuotationResult
{
    public Quotation? Data { get; set; }
}

public class CreateQuotationRequest : IRequest<CreateQuotationResult>
{
    public DateTime? QuotationDate { get; init; }
    public string? QuotationStatus { get; init; }
    public DateTime? ValidUntil { get; init; }
    public string? Description { get; init; }
    public string? CustomerId { get; init; }
    public string? TaxId { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateQuotationValidator : AbstractValidator<CreateQuotationRequest>
{
    public CreateQuotationValidator()
    {
        RuleFor(x => x.QuotationDate).NotEmpty();
        RuleFor(x => x.QuotationStatus).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty();
    }
}

public class CreateQuotationHandler : IRequestHandler<CreateQuotationRequest, CreateQuotationResult>
{
    private readonly ICommandRepository<Quotation> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly QuotationService _quotationService;

    public CreateQuotationHandler(
        ICommandRepository<Quotation> repository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        QuotationService quotationService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
        _quotationService = quotationService;
    }

    public async Task<CreateQuotationResult> Handle(CreateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Quotation();
        entity.CreatedById = request.CreatedById;

        entity.Number = _numberSequenceService.GenerateNumber(nameof(Quotation), "", "QUO");
        entity.QuotationDate = request.QuotationDate;
        entity.QuotationStatus = (QuotationStatus)int.Parse(request.QuotationStatus!);
        entity.ValidUntil = request.ValidUntil ?? request.QuotationDate?.AddDays(30);
        entity.Description = request.Description;
        entity.CustomerId = request.CustomerId;
        entity.TaxId = request.TaxId;

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        _quotationService.Recalculate(entity.Id);

        return new CreateQuotationResult
        {
            Data = entity
        };
    }
}