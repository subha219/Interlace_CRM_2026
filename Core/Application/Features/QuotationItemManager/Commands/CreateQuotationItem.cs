using Application.Common.Repositories;
using Application.Features.QuotationManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.QuotationItemManager.Commands;

public class CreateQuotationItemResult
{
    public QuotationItem? Data { get; set; }
}

public class CreateQuotationItemRequest : IRequest<CreateQuotationItemResult>
{
    public string? QuotationId { get; init; }
    public string? ProductId { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateQuotationItemValidator : AbstractValidator<CreateQuotationItemRequest>
{
    public CreateQuotationItemValidator()
    {
        RuleFor(x => x.QuotationId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitPrice).NotEmpty();
        RuleFor(x => x.Quantity).NotEmpty();
    }
}

public class CreateQuotationItemHandler : IRequestHandler<CreateQuotationItemRequest, CreateQuotationItemResult>
{
    private readonly ICommandRepository<QuotationItem> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuotationService _quotationService;

    public CreateQuotationItemHandler(
        ICommandRepository<QuotationItem> repository,
        IUnitOfWork unitOfWork,
        QuotationService quotationService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _quotationService = quotationService;
    }

    public async Task<CreateQuotationItemResult> Handle(CreateQuotationItemRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new QuotationItem();
        entity.CreatedById = request.CreatedById;

        entity.QuotationId = request.QuotationId;
        entity.ProductId = request.ProductId;
        entity.Summary = request.Summary;
        entity.UnitPrice = request.UnitPrice;
        entity.Quantity = request.Quantity;

        entity.Total = entity.Quantity * entity.UnitPrice;

        await _repository.CreateAsync(entity, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        _quotationService.Recalculate(entity.QuotationId ?? "");

        return new CreateQuotationItemResult
        {
            Data = entity
        };
    }
}