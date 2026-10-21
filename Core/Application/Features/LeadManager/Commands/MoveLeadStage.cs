using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;

namespace Application.Features.LeadManager.Commands;

public class MoveLeadStageResult
{
    public Lead? Data { get; set; }
}

public class MoveLeadStageRequest : IRequest<MoveLeadStageResult>
{
    public string? LeadId { get; init; }

    /// <summary>
    /// Board column: a PipelineStage name (Prospecting ... DecisionMaking), "Won" or "Lost".
    /// </summary>
    public string? Target { get; init; }
    public string? UpdatedById { get; init; }
}

public class MoveLeadStageValidator : AbstractValidator<MoveLeadStageRequest>
{
    public MoveLeadStageValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty();
        RuleFor(x => x.Target).NotEmpty();
    }
}

public class MoveLeadStageHandler : IRequestHandler<MoveLeadStageRequest, MoveLeadStageResult>
{
    private readonly ICommandRepository<Lead> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveLeadStageHandler(ICommandRepository<Lead> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MoveLeadStageResult> Handle(MoveLeadStageRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.LeadId ?? string.Empty, cancellationToken);

        if (entity == null)
        {
            throw new Exception($"Entity not found: {request.LeadId}");
        }

        var target = request.Target ?? string.Empty;

        if (string.Equals(target, "Won", StringComparison.OrdinalIgnoreCase))
        {
            entity.PipelineStage = PipelineStage.Closed;
            entity.ClosingStatus = ClosingStatus.ClosedWon;
            entity.DateClosingActual ??= DateTime.Now;
            entity.AmountClosed ??= entity.AmountTargeted;
        }
        else if (string.Equals(target, "Lost", StringComparison.OrdinalIgnoreCase))
        {
            entity.PipelineStage = PipelineStage.Closed;
            entity.ClosingStatus = ClosingStatus.ClosedLost;
            entity.DateClosingActual ??= DateTime.Now;
            entity.AmountClosed = 0;
        }
        else if (Enum.TryParse<PipelineStage>(target, true, out var stage) && stage != PipelineStage.Closed)
        {
            //>>> re-opening a closed lead clears the closing result
            entity.PipelineStage = stage;
            entity.ClosingStatus = ClosingStatus.OnProgress;
            entity.DateClosingActual = null;
            entity.AmountClosed = null;
        }
        else
        {
            throw new Exception($"Unknown pipeline column: {request.Target}");
        }

        entity.UpdatedById = request.UpdatedById;

        _repository.Update(entity);
        await _unitOfWork.SaveAsync(cancellationToken);

        return new MoveLeadStageResult
        {
            Data = entity
        };
    }
}
