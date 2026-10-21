using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Features.EmailManager.Commands;
using Application.Features.IntegrationManager;
using Application.Features.SupportCaseManager;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SupportInsightManager;

// ---------- SLA policy ----------

public record SlaPolicyDto(string Priority, int FirstResponseHours, int ResolutionHours, string? EscalationUserId);
public class GetSlaPoliciesResult { public List<SlaPolicyDto> Data { get; init; } = new(); }
public class GetSlaPoliciesRequest : IRequest<GetSlaPoliciesResult> { }

public class GetSlaPoliciesHandler : IRequestHandler<GetSlaPoliciesRequest, GetSlaPoliciesResult>
{
    private readonly IQueryContext _context;
    public GetSlaPoliciesHandler(IQueryContext context) { _context = context; }

    public async Task<GetSlaPoliciesResult> Handle(GetSlaPoliciesRequest request, CancellationToken cancellationToken)
    {
        var sla = await SlaConfig.LoadAsync(_context, cancellationToken);
        return new GetSlaPoliciesResult
        {
            Data = new[] { CasePriority.Critical, CasePriority.High, CasePriority.Normal, CasePriority.Low }
                .Select(p => new SlaPolicyDto(p.ToString(), sla.FirstResponseHours(p), sla.ResolutionHours(p), sla.EscalationUser(p))).ToList()
        };
    }
}

public class SlaPolicyInput
{
    public string? Priority { get; init; }
    public int FirstResponseHours { get; init; }
    public int ResolutionHours { get; init; }
    public string? EscalationUserId { get; init; }
}

public class SaveSlaPoliciesResult { public int Saved { get; init; } }

public class SaveSlaPoliciesRequest : IRequest<SaveSlaPoliciesResult>
{
    public List<SlaPolicyInput>? Policies { get; init; }
    public string? UserId { get; set; }
}

public class SaveSlaPoliciesValidator : AbstractValidator<SaveSlaPoliciesRequest>
{
    public SaveSlaPoliciesValidator()
    {
        RuleFor(x => x.Policies).NotEmpty();
        RuleForEach(x => x.Policies).ChildRules(p =>
        {
            p.RuleFor(x => x.Priority).Must(v => Enum.TryParse<CasePriority>(v, out _)).WithMessage("Unknown priority.");
            p.RuleFor(x => x.FirstResponseHours).InclusiveBetween(1, 720);
            p.RuleFor(x => x.ResolutionHours).InclusiveBetween(1, 8760);
        });
        RuleFor(x => x.Policies).Must(l => l == null || l.All(p => p.ResolutionHours >= p.FirstResponseHours))
            .WithMessage("The resolution target cannot be shorter than the first-response target.");
    }
}

public class SaveSlaPoliciesHandler : IRequestHandler<SaveSlaPoliciesRequest, SaveSlaPoliciesResult>
{
    private readonly ICommandRepository<SlaPolicy> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SaveSlaPoliciesHandler(ICommandRepository<SlaPolicy> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SaveSlaPoliciesResult> Handle(SaveSlaPoliciesRequest request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetQuery().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var p in request.Policies!)
        {
            var priority = Enum.Parse<CasePriority>(p.Priority!);
            var user = string.IsNullOrWhiteSpace(p.EscalationUserId) ? null : p.EscalationUserId;
            var row = existing.FirstOrDefault(x => x.Priority == priority);
            if (row == null)
            {
                await _repository.CreateAsync(new SlaPolicy { CreatedById = request.UserId, Priority = priority, FirstResponseHours = p.FirstResponseHours, ResolutionHours = p.ResolutionHours, EscalationUserId = user }, cancellationToken);
            }
            else
            {
                row.FirstResponseHours = p.FirstResponseHours;
                row.ResolutionHours = p.ResolutionHours;
                row.EscalationUserId = user;
                row.UpdatedById = request.UserId;
                _repository.Update(row);
            }
        }
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveSlaPoliciesResult { Saved = request.Policies.Count };
    }
}

