using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.ActivityManager.Commands;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AiManager;

public static class AiEntities
{
    public static readonly string[] All = { "Customer", "Lead", "SupportCase" };
    public static string Url(string entity) => entity switch { "Customer" => "/Customers/CustomerList", "Lead" => "/Pipelines/PipelineBoard", _ => "/SupportCases/SupportCaseList" };
}

// ---------- summary ----------

public class AiTextResult
{
    public string? Text { get; init; }

    /// <summary>"ai" when the model wrote it, "facts" when only the plain facts are shown.</summary>
    public string Mode { get; init; } = "facts";
    public string? Notice { get; init; }
    public List<AiSource> Sources { get; init; } = new();
    public string? CustomerEmail { get; init; }
}

public class SummarizeRecordRequest : IRequest<AiTextResult>
{
    public string? Entity { get; init; }
    public string? Id { get; init; }
    public string? UserId { get; set; }
}

public class SummarizeRecordValidator : AbstractValidator<SummarizeRecordRequest>
{
    public SummarizeRecordValidator()
    {
        RuleFor(x => x.Entity).Must(e => AiEntities.All.Contains(e ?? "")).WithMessage("Unknown record type.");
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class SummarizeRecordHandler : IRequestHandler<SummarizeRecordRequest, AiTextResult>
{
    private readonly IQueryContext _context;
    private readonly AiGovernor _governor;
    public SummarizeRecordHandler(IQueryContext context, AiGovernor governor) { _context = context; _governor = governor; }

    public async Task<AiTextResult> Handle(SummarizeRecordRequest request, CancellationToken cancellationToken)
    {
        var ctx = await AiContextBuilder.BuildAsync(_context, request.Entity!, request.Id!, cancellationToken) ?? throw new KeyNotFoundException("Record not found.");
        var facts = string.Join("\n", ctx.Lines);

        var system = "You summarise one CRM record for a busy colleague. Use ONLY the facts provided. Write at most 120 words as short lines starting with '- ': " +
                     "where things stand, what is at risk or overdue, and the single best next step. Quote record numbers. Say so when information is missing. Never invent facts. " + AiRules.UntrustedNote;
        var run = await _governor.RunAsync(request.UserId, "Summary", request.Entity, request.Id, system, AiRules.Wrap(ctx.Lines), 500, cancellationToken);
        return run.Text != null
            ? new AiTextResult { Text = run.Text, Mode = "ai", Sources = ctx.Sources, CustomerEmail = ctx.CustomerEmail }
            : new AiTextResult { Text = facts, Mode = "facts", Notice = run.Error, Sources = ctx.Sources, CustomerEmail = ctx.CustomerEmail };
    }
}

// ---------- reply draft ----------

public class DraftCaseReplyRequest : IRequest<AiTextResult>
{
    public string? CaseId { get; init; }
    public string? Tone { get; init; }
    public string? UserId { get; set; }
}

public class DraftCaseReplyValidator : AbstractValidator<DraftCaseReplyRequest>
{
    public DraftCaseReplyValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Tone).Must(t => string.IsNullOrEmpty(t) || t == "Friendly" || t == "Formal" || t == "Brief").WithMessage("Choose Friendly, Formal or Brief.");
    }
}

/// <summary>Only drafts text. Nothing is sent: the agent reads, edits and sends it with "Reply to customer".</summary>
public class DraftCaseReplyHandler : IRequestHandler<DraftCaseReplyRequest, AiTextResult>
{
    private readonly IQueryContext _context;
    private readonly AiGovernor _governor;
    public DraftCaseReplyHandler(IQueryContext context, AiGovernor governor) { _context = context; _governor = governor; }

    public async Task<AiTextResult> Handle(DraftCaseReplyRequest request, CancellationToken cancellationToken)
    {
        var ctx = await AiContextBuilder.BuildAsync(_context, "SupportCase", request.CaseId!, cancellationToken) ?? throw new KeyNotFoundException("Case not found.");
        var tone = string.IsNullOrEmpty(request.Tone) ? "Friendly" : request.Tone;
        var system = $"You draft a reply from a support agent to the customer about the case described in the facts. Tone: {tone}. Write only the message body (no subject, no signature placeholder). " +
                     "Answer what the customer asked using ONLY the facts. If the facts do not contain the answer, say what will be checked and when you will come back, without promising anything specific. " +
                     "Never quote internal notes, never mention SLA or internal systems. " + AiRules.UntrustedNote;
        var run = await _governor.RunAsync(request.UserId, "CaseReply", "SupportCase", request.CaseId, system, AiRules.Wrap(ctx.Lines), 500, cancellationToken);
        return new AiTextResult { Text = run.Text, Mode = run.Text != null ? "ai" : "facts", Notice = run.Error, Sources = ctx.Sources, CustomerEmail = ctx.CustomerEmail };
    }
}

