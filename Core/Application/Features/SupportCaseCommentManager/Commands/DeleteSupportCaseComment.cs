using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.SupportCaseCommentManager.Commands;

public class DeleteSupportCaseCommentResult
{
    public SupportCaseComment? Data { get; set; }
}

public class DeleteSupportCaseCommentRequest : IRequest<DeleteSupportCaseCommentResult>
{
    public string? Id { get; init; }
    public string? DeletedById { get; init; }
}

public class DeleteSupportCaseCommentValidator : AbstractValidator<DeleteSupportCaseCommentRequest>
{
    public DeleteSupportCaseCommentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteSupportCaseCommentHandler : IRequestHandler<DeleteSupportCaseCommentRequest, DeleteSupportCaseCommentResult>
{
    private readonly ICommandRepository<SupportCaseComment> _repository;
    private readonly IUnitOfWork _unitOfWork;


    public DeleteSupportCaseCommentHandler(
        ICommandRepository<SupportCaseComment> repository,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;

    }

    public async Task<DeleteSupportCaseCommentResult> Handle(DeleteSupportCaseCommentRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.DeletedById;

        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);


        return new DeleteSupportCaseCommentResult
        {
            Data = entity
        };
    }
}
