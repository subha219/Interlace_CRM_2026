using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.WorkflowManager;

public record WorkflowRuleDto(string? Id, string? Name, string? TriggerEvent, string? ConditionsJson, string? ActionsJson, bool IsActive, int RunCount, DateTime? LastRunAtUtc);

public class ListWorkflowRulesResult
{
    public List<WorkflowRuleDto> Data { get; init; } = new();
    public string[] Events { get; init; } = WorkflowModel.Events;
}

public class ListWorkflowRulesRequest : IRequest<ListWorkflowRulesResult> { }

public class ListWorkflowRulesHandler : IRequestHandler<ListWorkflowRulesRequest, ListWorkflowRulesResult>
{
    private readonly IQueryContext _context;
    public ListWorkflowRulesHandler(IQueryContext context) { _context = context; }

    public async Task<ListWorkflowRulesResult> Handle(ListWorkflowRulesRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.WorkflowRule.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new ListWorkflowRulesResult { Data = rows.Select(x => new WorkflowRuleDto(x.Id, x.Name, x.TriggerEvent, x.ConditionsJson, x.ActionsJson, x.IsActive, x.RunCount, x.LastRunAtUtc)).ToList() };
    }
}

public class SaveWorkflowRuleResult { public string? Id { get; init; } }

public class SaveWorkflowRuleRequest : IRequest<SaveWorkflowRuleResult>
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? TriggerEvent { get; init; }
    public string? ConditionsJson { get; init; }
    public string? ActionsJson { get; init; }
    public bool IsActive { get; init; } = true;
    public string? UserId { get; set; }
}

public class SaveWorkflowRuleValidator : AbstractValidator<SaveWorkflowRuleRequest>
{
    public SaveWorkflowRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x).Custom((x, ctx) =>
        {
            var error = WorkflowModel.Validate(x.TriggerEvent, x.ConditionsJson, x.ActionsJson);
            if (error != null) ctx.AddFailure(error);
        });
    }
}

public class SaveWorkflowRuleHandler : IRequestHandler<SaveWorkflowRuleRequest, SaveWorkflowRuleResult>
{
    private readonly ICommandRepository<WorkflowRule> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveWorkflowRuleHandler(ICommandRepository<WorkflowRule> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveWorkflowRuleResult> Handle(SaveWorkflowRuleRequest request, CancellationToken cancellationToken)
    {
        WorkflowRule? entity = null;
        if (!string.IsNullOrEmpty(request.Id))
        {
            entity = await _repository.GetAsync(request.Id, cancellationToken) ?? throw new Exception("Rule not found.");
        }
        var creating = entity == null;
        entity ??= new WorkflowRule { CreatedById = request.UserId };

        entity.Name = request.Name!.Trim();
        entity.TriggerEvent = request.TriggerEvent;
        entity.ConditionsJson = string.IsNullOrWhiteSpace(request.ConditionsJson) ? "[]" : request.ConditionsJson;
        entity.ActionsJson = request.ActionsJson;
        entity.IsActive = request.IsActive;

        if (creating) await _repository.CreateAsync(entity, cancellationToken);
        else { entity.UpdatedById = request.UserId; _repository.Update(entity); }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveWorkflowRuleResult { Id = entity.Id };
    }
}

public class DeleteWorkflowRuleResult { public bool Deleted { get; init; } }
public class DeleteWorkflowRuleRequest : IRequest<DeleteWorkflowRuleResult> { public string? Id { get; init; } public string? UserId { get; set; } }

public class DeleteWorkflowRuleHandler : IRequestHandler<DeleteWorkflowRuleRequest, DeleteWorkflowRuleResult>
{
    private readonly ICommandRepository<WorkflowRule> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteWorkflowRuleHandler(ICommandRepository<WorkflowRule> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<DeleteWorkflowRuleResult> Handle(DeleteWorkflowRuleRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetAsync(request.Id ?? "", cancellationToken) ?? throw new Exception("Rule not found.");
        entity.UpdatedById = request.UserId;
        _repository.Delete(entity);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DeleteWorkflowRuleResult { Deleted = true };
    }
}

public record WorkflowRunDto(string? Id, string? RuleName, string? EventName, string? RecordLabel, bool Success, string? Detail, DateTime RunAtUtc);
public class ListWorkflowRunsResult { public List<WorkflowRunDto> Data { get; init; } = new(); }
public class ListWorkflowRunsRequest : IRequest<ListWorkflowRunsResult> { public string? RuleId { get; init; } }

public class ListWorkflowRunsHandler : IRequestHandler<ListWorkflowRunsRequest, ListWorkflowRunsResult>
{
    private readonly IQueryContext _context;
    public ListWorkflowRunsHandler(IQueryContext context) { _context = context; }

    public async Task<ListWorkflowRunsResult> Handle(ListWorkflowRunsRequest request, CancellationToken cancellationToken)
    {
        var q = _context.WorkflowRun.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(request.RuleId)) q = q.Where(x => x.RuleId == request.RuleId);
        var rows = await q.OrderByDescending(x => x.RunAtUtc).Take(100).ToListAsync(cancellationToken);
        return new ListWorkflowRunsResult { Data = rows.Select(x => new WorkflowRunDto(x.Id, x.RuleName, x.EventName, x.RecordLabel, x.Success, x.Detail, x.RunAtUtc)).ToList() };
    }
}
