using Application.Common.Repositories;
using Application.Features.InvoiceManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.InvoiceItemManager.Commands;

public class CreateInvoiceItemResult
{
    public InvoiceItem? Data { get; set; }
}

public class CreateInvoiceItemRequest : IRequest<CreateInvoiceItemResult>
{
    public string? InvoiceId { get; init; }
    public string? ProductId { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateInvoiceItemValidator : AbstractValidator<CreateInvoiceItemRequest>
{
    public CreateInvoiceItemValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitPrice).NotEmpty();
        RuleFor(x => x.Quantity).NotEmpty();
    }
}

public class CreateInvoiceItemHandler : IRequestHandler<CreateInvoiceItemRequest, CreateInvoiceItemResult>
{
    private readonly ICommandRepository<InvoiceItem> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly InvoiceService _invoiceService;

    public CreateInvoiceItemHandler(
        ICommandRepository<InvoiceItem> repository,
        IUnitOfWork unitOfWork,
        InvoiceService invoiceService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _invoiceService = invoiceService;
    }

    public async Task<CreateInvoiceItemResult> Handle(CreateInvoiceItemRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new InvoiceItem();
        entity.CreatedById = request.CreatedById;

        entity.InvoiceId = request.InvoiceId;
        entity.ProductId = request.ProductId;
        entity.Summary = request.Summary;
        entity.UnitPrice = request.UnitPrice;
        entity.Quantity = request.Quantity;

        entity.Total = entity.Quantity * entity.UnitPrice;

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        _invoiceService.Recalculate(entity.InvoiceId ?? "");

        return new CreateInvoiceItemResult
        {
            Data = entity
        };
    }
}