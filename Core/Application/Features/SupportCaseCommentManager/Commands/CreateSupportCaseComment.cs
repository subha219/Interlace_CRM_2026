using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.SupportCaseCommentManager.Commands;

public class CreateSupportCaseCommentResult
{
    public SupportCaseComment? Data { get; set; }
}

public class CreateSupportCaseCommentRequest : IRequest<CreateSupportCaseCommentResult>
{
    public string? SupportCaseId { get; init; }
    public string? Comment { get; init; }
    public string? CreatedById { get; init; }
}

public class CreateSupportCaseCommentValidator : AbstractValidator<CreateSupportCaseCommentRequest>
{
    public CreateSupportCaseCommentValidator()
    {
        RuleFor(x => x.SupportCaseId).NotEmpty();
        RuleFor(x => x.Comment).NotEmpty();
    }
}

public class CreateSupportCaseCommentHandler : IRequestHandler<CreateSupportCaseCommentRequest, CreateSupportCaseCommentResult>
{
    private readonly ICommandRepository<SupportCaseComment> _repository;
    private readonly ICommandRepository<SupportCase> _cases;
    private readonly IUnitOfWork _unitOfWork;


    public CreateSupportCaseCommentHandler(
        ICommandRepository<SupportCaseComment> repository,
        ICommandRepository<SupportCase> cases,
        IUnitOfWork unitOfWork
    )
    {
        _cases = cases;
        _repository = repository;
        _unitOfWork = unitOfWork;

    }

    public async Task<CreateSupportCaseCommentResult> Handle(CreateSupportCaseCommentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SupportCaseComment();
        entity.CreatedById = request.CreatedById;

        entity.SupportCaseId = request.SupportCaseId;
        entity.Comment = request.Comment;


        await _repository.CreateAsync(entity, cancellationToken);

        //>>> the first comment on a case is its first response
        if (!string.IsNullOrEmpty(request.SupportCaseId))
        {
            var supportCase = await _cases.GetAsync(request.SupportCaseId, cancellationToken);
            if (supportCase != null && supportCase.FirstRespondedAt == null)
            {
                supportCase.FirstRespondedAt = DateTime.Now;
                _cases.Update(supportCase);
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);


        return new CreateSupportCaseCommentResult
        {
            Data = entity
        };
    }
}
