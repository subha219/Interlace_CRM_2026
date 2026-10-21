using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.SupportCaseManager.Commands;

public class DeleteSupportCaseResult
{
    public SupportCase? Data { get; set; }
}

public class DeleteSupportCaseRequest : IRequest<DeleteSupportCaseResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteSupportCaseValidator : AbstractValidator<DeleteSupportCaseRequest>
{
    public DeleteSupportCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteSupportCaseHandler : IRequestHandler<DeleteSupportCaseRequest, DeleteSupportCaseResult>
{
    private readonly ICommandRepository<SupportCase> _repository;
    private readonly IUnitOfWork _unitOfWork;


    public DeleteSupportCaseHandler(
        ICommandRepository<SupportCase> repository,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;

    }

    public async Task<DeleteSupportCaseResult> Handle(DeleteSupportCaseRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;

        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);


        return new DeleteSupportCaseResult
        {
            Data = entity
        };
    }
}
