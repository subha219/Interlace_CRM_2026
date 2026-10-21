using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.LeadManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.PipelineSettingManager;

public record PipelineStageDto(string Stage, string DefaultLabel, string Label, double Probability, double DefaultProbability);
public class GetPipelineStagesResult { public List<PipelineStageDto> Data { get; init; } = new(); }
public class GetPipelineStagesRequest : IRequest<GetPipelineStagesResult> { }

public class GetPipelineStagesHandler : IRequestHandler<GetPipelineStagesRequest, GetPipelineStagesResult>
{
    private readonly IQueryContext _context;
    public GetPipelineStagesHandler(IQueryContext context) { _context = context; }

    public async Task<GetPipelineStagesResult> Handle(GetPipelineStagesRequest request, CancellationToken cancellationToken)
    {
        var config = await PipelineConfig.LoadAsync(_context, cancellationToken);
        return new GetPipelineStagesResult
        {
            Data = PipelineConfig.OpenStages.Select(s => new PipelineStageDto(
                s.ToString(), s.ToFriendlyName(), config.Label(s), config.For(s, ClosingStatus.OnProgress), PipelineProbability.For(s, ClosingStatus.OnProgress))).ToList()
        };
    }
}

public class StageInput
{
    public string? Stage { get; init; }
    public string? Label { get; init; }
    public double Probability { get; init; }
}

public class SavePipelineStagesResult { public int Saved { get; init; } }

public class SavePipelineStagesRequest : IRequest<SavePipelineStagesResult>
{
    public List<StageInput>? Stages { get; init; }
    public string? UserId { get; set; }
}

public class SavePipelineStagesValidator : AbstractValidator<SavePipelineStagesRequest>
{
    public SavePipelineStagesValidator()
    {
        RuleFor(x => x.Stages).NotEmpty();
        RuleForEach(x => x.Stages).ChildRules(s =>
        {
            s.RuleFor(x => x.Stage).Must(v => Enum.TryParse<PipelineStage>(v, out var e) && e != PipelineStage.Closed).WithMessage("Unknown stage.");
            s.RuleFor(x => x.Label).MaximumLength(100);
            s.RuleFor(x => x.Probability).InclusiveBetween(0, 100);
        });
    }
}

public class SavePipelineStagesHandler : IRequestHandler<SavePipelineStagesRequest, SavePipelineStagesResult>
{
    private readonly ICommandRepository<PipelineStageSetting> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SavePipelineStagesHandler(ICommandRepository<PipelineStageSetting> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SavePipelineStagesResult> Handle(SavePipelineStagesRequest request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetQuery().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var input in request.Stages!)
        {
            var stage = Enum.Parse<PipelineStage>(input.Stage!);
            var label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim();
            var row = existing.FirstOrDefault(x => x.Stage == stage);
            if (row == null)
            {
                await _repository.CreateAsync(new PipelineStageSetting { CreatedById = request.UserId, Stage = stage, Label = label, Probability = input.Probability }, cancellationToken);
            }
            else
            {
                row.Label = label;
                row.Probability = input.Probability;
                row.UpdatedById = request.UserId;
                _repository.Update(row);
            }
        }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SavePipelineStagesResult { Saved = request.Stages.Count };
    }
}

public class ResetPipelineStagesResult { public bool Reset { get; init; } }
public class ResetPipelineStagesRequest : IRequest<ResetPipelineStagesResult> { public string? UserId { get; set; } }

public class ResetPipelineStagesHandler : IRequestHandler<ResetPipelineStagesRequest, ResetPipelineStagesResult>
{
    private readonly ICommandRepository<PipelineStageSetting> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public ResetPipelineStagesHandler(ICommandRepository<PipelineStageSetting> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<ResetPipelineStagesResult> Handle(ResetPipelineStagesRequest request, CancellationToken cancellationToken)
    {
        foreach (var row in await _repository.GetQuery().Where(x => !x.IsDeleted).ToListAsync(cancellationToken))
        {
            row.UpdatedById = request.UserId;
            _repository.Delete(row);
        }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new ResetPipelineStagesResult { Reset = true };
    }
}
