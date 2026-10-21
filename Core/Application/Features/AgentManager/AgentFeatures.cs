using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.ActivityManager.Commands;
using Application.Features.AiManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AgentManager;

// ---------- list agents ----------

public class AgentDto
{
    public string? Key { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool UsesAi { get; init; }
    public string? RequiredRole { get; init; }
    public bool Enabled { get; init; }
    public bool AutoRun { get; init; }
    public int MaxPerRun { get; init; }
    public int Pending { get; init; }
    public DateTime? LastRunAtUtc { get; init; }
    public int LastProposed { get; init; }
    public string? LastError { get; init; }
}

public class GetAgentsResult { public List<AgentDto> Data { get; init; } = new(); public bool ModelConfigured { get; init; } }
public class GetAgentsRequest : IRequest<GetAgentsResult> { }

public class GetAgentsHandler : IRequestHandler<GetAgentsRequest, GetAgentsResult>
{
    private readonly IQueryContext _context;
    private readonly AiGovernor _governor;
    public GetAgentsHandler(IQueryContext context, AiGovernor governor) { _context = context; _governor = governor; }

    public async Task<GetAgentsResult> Handle(GetAgentsRequest request, CancellationToken cancellationToken)
    {
        var settings = await _context.AgentSetting.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        var pending = await _context.AgentProposal.AsNoTracking().Where(x => !x.IsDeleted && x.Status == "Pending").GroupBy(x => x.AgentKey).Select(g => new { g.Key, N = g.Count() }).ToListAsync(cancellationToken);
        var runs = await _context.AgentRun.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.StartedAtUtc).Take(100).ToListAsync(cancellationToken);
        return new GetAgentsResult
        {
            ModelConfigured = _governor.IsConfigured,
            Data = AgentRules.Catalog.Select(a =>
            {
                var s = settings.FirstOrDefault(x => x.AgentKey == a.Key);
                var last = runs.FirstOrDefault(x => x.AgentKey == a.Key);
                return new AgentDto
                {
                    Key = a.Key, Name = a.Name, Description = a.Description, UsesAi = a.UsesAi, RequiredRole = a.RequiredRole,
                    Enabled = s?.Enabled ?? false, AutoRun = s?.AutoRun ?? false, MaxPerRun = s?.MaxPerRun ?? 20, Pending = pending.FirstOrDefault(p => p.Key == a.Key)?.N ?? 0,
                    LastRunAtUtc = last?.StartedAtUtc, LastProposed = last?.Proposed ?? 0, LastError = last?.Error
                };
            }).ToList()
        };
    }
}

// ---------- switch an agent on or off ----------

public class SaveAgentSettingRequest : IRequest<bool>
{
    public string? AgentKey { get; init; }
    public bool Enabled { get; init; }
    public bool AutoRun { get; init; }
    public int MaxPerRun { get; init; } = 20;
    public string? UserId { get; set; }
}

public class SaveAgentSettingValidator : AbstractValidator<SaveAgentSettingRequest>
{
    public SaveAgentSettingValidator()
    {
        RuleFor(x => x.AgentKey).Must(k => AgentRules.Find(k) != null).WithMessage("Unknown agent.");
        RuleFor(x => x.MaxPerRun).InclusiveBetween(1, 100).WithMessage("Proposals per run: 1 to 100.");
    }
}

