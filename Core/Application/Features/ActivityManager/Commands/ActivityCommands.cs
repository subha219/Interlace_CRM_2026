using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.ActivityManager.Commands;

public class ActivityResult
{
    public Activity? Data { get; set; }
}

public class SaveActivityRequest : IRequest<ActivityResult>
{
    /// <summary>Empty to create, filled to update.</summary>
    public string? Id { get; init; }
    public string? Subject { get; init; }
    public string? Description { get; init; }
    public ActivityType? Type { get; init; }
    public string? Priority { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }
    public string? AssignedToUserId { get; init; }

    /// <summary>Filled by the controller from the signed-in user.</summary>
    public string? UserId { get; set; }
}

public class SaveActivityValidator : AbstractValidator<SaveActivityRequest>
{
    public SaveActivityValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Type).NotNull();
        RuleFor(x => x).Must(x => x.EndAt == null || x.StartAt == null || x.EndAt >= x.StartAt)
            .WithMessage("End must not be before the start.");
    }
}

public class SaveActivityHandler : IRequestHandler<SaveActivityRequest, ActivityResult>
{
    private readonly ICommandRepository<Activity> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numberSequenceService;

    public SaveActivityHandler(ICommandRepository<Activity> repository, IUnitOfWork unitOfWork, NumberSequenceService numberSequenceService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _numberSequenceService = numberSequenceService;
    }

    public async Task<ActivityResult> Handle(SaveActivityRequest request, CancellationToken cancellationToken)
    {
        Activity entity;
        var creating = string.IsNullOrEmpty(request.Id);

        if (creating)
        {
            entity = new Activity
            {
                CreatedById = request.UserId,
                Number = _numberSequenceService.GenerateNumber(nameof(Activity), "", "ACT"),
                Status = ActivityStatus.Open,
                OwnerUserId = request.UserId
            };
        }
        else
        {
            entity = await _repository.GetAsync(request.Id!, cancellationToken)
                ?? throw new Exception($"Activity not found: {request.Id}");
            entity.UpdatedById = request.UserId;
        }

        entity.Subject = request.Subject;
        entity.Description = request.Description;
        entity.Type = request.Type;
        entity.Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority;
        entity.StartAt = request.StartAt;
        entity.EndAt = request.EndAt;
        entity.RelatedType = string.IsNullOrWhiteSpace(request.RelatedType) ? null : request.RelatedType;
        entity.RelatedId = entity.RelatedType == null ? null : request.RelatedId;
        entity.RelatedName = entity.RelatedType == null ? null : request.RelatedName;
        entity.AssignedToUserId = string.IsNullOrWhiteSpace(request.AssignedToUserId) ? request.UserId : request.AssignedToUserId;

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else _repository.Update(entity);

        await _unitOfWork.SaveAsync(cancellationToken);
        return new ActivityResult { Data = entity };
    }
}

public class SetActivityStatusRequest : IRequest<ActivityResult>
{
    public string? Id { get; init; }
    public ActivityStatus? Status { get; init; }
    public string? Outcome { get; init; }
    public string? UserId { get; set; }
}

public class SetActivityStatusValidator : AbstractValidator<SetActivityStatusRequest>
{
    public SetActivityStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).NotNull();
    }
}

public class SetActivityStatusHandler : IRequestHandler<SetActivityStatusRequest, ActivityResult>
{
    private readonly ICommandRepository<Activity> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetActivityStatusHandler(ICommandRepository<Activity> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ActivityResult> Handle(SetActivityStatusRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id!, cancellationToken)
            ?? throw new Exception($"Activity not found: {request.Id}");

        entity.Status = request.Status;
        entity.CompletedAt = request.Status == ActivityStatus.Done ? DateTime.Now : null;
        if (!string.IsNullOrWhiteSpace(request.Outcome)) entity.Outcome = request.Outcome;
        entity.UpdatedById = request.UserId;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new ActivityResult { Data = entity };
    }
}

public class DeleteActivityRequest : IRequest<ActivityResult>
{
    public string? Id { get; init; }
    public string? UserId { get; set; }
}

public class DeleteActivityValidator : AbstractValidator<DeleteActivityRequest>
{
    public DeleteActivityValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteActivityHandler : IRequestHandler<DeleteActivityRequest, ActivityResult>
{
    private readonly ICommandRepository<Activity> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteActivityHandler(ICommandRepository<Activity> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ActivityResult> Handle(DeleteActivityRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id!, cancellationToken)
            ?? throw new Exception($"Activity not found: {request.Id}");

        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new ActivityResult { Data = entity };
    }
}
