using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.PaymentManager.Commands;

public class CreatePaymentResult
{
    public Payment? Data { get; set; }
}

public class CreatePaymentRequest : IRequest<CreatePaymentResult>
{
    public string? InvoiceId { get; init; }
    public DateTime? PaymentDate { get; init; }
    public double? Amount { get; init; }
    public string? Method { get; init; }
    public string? Reference { get; init; }
    public string? Description { get; init; }
    public string? CreatedById { get; init; }
}

public class CreatePaymentValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.PaymentDate).NotNull();
        RuleFor(x => x.Amount).NotNull();
        RuleFor(x => x.Method).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class CreatePaymentHandler : IRequestHandler<CreatePaymentRequest, CreatePaymentResult>
{
    private readonly ICommandRepository<Payment> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;
    private readonly InvoiceService _invoiceService;

    public CreatePaymentHandler(
        ICommandRepository<Payment> repository,
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

    public async Task<CreatePaymentResult> Handle(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Payment();
        entity.CreatedById = request.CreatedById;
        entity.Number = _numberSequenceService.GenerateNumber(nameof(Payment), "", "PAY");
        entity.InvoiceId = request.InvoiceId;
        entity.PaymentDate = request.PaymentDate;
        entity.Amount = request.Amount;
        entity.Method = (PaymentMethod)int.Parse(request.Method!);
        entity.Reference = request.Reference;
        entity.Description = request.Description;


        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);
        _invoiceService.Recalculate(entity.InvoiceId ?? string.Empty);

        return new CreatePaymentResult
        {
            Data = entity
        };
    }
}