// ---------- copilot action: propose, then confirm ----------

public class ProposeActionResult
{
    public AiProposal? Proposal { get; init; }
    public string? Notice { get; init; }
    public List<AiSource> Sources { get; init; } = new();
    public string? RecordName { get; init; }
}

public class ProposeActionRequest : IRequest<ProposeActionResult>
{
    public string? Entity { get; init; }
    public string? Id { get; init; }
    public string? Instruction { get; init; }
    public string? UserId { get; set; }
}

public class ProposeActionValidator : AbstractValidator<ProposeActionRequest>
{
    public ProposeActionValidator()
    {
        RuleFor(x => x.Entity).Must(e => AiEntities.All.Contains(e ?? "")).WithMessage("Unknown record type.");
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Instruction).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Turns "remind me to call them next week" into a proposal. It changes nothing until the person confirms.</summary>
public class ProposeActionHandler : IRequestHandler<ProposeActionRequest, ProposeActionResult>
{
    private readonly IQueryContext _context;
    private readonly AiGovernor _governor;
    public ProposeActionHandler(IQueryContext context, AiGovernor governor) { _context = context; _governor = governor; }

    public async Task<ProposeActionResult> Handle(ProposeActionRequest request, CancellationToken cancellationToken)
    {
        var ctx = await AiContextBuilder.BuildAsync(_context, request.Entity!, request.Id!, cancellationToken) ?? throw new KeyNotFoundException("Record not found.");
        var system = "You turn the user's instruction into ONE follow-up task for the record in the facts. Answer with a single JSON object and nothing else: " +
                     "{\"type\":\"task\",\"subject\":\"short imperative title\",\"dueInDays\":number 0-365,\"priority\":\"Low|Normal|High\"}. " +
                     "If the instruction asks for anything other than a follow-up task, answer {\"type\":\"none\"}. " + AiRules.UntrustedNote;
        var user = $"Instruction from the user: {request.Instruction!.Trim()}\n\nRecord facts:\n{AiRules.Wrap(ctx.Lines)}";
        var run = await _governor.RunAsync(request.UserId, "ProposeAction", request.Entity, request.Id, system, user, 250, cancellationToken);
        if (run.Text == null) return new ProposeActionResult { Notice = run.Error, Sources = ctx.Sources, RecordName = ctx.Title };

        var proposal = AiRules.ParseProposal(run.Text);
        return proposal == null
            ? new ProposeActionResult { Notice = "I could only prepare follow-up tasks. Try something like \"remind me to call them on Friday\".", Sources = ctx.Sources, RecordName = ctx.Title }
            : new ProposeActionResult { Proposal = proposal, Sources = ctx.Sources, RecordName = ctx.Title };
    }
}

public class ExecuteActionResult { public string? Number { get; init; } }

public class ExecuteActionRequest : IRequest<ExecuteActionResult>
{
    public string? Entity { get; init; }
    public string? Id { get; init; }
    public string? Subject { get; init; }
    public int DueInDays { get; init; }
    public string? Priority { get; init; }
    public string? UserId { get; set; }
}

public class ExecuteActionValidator : AbstractValidator<ExecuteActionRequest>
{
    public ExecuteActionValidator()
    {
        RuleFor(x => x.Entity).Must(e => AiEntities.All.Contains(e ?? "")).WithMessage("Unknown record type.");
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DueInDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Priority).Must(p => p == "Low" || p == "Normal" || p == "High").WithMessage("Choose Low, Normal or High.");
    }
}

/// <summary>Runs after the person confirmed (and possibly edited) the proposal. Uses the normal activity command, so audit and permissions apply as always.</summary>
public class ExecuteActionHandler : IRequestHandler<ExecuteActionRequest, ExecuteActionResult>
{
    private readonly IQueryContext _context;
    private readonly ISender _sender;
    public ExecuteActionHandler(IQueryContext context, ISender sender) { _context = context; _sender = sender; }

    public async Task<ExecuteActionResult> Handle(ExecuteActionRequest request, CancellationToken cancellationToken)
    {
        var ctx = await AiContextBuilder.BuildAsync(_context, request.Entity!, request.Id!, cancellationToken) ?? throw new KeyNotFoundException("Record not found.");
        var due = DateTime.Today.AddDays(request.DueInDays).AddHours(17);
        var result = await _sender.Send(new SaveActivityRequest
        {
            Subject = request.Subject!.Trim(),
            Description = "Created by the AI assistant after confirmation.",
            Type = ActivityType.Task,
            Priority = request.Priority,
            StartAt = due,
            RelatedType = request.Entity,
            RelatedId = request.Id,
            RelatedName = ctx.Title,
            AssignedToUserId = request.UserId,
            UserId = request.UserId
        }, cancellationToken);
        return new ExecuteActionResult { Number = result.Data?.Number };
    }
}

