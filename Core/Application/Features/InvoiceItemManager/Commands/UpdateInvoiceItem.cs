using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.InvoiceItemManager.Commands;

public class UpdateInvoiceItemResult
{
    public InvoiceItem? Data { get; set; }
}

public class UpdateInvoiceItemRequest : IRequest<UpdateInvoiceItemResult>
{
    public string? Id { get; init; }
    public string? InvoiceId { get; init; }
    public string? ProductId { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateInvoiceItemValidator : AbstractValidator<UpdateInvoiceItemRequest>
{
    public UpdateInvoiceItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitPrice).NotEmpty();
        RuleFor(x => x.Quantity).NotEmpty();
    }
}

public class UpdateInvoiceItemHandler : IRequestHandler<UpdateInvoiceItemRequest, UpdateInvoiceItemResult>
{
    private readonly ICommandRepository<InvoiceItem> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;

    public UpdateInvoiceItemHandler(
        ICommandRepository<InvoiceItem> repository,
        IUnitOfWork unitOfWork,
        InvoiceService invoiceService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<UpdateInvoiceItemResult> Handle(UpdateInvoiceItemRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.UpdatedById;

        entity.InvoiceId = request.InvoiceId;
        entity.ProductId = request.ProductId;
        entity.Summary = request.Summary;
        entity.UnitPrice = request.UnitPrice;
        entity.Quantity = request.Quantity;

        entity.Total = entity.UnitPrice * entity.Quantity;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        _invoiceService.Recalculate(entity.InvoiceId ?? "");

        return new UpdateInvoiceItemResult
        {
            Data = entity
        };
    }
}

