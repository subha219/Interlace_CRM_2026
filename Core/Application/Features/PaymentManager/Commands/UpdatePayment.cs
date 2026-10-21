using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.PaymentManager.Commands;

public class UpdatePaymentResult
{
    public Payment? Data { get; set; }
}

public class UpdatePaymentRequest : IRequest<UpdatePaymentResult>
{
    public string? Id { get; init; }
    public string? InvoiceId { get; init; }
    public DateTime? PaymentDate { get; init; }
    public double? Amount { get; init; }
    public string? Method { get; init; }
    public string? Reference { get; init; }
    public string? Description { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdatePaymentValidator : AbstractValidator<UpdatePaymentRequest>
{
    public UpdatePaymentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.PaymentDate).NotNull();
        RuleFor(x => x.Amount).NotNull();
        RuleFor(x => x.Method).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class UpdatePaymentHandler : IRequestHandler<UpdatePaymentRequest, UpdatePaymentResult>
{
    private readonly ICommandRepository<Payment> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;

    public UpdatePaymentHandler(
        ICommandRepository<Payment> repository,
        IUnitOfWork unitOfWork,
        InvoiceService invoiceService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<UpdatePaymentResult> Handle(UpdatePaymentRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.UpdatedById;
        var previousInvoiceId = entity.InvoiceId;
        entity.InvoiceId = request.InvoiceId;
        entity.PaymentDate = request.PaymentDate;
        entity.Amount = request.Amount;
        entity.Method = (PaymentMethod)int.Parse(request.Method!);
        entity.Reference = request.Reference;
        entity.Description = request.Description;


        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        _invoiceService.Recalculate(entity.InvoiceId ?? string.Empty);
        if (!string.IsNullOrEmpty(previousInvoiceId) && previousInvoiceId != entity.InvoiceId)
        {
            _invoiceService.Recalculate(previousInvoiceId);
        }

        return new UpdatePaymentResult
        {
            Data = entity
        };
    }
}