public class SaveAgentSettingHandler : IRequestHandler<SaveAgentSettingRequest, bool>
{
    private readonly ICommandRepository<AgentSetting> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveAgentSettingHandler(ICommandRepository<AgentSetting> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(SaveAgentSettingRequest request, CancellationToken cancellationToken)
    {
        var row = await _repository.GetQuery().Where(x => !x.IsDeleted && x.AgentKey == request.AgentKey).FirstOrDefaultAsync(cancellationToken);
        if (row == null) await _repository.CreateAsync(new AgentSetting { AgentKey = request.AgentKey, Enabled = request.Enabled, AutoRun = request.AutoRun && request.Enabled, MaxPerRun = request.MaxPerRun, CreatedById = request.UserId }, cancellationToken);
        else { row.Enabled = request.Enabled; row.AutoRun = request.AutoRun && request.Enabled; row.MaxPerRun = request.MaxPerRun; row.UpdatedById = request.UserId; _repository.Update(row); }
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}

// ---------- run an agent ----------

public class RunAgentResult { public int Scanned { get; init; } public int Proposed { get; init; } public bool UsedAi { get; init; } public string? Notice { get; init; } }

public class RunAgentRequest : IRequest<RunAgentResult>
{
    public string? AgentKey { get; init; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class RunAgentValidator : AbstractValidator<RunAgentRequest>
{
    public RunAgentValidator() { RuleFor(x => x.AgentKey).Must(k => AgentRules.Find(k) != null).WithMessage("Unknown agent."); }
}

/// <summary>
/// Looks for work and writes proposals. It never changes a record. A record that already has a pending proposal,
/// a recently rejected one, or an open task for the same purpose is skipped, so running twice does not repeat itself.
/// </summary>
public class RunAgentHandler : IRequestHandler<RunAgentRequest, RunAgentResult>
{
    private readonly IQueryContext _context;
    private readonly ICommandRepository<AgentProposal> _proposals;
    private readonly ICommandRepository<AgentRun> _runs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AiGovernor _governor;

    public RunAgentHandler(IQueryContext context, ICommandRepository<AgentProposal> proposals, ICommandRepository<AgentRun> runs, IUnitOfWork unitOfWork, AiGovernor governor)
    { _context = context; _proposals = proposals; _runs = runs; _unitOfWork = unitOfWork; _governor = governor; }

    private sealed record Candidate(string RelatedType, string RelatedId, string RelatedName, string Kind, string Title, string Reason, string Payload, string Source);

    public async Task<RunAgentResult> Handle(RunAgentRequest request, CancellationToken cancellationToken)
    {
        var def = AgentRules.Find(request.AgentKey)!;
        var setting = await _context.AgentSetting.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.AgentKey == def.Key, cancellationToken);
        if (setting is not { Enabled: true }) throw new Exception("Switch this agent on first.");
        var max = Math.Clamp(setting.MaxPerRun, 1, 100);
        var now = DateTime.UtcNow;

        var run = new AgentRun { AgentKey = def.Key, RunByUserId = request.UserId, RunByName = request.UserName, StartedAtUtc = now, CreatedById = request.UserId };
        await _runs.CreateAsync(run, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        var candidates = new List<Candidate>();
        var scanned = 0; var usedAi = false; string? error = null; string? notice = null;
        try
        {
            switch (def.Key)
            {
                case AgentRules.LeadFollowUp:
                    var lf = await LeadFollowUp(candidates, now, max, request.UserId, cancellationToken);
                    scanned = lf.Scanned; usedAi = lf.UsedAi; notice = lf.Notice; break;
                case AgentRules.ContractRenewal: scanned = await ContractRenewal(candidates, now, max, cancellationToken); break;
                case AgentRules.DataQuality: scanned = await DataQuality(candidates, max, cancellationToken); break;
                case AgentRules.SupportTriage:
                    var t = await SupportTriage(candidates, max, request.UserId, run.Id, cancellationToken);
                    scanned = t.Scanned; usedAi = t.UsedAi; notice = t.Notice; break;
            }

            // skip anything that already has a pending, recently rejected or recently approved proposal
            var keys = candidates.Select(c => AgentRules.DedupKey(def.Key, c.RelatedType, c.RelatedId, c.Kind)).ToList();
            var rejectedSince = now.AddDays(-AgentRules.RejectedQuietDays);
            var approvedSince = now.AddDays(-AgentRules.ApprovedQuietDays);
            var blocked = (await _context.AgentProposal.AsNoTracking().Where(x => !x.IsDeleted && x.DedupKey != null && keys.Contains(x.DedupKey) &&
                    (x.Status == "Pending" || (x.Status == "Rejected" && x.DecidedAtUtc > rejectedSince) || (x.Status == "Approved" && x.DecidedAtUtc > approvedSince)))
                .Select(x => x.DedupKey).ToListAsync(cancellationToken)).ToHashSet();

            var proposed = 0;
            foreach (var c in candidates)
            {
                var key = AgentRules.DedupKey(def.Key, c.RelatedType, c.RelatedId, c.Kind);
                if (blocked.Contains(key) || proposed >= max) continue;
                await _proposals.CreateAsync(new AgentProposal
                {
                    AgentKey = def.Key, RunId = run.Id, RelatedType = c.RelatedType, RelatedId = c.RelatedId, RelatedName = AgentRules.Clip(c.RelatedName, 255),
                    Kind = c.Kind, Title = AgentRules.Clip(c.Title, 255), Reason = AgentRules.Clip(c.Reason, 600), Payload = c.Payload, RequiredRole = def.RequiredRole,
                    Source = c.Source, Status = "Pending", DedupKey = key, CreatedById = request.UserId
                }, cancellationToken);
                proposed++;
            }
            run.Proposed = proposed;
        }
        catch (Exception ex) { error = ex.GetType().Name; }

        run.Scanned = scanned; run.UsedAi = usedAi; run.Error = error; run.FinishedAtUtc = DateTime.UtcNow; run.UpdatedById = request.UserId;
        _runs.Update(run);
        await _unitOfWork.SaveAsync(cancellationToken);
        if (error != null) throw new Exception("The agent stopped with an error (" + error + "). Nothing was changed.");
        return new RunAgentResult { Scanned = scanned, Proposed = run.Proposed, UsedAi = usedAi, Notice = notice };
    }

    private async Task<HashSet<string>> OpenTaskTargets(string relatedType, List<string> ids, CancellationToken ct) =>
        (await _context.Activity.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedType == relatedType && x.RelatedId != null && ids.Contains(x.RelatedId) && x.Status == ActivityStatus.Open)
            .Select(x => x.RelatedId!).Distinct().ToListAsync(ct)).ToHashSet();

    private async Task<(int Scanned, bool UsedAi, string? Notice)> LeadFollowUp(List<Candidate> output, DateTime now, int max, string? userId, CancellationToken ct)
    {
        var leads = await _context.Lead.AsNoTracking().Where(x => !x.IsDeleted && x.ClosingStatus == ClosingStatus.OnProgress && x.ConvertedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc).Take(300).Select(x => new { x.Id, x.Title, x.CompanyName, x.CreatedAtUtc, x.DateClosingEstimation }).ToListAsync(ct);
        var ids = leads.Select(l => l.Id).ToList();
        var laTouch = await _context.LeadActivity.AsNoTracking().Where(x => !x.IsDeleted && x.LeadId != null && ids.Contains(x.LeadId)).GroupBy(x => x.LeadId!).Select(g => new { Id = g.Key, At = g.Max(x => x.CreatedAtUtc) }).ToListAsync(ct);
        var acTouch = await _context.Activity.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedType == "Lead" && x.RelatedId != null && ids.Contains(x.RelatedId)).GroupBy(x => x.RelatedId!).Select(g => new { Id = g.Key, At = g.Max(x => x.CreatedAtUtc) }).ToListAsync(ct);
        var open = await OpenTaskTargets("Lead", ids, ct);
        var useAi = _governor.IsConfigured; var usedAi = false; string? notice = null; var aiCalls = 0;
        foreach (var l in leads)
        {
            if (open.Contains(l.Id) || output.Count >= max * 2) continue;
            var touches = laTouch.Where(t => t.Id == l.Id).Select(t => t.At).Concat(acTouch.Where(t => t.Id == l.Id).Select(t => t.At)).Where(d => d != null).ToList();
            var last = touches.Count == 0 ? (DateTime?)null : touches.Max();
            var reason = AgentRules.StaleLeadReason(l.CreatedAtUtc ?? now, last, now);
            if (reason == null) continue;
            var name = string.IsNullOrWhiteSpace(l.CompanyName) ? l.Title ?? "lead" : $"{l.Title} ({l.CompanyName})";

            //>>> a message to copy and edit; the AI model writes it when one is set up, a short template otherwise
            string? draft = null; var source = "rules";
            if (useAi && aiCalls < max)
            {
                aiCalls++;
                var facts = AiRules.Wrap(new[] { "Lead: " + AgentRules.Clip(l.Title, 200), "Company: " + AgentRules.Clip(l.CompanyName, 200), "Situation: " + reason });
                var system = "You write one short follow-up message from a sales representative to a prospect, in plain text, at most 60 words, friendly and professional. " +
                             "Do not invent facts, prices, dates or promises, and do not include links. Answer with the message only. " + AiRules.UntrustedNote;
                var run = await _governor.RunAsync(userId, "AgentLeadDraft", "Lead", l.Id, system, facts, 200, ct);
                if (run.Text != null) { draft = AgentRules.CleanDraft(run.Text); if (draft != null) { source = "ai"; usedAi = true; } }
                else if (notice == null) { notice = run.Error; useAi = false; }
            }
            draft ??= AgentRules.LeadDraft(l.Title, l.CompanyName);
            output.Add(new Candidate("Lead", l.Id, name, "Task", $"Follow up: {l.Title}", reason, AgentRules.TaskPayload(1, AgentRules.LeadPriority(l.DateClosingEstimation, now), draft), source));
        }
        return (leads.Count, usedAi, notice == null ? null : "The AI model was not used for the drafts (" + notice + "); short templates were used.");
    }

    private async Task<int> ContractRenewal(List<Candidate> output, DateTime now, int max, CancellationToken ct)
    {
        var horizon = now.Date.AddDays(120);
        var contracts = await _context.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.Status == "Active" && !x.AutoRenew && x.EndDate != null && x.EndDate <= horizon && x.EndDate >= now.Date)
            .OrderBy(x => x.EndDate).Take(300).Select(x => new { x.Id, x.Number, x.Title, x.EndDate, x.RenewalNoticeDays, x.Value }).ToListAsync(ct);
        var ids = contracts.Select(c => c.Id).ToList();
        var renewed = (await _context.Contract.AsNoTracking().Where(x => !x.IsDeleted && x.RenewedFromId != null && ids.Contains(x.RenewedFromId)).Select(x => x.RenewedFromId!).ToListAsync(ct)).ToHashSet();
        var open = await OpenTaskTargets("Contract", ids, ct);
        foreach (var c in contracts)
        {
            if (renewed.Contains(c.Id) || open.Contains(c.Id)) continue;
            var left = AgentRules.RenewalDaysLeft(c.EndDate, c.RenewalNoticeDays, now);
            if (left == null) continue;
            output.Add(new Candidate("Contract", c.Id, $"{c.Number} {c.Title}".Trim(), "Task", $"Renewal: {c.Number} {c.Title}".Trim(),
                $"Ends in {left} day(s) on {c.EndDate:dd MMM yyyy}; no renewal exists." + (c.Value != null ? $" Value {c.Value:N2}." : ""),
                AgentRules.TaskPayload(Math.Min(left.Value, 3), left <= 30 ? "High" : "Normal", AgentRules.RenewalDraft(c.Number, c.Title, c.EndDate)), "rules"));
        }
        return contracts.Count;
    }

    private async Task<int> DataQuality(List<Candidate> output, int max, CancellationToken ct)
    {
        var customers = await _context.Customer.AsNoTracking().Where(x => !x.IsDeleted && (x.EmailAddress == null || x.EmailAddress == "" || x.PhoneNumber == null || x.PhoneNumber == ""))
            .OrderBy(x => x.CreatedAtUtc).Take(200).Select(x => new { x.Id, x.Name, x.EmailAddress, x.PhoneNumber }).ToListAsync(ct);
        var leads = await _context.Lead.AsNoTracking().Where(x => !x.IsDeleted && x.ClosingStatus == ClosingStatus.OnProgress && x.ConvertedAtUtc == null &&
                (x.CompanyEmail == null || x.CompanyEmail == "" || x.CompanyPhoneNumber == null || x.CompanyPhoneNumber == ""))
            .OrderBy(x => x.CreatedAtUtc).Take(200).Select(x => new { x.Id, x.Title, x.CompanyEmail, x.CompanyPhoneNumber }).ToListAsync(ct);
        var openC = await OpenTaskTargets("Customer", customers.Select(c => c.Id).ToList(), ct);
        var openL = await OpenTaskTargets("Lead", leads.Select(c => c.Id).ToList(), ct);
        foreach (var c in customers)
        {
            if (openC.Contains(c.Id)) continue;
            var gaps = AgentRules.ContactGaps(c.EmailAddress, c.PhoneNumber);
            if (gaps.Count > 0) output.Add(new Candidate("Customer", c.Id, c.Name ?? "customer", "Task", $"Complete details: {c.Name}", "Missing " + string.Join(" and ", gaps) + ".", AgentRules.TaskPayload(3, "Normal"), "rules"));
        }
        foreach (var l in leads)
        {
            if (openL.Contains(l.Id)) continue;
            var gaps = AgentRules.ContactGaps(l.CompanyEmail, l.CompanyPhoneNumber);
            if (gaps.Count > 0) output.Add(new Candidate("Lead", l.Id, l.Title ?? "lead", "Task", $"Complete details: {l.Title}", "Missing " + string.Join(" and ", gaps) + ".", AgentRules.TaskPayload(3, "Normal"), "rules"));
        }
        return customers.Count + leads.Count;
    }

    private async Task<(int Scanned, bool UsedAi, string? Notice)> SupportTriage(List<Candidate> output, int max, string? userId, string runId, CancellationToken ct)
    {
        var cases = await _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.Status == CaseStatus.New)
            .OrderByDescending(x => x.CreatedAtUtc).Take(max).Select(x => new { x.Id, x.Number, x.Title, x.Description, x.Priority, x.CategoryId }).ToListAsync(ct);
        var cats = (await _context.SupportCategory.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive && x.Name != null).OrderBy(x => x.Name).Take(40)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct)).Select(x => (Id: x.Id, Name: x.Name!)).ToList();
        var useAi = _governor.IsConfigured;
        var usedAi = false; string? notice = null;
        foreach (var c in cases)
        {
            var current = c.Priority ?? CasePriority.Normal;
            var wantsCategory = string.IsNullOrEmpty(c.CategoryId) && cats.Count > 0;
            AgentRules.Triage? result = null; var source = "rules";
            if (useAi)
            {
                var lines = new List<string> { $"Case {c.Number}: {AgentRules.Clip(c.Title, 200)}", "Description: " + AgentRules.Clip(c.Description, 800).Replace("\n", " ") };
                if (wantsCategory) lines.Add("Categories: " + string.Join(", ", cats.Select(x => x.Name)));
                var facts = AiRules.Wrap(lines);
                var system = "You triage one support case. Choose its priority from the facts only. Answer with a single JSON object and nothing else: " +
                             "{\"priority\":\"Low|Normal|High|Critical\",\"reason\":\"one short sentence\",\"category\":\"exactly one of the listed categories, or an empty string\"}. " +
                             "Critical means a full outage, data loss or a security problem; High means the customer is blocked; " +
                             "Normal is the default; Low is a question or suggestion. " + AiRules.UntrustedNote;
                var run = await _governor.RunAsync(userId, "AgentTriage", "SupportCase", c.Id, system, facts, 150, ct);
                if (run.Text != null) { result = AgentRules.ParseTriage(run.Text); if (result != null) { source = "ai"; usedAi = true; } }
                else if (notice == null) { notice = run.Error; useAi = false; }
            }
            result ??= AgentRules.RuleTriage(c.Title, c.Description);
            if (result.Priority != current)
                output.Add(new Candidate("SupportCase", c.Id, $"{c.Number} {c.Title}".Trim(), "SetPriority", $"Set {c.Number} to {result.Priority}", result.Reason + $" Now {current}.",
                    AgentRules.PriorityPayload((int)current, (int)result.Priority), source));

            if (!wantsCategory) continue;
            (string Id, string Name)? cat = null; var catSource = "rules";
            if (!string.IsNullOrWhiteSpace(result.Category))
            {
                var m = cats.FirstOrDefault(x => string.Equals(x.Name, result.Category.Trim(), StringComparison.OrdinalIgnoreCase));
                if (m.Id != null) { cat = m; catSource = "ai"; }
            }
            if (cat == null) cat = AgentRules.MatchCategory(c.Title, c.Description, cats);
            if (cat != null)
                output.Add(new Candidate("SupportCase", c.Id, $"{c.Number} {c.Title}".Trim(), "SetCategory", $"Put {c.Number} in {cat.Value.Name}",
                    catSource == "ai" ? "The AI model picked this category from the text." : $"The text mentions \"{cat.Value.Name}\".",
                    AgentRules.CategoryPayload(cat.Value.Id, cat.Value.Name), catSource));
        }
        return (cases.Count, usedAi, notice == null ? null : "The AI model was not used (" + notice + "); keyword rules were applied.");
    }
}

