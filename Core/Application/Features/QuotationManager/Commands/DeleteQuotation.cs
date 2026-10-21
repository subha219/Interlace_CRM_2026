using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.QuotationManager.Commands;

public class DeleteQuotationResult
{
    public Quotation? Data { get; set; }
}

public class DeleteQuotationRequest : IRequest<DeleteQuotationResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteQuotationValidator : AbstractValidator<DeleteQuotationRequest>
{
    public DeleteQuotationValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteQuotationHandler : IRequestHandler<DeleteQuotationRequest, DeleteQuotationResult>
{
    private readonly ICommandRepository<Quotation> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteQuotationHandler(
        ICommandRepository<Quotation> repository,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteQuotationResult> Handle(DeleteQuotationRequest request, CancellationToken cancellationToken)
    {

        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;

        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new DeleteQuotationResult
        {
            Data = entity
        };
    }
}

