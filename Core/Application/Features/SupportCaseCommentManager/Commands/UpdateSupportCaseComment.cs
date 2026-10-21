using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.SupportCaseCommentManager.Commands;

public class UpdateSupportCaseCommentResult
{
    public SupportCaseComment? Data { get; set; }
}

public class UpdateSupportCaseCommentRequest : IRequest<UpdateSupportCaseCommentResult>
{
    public string? Id { get; init; }
    public string? SupportCaseId { get; init; }
    public string? Comment { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateSupportCaseCommentValidator : AbstractValidator<UpdateSupportCaseCommentRequest>
{
    public UpdateSupportCaseCommentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SupportCaseId).NotEmpty();
        RuleFor(x => x.Comment).NotEmpty();
    }
}

public class UpdateSupportCaseCommentHandler : IRequestHandler<UpdateSupportCaseCommentRequest, UpdateSupportCaseCommentResult>
{
    private readonly ICommandRepository<SupportCaseComment> _repository;
    private readonly IUnitOfWork _unitOfWork;


    public UpdateSupportCaseCommentHandler(
        ICommandRepository<SupportCaseComment> repository,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;

    }

    public async Task<UpdateSupportCaseCommentResult> Handle(UpdateSupportCaseCommentRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.UpdatedById;
        entity.SupportCaseId = request.SupportCaseId;
        entity.Comment = request.Comment;


        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);


        return new UpdateSupportCaseCommentResult
        {
            Data = entity
        };
    }
}
