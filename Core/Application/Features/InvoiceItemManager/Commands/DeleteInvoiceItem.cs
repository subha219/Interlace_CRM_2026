using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.InvoiceItemManager.Commands;

public class DeleteInvoiceItemResult
{
    public InvoiceItem? Data { get; set; }
}

public class DeleteInvoiceItemRequest : IRequest<DeleteInvoiceItemResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteInvoiceItemValidator : AbstractValidator<DeleteInvoiceItemRequest>
{
    public DeleteInvoiceItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteInvoiceItemHandler : IRequestHandler<DeleteInvoiceItemRequest, DeleteInvoiceItemResult>
{
    private readonly ICommandRepository<InvoiceItem> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;

    public DeleteInvoiceItemHandler(
        ICommandRepository<InvoiceItem> repository,
        IUnitOfWork unitOfWork,
        InvoiceService invoiceService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<DeleteInvoiceItemResult> Handle(DeleteInvoiceItemRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;

        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        _invoiceService.Recalculate(entity.InvoiceId ?? "");

        return new DeleteInvoiceItemResult
        {
            Data = entity
        };
    }
}