// ---------- dashboard ----------

public record CountItem(string Label, int Count);
public record AgentLoad(string? UserId, string? Name, int Open, int Breached);

public class SupportDashboardResult
{
    public int Open { get; init; }
    public int Breached { get; init; }
    public int DueIn24Hours { get; init; }
    public int Unanswered { get; init; }
    public int ResolvedLast30Days { get; init; }
    public double AvgFirstResponseHours { get; init; }
    public double AvgResolutionHours { get; init; }
    public int SlaCompliancePercent { get; init; }
    public double CsatAverage { get; init; }
    public int CsatCount { get; init; }
    public List<CountItem> ByPriority { get; init; } = new();
    public List<CountItem> ByStatus { get; init; } = new();
    public List<AgentLoad> Agents { get; init; } = new();
}

public class GetSupportDashboardRequest : IRequest<SupportDashboardResult> { }

public class GetSupportDashboardHandler : IRequestHandler<GetSupportDashboardRequest, SupportDashboardResult>
{
    private readonly IQueryContext _context;
    public GetSupportDashboardHandler(IQueryContext context) { _context = context; }

    public async Task<SupportDashboardResult> Handle(GetSupportDashboardRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var cases = await _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted)
            .Select(x => new { x.Status, x.Priority, x.OwnerUserId, x.DueDate, x.CreatedAtUtc, x.ResolvedAtUtc, x.FirstRespondedAt, x.FirstResponseDue, x.CsatScore })
            .ToListAsync(cancellationToken);

        bool IsOpen(CaseStatus? s) => s != CaseStatus.Resolved && s != CaseStatus.Closed;
        var open = cases.Where(x => IsOpen(x.Status)).ToList();
        var breached = open.Count(x => x.DueDate != null && x.DueDate < now);

        var since = DateTime.UtcNow.AddDays(-30);
        var resolved = cases.Where(x => !IsOpen(x.Status) && x.ResolvedAtUtc != null && x.ResolvedAtUtc >= since).ToList();
        var hours = resolved.Where(x => x.CreatedAtUtc != null).Select(x => (x.ResolvedAtUtc!.Value - x.CreatedAtUtc!.Value).TotalHours).ToList();
        var withDue = resolved.Where(x => x.DueDate != null).ToList();
        var met = withDue.Count(x => x.ResolvedAtUtc!.Value.ToLocalTime() <= x.DueDate!.Value);

        var responseHours = cases.Where(x => x.FirstRespondedAt != null && x.CreatedAtUtc != null && x.CreatedAtUtc >= since)
            .Select(x => (x.FirstRespondedAt!.Value.ToUniversalTime() - x.CreatedAtUtc!.Value).TotalHours).Where(h => h >= 0).ToList();

        var scores = cases.Where(x => x.CsatScore != null).Select(x => x.CsatScore!.Value).ToList();

        return new SupportDashboardResult
        {
            Open = open.Count,
            Breached = breached,
            DueIn24Hours = open.Count(x => x.DueDate != null && x.DueDate >= now && x.DueDate <= now.AddHours(24)),
            Unanswered = open.Count(x => x.FirstRespondedAt == null && x.FirstResponseDue != null && x.FirstResponseDue < now),
            ResolvedLast30Days = resolved.Count,
            AvgFirstResponseHours = responseHours.Count == 0 ? 0 : Math.Round(responseHours.Average(), 1),
            AvgResolutionHours = hours.Count == 0 ? 0 : Math.Round(hours.Average(), 1),
            SlaCompliancePercent = withDue.Count == 0 ? 100 : (int)Math.Round(100.0 * met / withDue.Count),
            CsatAverage = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 2),
            CsatCount = scores.Count,
            ByPriority = new[] { CasePriority.Critical, CasePriority.High, CasePriority.Normal, CasePriority.Low }
                .Select(p => new CountItem(p.ToString(), open.Count(x => x.Priority == p))).ToList(),
            ByStatus = new[] { CaseStatus.New, CaseStatus.Open, CaseStatus.Pending }
                .Select(s => new CountItem(s.ToString(), open.Count(x => x.Status == s))).ToList(),
            Agents = open.GroupBy(x => x.OwnerUserId)
                .Select(g => new AgentLoad(g.Key, null, g.Count(), g.Count(x => x.DueDate != null && x.DueDate < now)))
                .OrderByDescending(a => a.Open).Take(15).ToList()
        };
    }
}

