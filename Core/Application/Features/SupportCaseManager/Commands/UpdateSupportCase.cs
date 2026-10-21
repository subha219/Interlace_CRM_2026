using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.SupportCaseManager.Commands;

public class UpdateSupportCaseResult
{
    public SupportCase? Data { get; set; }
}

public class UpdateSupportCaseRequest : IRequest<UpdateSupportCaseResult>
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? Priority { get; init; }
    public string? Channel { get; init; }
    public string? CustomerId { get; init; }
    public string? SalesRepresentativeId { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Description { get; init; }
    public string? Resolution { get; init; }
    public string? UpdatedById { get; init; }
}

public class UpdateSupportCaseValidator : AbstractValidator<UpdateSupportCaseRequest>
{
    public UpdateSupportCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.Priority).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}

public class UpdateSupportCaseHandler : IRequestHandler<UpdateSupportCaseRequest, UpdateSupportCaseResult>
{
    private readonly ICommandRepository<SupportCase> _repository;
    private readonly IUnitOfWork _unitOfWork;


    public UpdateSupportCaseHandler(
        ICommandRepository<SupportCase> repository,
        IUnitOfWork unitOfWork
        )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;

    }

    public async Task<UpdateSupportCaseResult> Handle(UpdateSupportCaseRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.Id}");
        }

        entity.UpdatedById = request.UpdatedById;
        entity.Title = request.Title;
        var newStatus = (CaseStatus)int.Parse(request.Status!);

        //>>> moving a case out of New counts as the first response
        if (entity.Status == CaseStatus.New && newStatus != CaseStatus.New)
        {
            entity.FirstRespondedAt ??= DateTime.Now;
        }
        entity.Status = newStatus;
        entity.Priority = (CasePriority)int.Parse(request.Priority!);
        entity.Channel = string.IsNullOrEmpty(request.Channel) ? null : (CaseChannel?)int.Parse(request.Channel);
        entity.CustomerId = request.CustomerId;
        entity.SalesRepresentativeId = request.SalesRepresentativeId;
        entity.DueDate = request.DueDate;
        entity.Description = request.Description;
        entity.Resolution = request.Resolution;
        if (entity.Status == CaseStatus.Resolved || entity.Status == CaseStatus.Closed)
        {
            entity.ResolvedAtUtc ??= DateTime.UtcNow;
        }
        else
        {
            entity.ResolvedAtUtc = null;
        }

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);


        return new UpdateSupportCaseResult
        {
            Data = entity
        };
    }
}
