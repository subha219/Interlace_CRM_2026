using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.InvoiceManager.Commands;

public class CreateInvoiceResult
{
    public Invoice? Data { get; set; }
}

public class CreateInvoiceRequest : IRequest<CreateInvoiceResult>
{
    public DateTime? InvoiceDate { get; init; }
    public string? InvoiceStatus { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Description { get; init; }
    public string? CustomerId { get; init; }
    public string? TaxId { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateInvoiceValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.InvoiceDate).NotEmpty();
        RuleFor(x => x.InvoiceStatus).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty();
    }
}

public class CreateInvoiceHandler : IRequestHandler<CreateInvoiceRequest, CreateInvoiceResult>
{
    private readonly ICommandRepository<Invoice> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly InvoiceService _invoiceService;

    public CreateInvoiceHandler(
        ICommandRepository<Invoice> repository,
        IUnitOfWork unitOfWork,
        NumberSequenceService numberSequenceService,
        InvoiceService invoiceService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
        _invoiceService = invoiceService;
    }

    public async Task<CreateInvoiceResult> Handle(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Invoice();
        entity.CreatedById = request.CreatedById;

        entity.Number = _numberSequenceService.GenerateNumber(nameof(Invoice), "", "INV");
        entity.InvoiceDate = request.InvoiceDate;
        entity.InvoiceStatus = (InvoiceStatus)int.Parse(request.InvoiceStatus!);
        entity.DueDate = request.DueDate;
        entity.Description = request.Description;
        entity.CustomerId = request.CustomerId;
        entity.TaxId = request.TaxId;

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        _invoiceService.Recalculate(entity.Id);

        return new CreateInvoiceResult
        {
            Data = entity
        };
    }
}