// ---------- CSAT ----------

public class GetCsatResult
{
    public string? Number { get; init; }
    public string? Title { get; init; }
    public int? Score { get; init; }
    public string? Comment { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public bool SurveySent { get; init; }
    public string? CustomerEmail { get; init; }
}

public class GetCsatRequest : IRequest<GetCsatResult> { public string? CaseId { get; init; } }

public class GetCsatHandler : IRequestHandler<GetCsatRequest, GetCsatResult>
{
    private readonly IQueryContext _context;
    public GetCsatHandler(IQueryContext context) { _context = context; }

    public async Task<GetCsatResult> Handle(GetCsatRequest request, CancellationToken cancellationToken)
    {
        var c = await _context.SupportCase.AsNoTracking().Include(x => x.Customer)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == request.CaseId, cancellationToken) ?? throw new Exception("Case not found.");
        return new GetCsatResult
        {
            Number = c.Number, Title = c.Title, Score = c.CsatScore, Comment = c.CsatComment, SubmittedAt = c.CsatSubmittedAt,
            SurveySent = !string.IsNullOrEmpty(c.CsatToken), CustomerEmail = c.Customer?.EmailAddress
        };
    }
}

public class RecordCsatResult { public bool Saved { get; init; } }

public class RecordCsatRequest : IRequest<RecordCsatResult>
{
    public string? CaseId { get; init; }
    public int Score { get; init; }
    public string? Comment { get; init; }
    public string? UserId { get; set; }
}

public class RecordCsatValidator : AbstractValidator<RecordCsatRequest>
{
    public RecordCsatValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

/// <summary>A staff member records the score a customer gave by phone or in person.</summary>
public class RecordCsatHandler : IRequestHandler<RecordCsatRequest, RecordCsatResult>
{
    private readonly ICommandRepository<SupportCase> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public RecordCsatHandler(ICommandRepository<SupportCase> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<RecordCsatResult> Handle(RecordCsatRequest request, CancellationToken cancellationToken)
    {
        var c = await _repository.GetAsync(request.CaseId!, cancellationToken) ?? throw new Exception("Case not found.");
        c.CsatScore = request.Score;
        c.CsatComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        c.CsatSubmittedAt = DateTime.Now;
        c.UpdatedById = request.UserId;
        _repository.Update(c);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new RecordCsatResult { Saved = true };
    }
}

public class SendCsatSurveyResult { public bool Sent { get; init; } public string? Error { get; init; } public string? Link { get; init; } }

public class SendCsatSurveyRequest : IRequest<SendCsatSurveyResult>
{
    public string? CaseId { get; init; }