// ---------- governance ----------

public class AiSettingsDto
{
    public bool Enabled { get; init; }
    public int DailyLimitPerUser { get; init; }
    public int MonthlyLimitTotal { get; init; }
    public bool ModelConfigured { get; init; }
}

public class AiUsageRow
{
    public DateTime? At { get; init; }
    public string? UserId { get; init; }
    public string? Feature { get; init; }
    public string? RelatedType { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int DurationMs { get; init; }
    public int Chars { get; init; }
}

public record AiUserCount(string? UserId, int Count);

public class GetAiGovernanceResult
{
    public AiSettingsDto Settings { get; init; } = new();
    public int UsedToday { get; init; }
    public int UsedThisMonth { get; init; }
    public int FailedThisMonth { get; init; }
    public List<AiUserCount> ByUser { get; init; } = new();
    public List<AiUsageRow> Recent { get; init; } = new();
}

public class GetAiGovernanceRequest : IRequest<GetAiGovernanceResult> { }

public class GetAiGovernanceHandler : IRequestHandler<GetAiGovernanceRequest, GetAiGovernanceResult>
{
    private readonly IQueryContext _context;
    private readonly AiGovernor _governor;
    public GetAiGovernanceHandler(IQueryContext context, AiGovernor governor) { _context = context; _governor = governor; }

    public async Task<GetAiGovernanceResult> Handle(GetAiGovernanceRequest request, CancellationToken cancellationToken)
    {
        var s = await _governor.SettingsAsync(cancellationToken);
        var dayStart = DateTime.UtcNow.Date;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var month = await _context.AiUsageLog.AsNoTracking().Where(x => x.CreatedAtUtc >= monthStart)
            .Select(x => new { x.UserId, x.Success, x.CreatedAtUtc }).ToListAsync(cancellationToken);
        var recent = await _context.AiUsageLog.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(50).ToListAsync(cancellationToken);
        return new GetAiGovernanceResult
        {
            Settings = new AiSettingsDto { Enabled = s.Enabled, DailyLimitPerUser = s.DailyLimitPerUser, MonthlyLimitTotal = s.MonthlyLimitTotal, ModelConfigured = _governor.IsConfigured },
            UsedToday = month.Count(x => x.CreatedAtUtc >= dayStart),
            UsedThisMonth = month.Count,
            FailedThisMonth = month.Count(x => !x.Success),
            ByUser = month.GroupBy(x => x.UserId).Select(g => new AiUserCount(g.Key, g.Count())).OrderByDescending(x => x.Count).Take(20).ToList(),
            Recent = recent.Select(x => new AiUsageRow { At = x.CreatedAtUtc, UserId = x.UserId, Feature = x.Feature, RelatedType = x.RelatedType, Success = x.Success, Error = x.Error, DurationMs = x.DurationMs, Chars = x.InputChars + x.OutputChars }).ToList()
        };
    }
}

public class SaveAiSettingsRequest : IRequest<bool>
{
    public bool Enabled { get; init; }
    public int DailyLimitPerUser { get; init; }
    public int MonthlyLimitTotal { get; init; }
    public string? UserId { get; set; }
}

public class SaveAiSettingsValidator : AbstractValidator<SaveAiSettingsRequest>
{
    public SaveAiSettingsValidator()
    {
        RuleFor(x => x.DailyLimitPerUser).InclusiveBetween(0, 10000).WithMessage("Daily limit: 0 (no limit) to 10000.");
        RuleFor(x => x.MonthlyLimitTotal).InclusiveBetween(0, 1000000).WithMessage("Monthly limit: 0 (no limit) to 1000000.");
    }
}

public class SaveAiSettingsHandler : IRequestHandler<SaveAiSettingsRequest, bool>
{
    private readonly ICommandRepository<AiSetting> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveAiSettingsHandler(ICommandRepository<AiSetting> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(SaveAiSettingsRequest request, CancellationToken cancellationToken)
    {
        var row = await _repository.GetQuery().Where(x => !x.IsDeleted).FirstOrDefaultAsync(cancellationToken);
        if (row == null)
        {
            await _repository.CreateAsync(new AiSetting { CreatedById = request.UserId, Enabled = request.Enabled, DailyLimitPerUser = request.DailyLimitPerUser, MonthlyLimitTotal = request.MonthlyLimitTotal }, cancellationToken);
        }
        else
        {
            row.Enabled = request.Enabled;
            row.DailyLimitPerUser = request.DailyLimitPerUser;
            row.MonthlyLimitTotal = request.MonthlyLimitTotal;
            row.UpdatedById = request.UserId;
            _repository.Update(row);
        }
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}
