using Application.Common.Repositories;
using Application.Features.QuotationManager;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.QuotationItemManager.Commands;

public class DeleteQuotationItemResult
{
    public QuotationItem? Data { get; set; }
}

public class DeleteQuotationItemRequest : IRequest<DeleteQuotationItemResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteQuotationItemValidator : AbstractValidator<DeleteQuotationItemRequest>
{
    public DeleteQuotationItemValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteQuotationItemHandler : IRequestHandler<DeleteQuotationItemRequest, DeleteQuotationItemResult>
{
    private readonly ICommandRepository<QuotationItem> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuotationService _quotationService;

    public DeleteQuotationItemHandler(
        ICommandRepository<QuotationItem> repository,
        IUnitOfWork unitOfWork,
        QuotationService quotationService
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _quotationService = quotationService;
    }

    public async Task<DeleteQuotationItemResult> Handle(DeleteQuotationItemRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;

        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        _quotationService.Recalculate(entity.QuotationId ?? "");

        return new DeleteQuotationItemResult
        {
            Data = entity
        };
    }
}