    /// <summary>Address of this site, filled by the controller, used to build the survey link.</summary>
    public string? BaseUrl { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class SendCsatSurveyHandler : IRequestHandler<SendCsatSurveyRequest, SendCsatSurveyResult>
{
    private readonly ICommandRepository<SupportCase> _repository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;

    public SendCsatSurveyHandler(ICommandRepository<SupportCase> repository, IQueryContext context, IUnitOfWork unitOfWork, ISender sender)
    {
        _repository = repository;
        _context = context;
        _unitOfWork = unitOfWork;
        _sender = sender;
    }

    public async Task<SendCsatSurveyResult> Handle(SendCsatSurveyRequest request, CancellationToken cancellationToken)
    {
        var c = await _repository.GetAsync(request.CaseId ?? "", cancellationToken) ?? throw new Exception("Case not found.");
        var customer = await _context.Customer.AsNoTracking().Where(x => x.Id == c.CustomerId).Select(x => new { x.Name, x.EmailAddress }).FirstOrDefaultAsync(cancellationToken);
        if (customer == null || string.IsNullOrWhiteSpace(customer.EmailAddress))
            return new SendCsatSurveyResult { Sent = false, Error = "The customer has no e-mail address." };
        if (c.CsatSubmittedAt != null)
            return new SendCsatSurveyResult { Sent = false, Error = "The customer already answered the survey." };

        c.CsatToken ??= IntegrationRules.NewSecret(24);
        c.UpdatedById = request.UserId;
        _repository.Update(c);
        await _unitOfWork.SaveAsync(cancellationToken);

        var link = $"{(request.BaseUrl ?? "").TrimEnd('/')}/Accounts/Csat?token={Uri.EscapeDataString(c.CsatToken)}";
        var result = await _sender.Send(new SendCrmEmailRequest
        {
            To = customer.EmailAddress,
            Subject = $"How did we do? Case {c.Number}",
            Body = $"Hello {customer.Name},\n\nYour support case \"{c.Title}\" ({c.Number}) has been handled. It would help us to hear how it went. It takes 10 seconds:\n\n{link}\n\nThank you!",
            RelatedType = "SupportCase",
            RelatedId = c.Id,
            RelatedName = c.Title,
            UserId = request.UserId,
            UserName = request.UserName
        }, cancellationToken);

        return new SendCsatSurveyResult { Sent = result.Success, Error = result.Error, Link = link };
    }
}

public class GetCsatSurveyResult { public string? Number { get; init; } public string? Title { get; init; } public bool AlreadyAnswered { get; init; } }
public class GetCsatSurveyRequest : IRequest<GetCsatSurveyResult> { public string? Token { get; init; } }

public class GetCsatSurveyHandler : IRequestHandler<GetCsatSurveyRequest, GetCsatSurveyResult>
{
    private readonly IQueryContext _context;
    public GetCsatSurveyHandler(IQueryContext context) { _context = context; }

    public async Task<GetCsatSurveyResult> Handle(GetCsatSurveyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) throw new UnauthorizedAccessException("Invalid link.");
        var c = await _context.SupportCase.AsNoTracking().Where(x => !x.IsDeleted && x.CsatToken == request.Token)
            .Select(x => new { x.Number, x.Title, x.CsatSubmittedAt }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid link.");
        return new GetCsatSurveyResult { Number = c.Number, Title = c.Title, AlreadyAnswered = c.CsatSubmittedAt != null };
    }
}

public class SubmitCsatResult { public bool Saved { get; init; } }

public class SubmitCsatRequest : IRequest<SubmitCsatResult>
{
    public string? Token { get; init; }
    public int Score { get; init; }
    public string? Comment { get; init; }
}

public class SubmitCsatValidator : AbstractValidator<SubmitCsatRequest>
{
    public SubmitCsatValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public class SubmitCsatHandler : IRequestHandler<SubmitCsatRequest, SubmitCsatResult>
{
    private readonly ICommandRepository<SupportCase> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public SubmitCsatHandler(ICommandRepository<SupportCase> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<SubmitCsatResult> Handle(SubmitCsatRequest request, CancellationToken cancellationToken)
    {
        var token = request.Token!;
        var c = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.CsatToken == token, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid link.");
        if (c.CsatSubmittedAt != null) throw new Exception("This survey was already answered.");

        c.CsatScore = request.Score;
        c.CsatComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        c.CsatSubmittedAt = DateTime.Now;
        _repository.Update(c);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SubmitCsatResult { Saved = true };
    }
}
