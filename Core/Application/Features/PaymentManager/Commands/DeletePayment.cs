using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.PaymentManager.Commands;

public class DeletePaymentResult
{
    public Payment? Data { get; set; }
}

public class DeletePaymentRequest : IRequest<DeletePaymentResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeletePaymentValidator : AbstractValidator<DeletePaymentRequest>
{
    public DeletePaymentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeletePaymentHandler : IRequestHandler<DeletePaymentRequest, DeletePaymentResult>
{
    private readonly ICommandRepository<Payment> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;

    public DeletePaymentHandler(
        ICommandRepository<Payment> repository,
        IUnitOfWork unitOfWork,
        InvoiceService invoiceService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<DeletePaymentResult> Handle(DeletePaymentRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;

        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        _invoiceService.Recalculate(entity.InvoiceId ?? string.Empty);

        return new DeletePaymentResult
        {
            Data = entity
        };
    }
}