// ---------- proposals ----------

public class AgentProposalDto
{
    public string? Id { get; init; }
    public string? AgentKey { get; init; }
    public string? RelatedType { get; init; }
    public string? RelatedId { get; init; }
    public string? RelatedName { get; init; }
    public string? Kind { get; init; }
    public string? Title { get; init; }
    public string? Reason { get; init; }
    public string? Source { get; init; }
    public string? Status { get; init; }
    public string? RequiredRole { get; init; }
    public int? DueInDays { get; init; }
    public string? Priority { get; init; }
    public string? Draft { get; init; }
    public string? DecidedByName { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? Result { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
}

public class GetProposalsResult { public List<AgentProposalDto> Data { get; init; } = new(); }
public class GetProposalsRequest : IRequest<GetProposalsResult> { public string? Status { get; init; } public string? AgentKey { get; init; } }

public class GetProposalsHandler : IRequestHandler<GetProposalsRequest, GetProposalsResult>
{
    private readonly IQueryContext _context;
    public GetProposalsHandler(IQueryContext context) { _context = context; }

    public async Task<GetProposalsResult> Handle(GetProposalsRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status is "Pending" or "Approved" or "Rejected" or "Failed" ? request.Status : "Pending";
        var q = _context.AgentProposal.AsNoTracking().Where(x => !x.IsDeleted && x.Status == status);
        if (!string.IsNullOrEmpty(request.AgentKey)) q = q.Where(x => x.AgentKey == request.AgentKey);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
        return new GetProposalsResult
        {
            Data = rows.Select(x =>
            {
                var task = x.Kind == "Task" ? AgentRules.ParseTaskPayload(x.Payload) : null;
                return new AgentProposalDto
                {
                    Id = x.Id, AgentKey = x.AgentKey, RelatedType = x.RelatedType, RelatedId = x.RelatedId, RelatedName = x.RelatedName, Kind = x.Kind, Title = x.Title, Reason = x.Reason,
                    Source = x.Source, Status = x.Status, RequiredRole = x.RequiredRole, DueInDays = task?.DueInDays, Priority = task?.Priority, Draft = task == null ? null : AgentRules.ParseDraft(x.Payload), DecidedByName = x.DecidedByName,
                    DecidedAtUtc = x.DecidedAtUtc, Result = x.Result, CreatedAtUtc = x.CreatedAtUtc
                };
            }).ToList()
        };
    }
}

public class GetRunsResult { public List<AgentRun> Data { get; init; } = new(); }
public class GetRunsRequest : IRequest<GetRunsResult> { }

public class GetRunsHandler : IRequestHandler<GetRunsRequest, GetRunsResult>
{
    private readonly IQueryContext _context;
    public GetRunsHandler(IQueryContext context) { _context = context; }
    public async Task<GetRunsResult> Handle(GetRunsRequest request, CancellationToken cancellationToken) =>
        new() { Data = await _context.AgentRun.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.StartedAtUtc).Take(20).ToListAsync(cancellationToken) };
}

// ---------- approve or reject ----------

public class DecideProposalResult { public string? Status { get; init; } public string? Message { get; init; } }

public class DecideProposalRequest : IRequest<DecideProposalResult>
{
    public string? Id { get; init; }
    public bool Approve { get; init; }

