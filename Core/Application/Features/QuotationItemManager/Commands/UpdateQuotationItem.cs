using Application.Common.Repositories;
using Application.Features.QuotationManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.QuotationItemManager.Commands;

public class UpdateQuotationItemResult
{
    public QuotationItem? Data { get; set; }
}

public class UpdateQuotationItemRequest : IRequest<UpdateQuotationItemResult>
{
    public string? Id { get; init; }
    public string? QuotationId { get; init; }
    public string? ProductId { get; init; }
    public string? Summary { get; init; }
    public double? UnitPrice { get; init; }
    public double? Quantity { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateQuotationItemValidator : AbstractValidator<UpdateQuotationItemRequest>
{
    public UpdateQuotationItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.QuotationId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.UnitPrice).NotEmpty();
        RuleFor(x => x.Quantity).NotEmpty();
    }
}

public class UpdateQuotationItemHandler : IRequestHandler<UpdateQuotationItemRequest, UpdateQuotationItemResult>
{
    private readonly ICommandRepository<QuotationItem> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuotationService _quotationService;

    public UpdateQuotationItemHandler(
        ICommandRepository<QuotationItem> repository,
        IUnitOfWork unitOfWork,
        QuotationService quotationService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _quotationService = quotationService;
    }

    public async Task<UpdateQuotationItemResult> Handle(UpdateQuotationItemRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.UpdatedById;

        entity.QuotationId = request.QuotationId;
        entity.ProductId = request.ProductId;
        entity.Summary = request.Summary;
        entity.UnitPrice = request.UnitPrice;
        entity.Quantity = request.Quantity;

        entity.Total = entity.UnitPrice * entity.Quantity;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        _quotationService.Recalculate(entity.QuotationId ?? "");

        return new UpdateQuotationItemResult
        {
            Data = entity
        };
    }
}