    /// <summary>The approver may adjust a task before approving it.</summary>
    public string? Title { get; init; }
    public int? DueInDays { get; init; }
    public string? Priority { get; init; }
    public string? Draft { get; init; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public List<string> UserRoles { get; set; } = new();
}

public class DecideProposalValidator : AbstractValidator<DecideProposalRequest>
{
    public DecideProposalValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Draft).MaximumLength(AgentRules.DraftMaxChars);
        RuleFor(x => x.DueInDays).InclusiveBetween(0, 365).When(x => x.DueInDays != null);
        RuleFor(x => x.Priority).Must(p => p is "Low" or "Normal" or "High").When(x => !string.IsNullOrEmpty(x.Priority)).WithMessage("Choose Low, Normal or High.");
    }
}

/// <summary>The only place an agent's suggestion turns into a change, and only for a person who holds the role for that kind of change.</summary>
public class DecideProposalHandler : IRequestHandler<DecideProposalRequest, DecideProposalResult>
{
    private readonly ICommandRepository<AgentProposal> _proposals;
    private readonly ICommandRepository<SupportCase> _cases;
    private readonly ICommandRepository<SupportCaseComment> _comments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;
    private readonly IQueryContext _context;

    public DecideProposalHandler(ICommandRepository<AgentProposal> proposals, ICommandRepository<SupportCase> cases, ICommandRepository<SupportCaseComment> comments, IUnitOfWork unitOfWork, ISender sender, IQueryContext context)
    { _proposals = proposals; _cases = cases; _comments = comments; _unitOfWork = unitOfWork; _sender = sender; _context = context; }

    public async Task<DecideProposalResult> Handle(DecideProposalRequest request, CancellationToken cancellationToken)
    {
        var p = await _proposals.GetAsync(request.Id!, cancellationToken) ?? throw new Exception("Proposal not found.");
        if (p.Status != "Pending") throw new Exception("This proposal was already decided.");
        var agent = AgentRules.Find(p.AgentKey)?.Name ?? "AI";

        p.DecidedByUserId = request.UserId; p.DecidedByName = request.UserName; p.DecidedAtUtc = DateTime.UtcNow; p.UpdatedById = request.UserId;
        if (!request.Approve)
        {
            p.Status = "Rejected"; p.Result = "Rejected";
            _proposals.Update(p);
            await _unitOfWork.SaveAsync(cancellationToken);
            return new DecideProposalResult { Status = "Rejected", Message = "Rejected. This agent will not suggest it again for a month." };
        }

        if (!string.IsNullOrEmpty(p.RequiredRole) && !request.UserRoles.Contains(p.RequiredRole, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Your role does not include {p.RequiredRole}, which this change needs.");

        string message;
        try
        {
            message = p.Kind switch
            {
                "Task" => await CreateTask(p, request, agent, cancellationToken),
                "SetPriority" => await SetPriority(p, request, agent, cancellationToken),
                "SetCategory" => await SetCategory(p, request, agent, cancellationToken),
                _ => throw new Exception("Unknown kind of proposal.")
            };
            p.Status = "Approved"; p.Result = AgentRules.Clip(message, 300);
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            p.Status = "Failed"; p.Result = AgentRules.Clip(ex.Message, 300);
            _proposals.Update(p);
            await _unitOfWork.SaveAsync(cancellationToken);
            throw new Exception(ex.Message);
        }
        _proposals.Update(p);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new DecideProposalResult { Status = "Approved", Message = message };
    }

    private async Task<string> CreateTask(AgentProposal p, DecideProposalRequest r, string agent, CancellationToken ct)
    {
        var payload = AgentRules.ParseTaskPayload(p.Payload) ?? throw new Exception("The proposal is damaged.");
        var days = r.DueInDays ?? payload.DueInDays;
        var priority = string.IsNullOrEmpty(r.Priority) ? payload.Priority : r.Priority;
        var subject = string.IsNullOrWhiteSpace(r.Title) ? p.Title : r.Title.Trim();
        var draft = r.Draft != null ? AgentRules.Clip(r.Draft.Trim(), AgentRules.DraftMaxChars) : AgentRules.ParseDraft(p.Payload);
        var result = await _sender.Send(new SaveActivityRequest
        {
            Subject = subject, Description = $"Suggested by the {agent} agent: {p.Reason} Approved by {r.UserName}." + (string.IsNullOrWhiteSpace(draft) ? "" : "\n\nSuggested message (check it before sending):\n" + draft), Type = ActivityType.Task, Priority = priority,
            StartAt = DateTime.Today.AddDays(days).AddHours(17), RelatedType = p.RelatedType, RelatedId = p.RelatedId, RelatedName = p.RelatedName,
            AssignedToUserId = r.UserId, UserId = r.UserId
        }, ct);
        return $"Task {result.Data?.Number} created.";
    }

    private async Task<string> SetCategory(AgentProposal p, DecideProposalRequest r, string agent, CancellationToken ct)
    {
        var change = AgentRules.ParseCategoryPayload(p.Payload) ?? throw new Exception("The proposal is damaged.");
        var c = await _cases.GetAsync(p.RelatedId ?? "", ct) ?? throw new Exception("The case no longer exists.");
        if (!string.IsNullOrEmpty(c.CategoryId)) throw new Exception("The case already has a category. Run the agent again.");
        var cat = await _context.SupportCategory.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.IsActive && x.Id == change.Id, ct)
            ?? throw new Exception("The category no longer exists or is switched off.");
        c.CategoryId = cat.Id; c.UpdatedById = r.UserId;
        _cases.Update(c);
        await _comments.CreateAsync(new SupportCaseComment
        {
            SupportCaseId = c.Id, Comment = $"Category set to {cat.Name} after approval by {r.UserName} ({agent} agent: {p.Reason})",
            VisibleToCustomer = false, FromCustomer = false, CreatedById = r.UserId
        }, ct);
        return $"Category set to {cat.Name}.";
    }

    private async Task<string> SetPriority(AgentProposal p, DecideProposalRequest r, string agent, CancellationToken ct)
    {
        var change = AgentRules.ParsePriorityPayload(p.Payload) ?? throw new Exception("The proposal is damaged.");
        var c = await _cases.GetAsync(p.RelatedId ?? "", ct) ?? throw new Exception("The case no longer exists.");
        if ((int)(c.Priority ?? CasePriority.Normal) != change.From) throw new Exception("The case priority changed since this was suggested. Run the agent again.");
        c.Priority = (CasePriority)change.To; c.UpdatedById = r.UserId;
        _cases.Update(c);
        await _comments.CreateAsync(new SupportCaseComment
        {
            SupportCaseId = c.Id, Comment = $"Priority changed from {(CasePriority)change.From} to {(CasePriority)change.To} after approval by {r.UserName} ({agent} agent: {p.Reason})",
            VisibleToCustomer = false, FromCustomer = false, CreatedById = r.UserId
        }, ct);
        return $"Priority set to {(CasePriority)change.To}.";
    }
}
