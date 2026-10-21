using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SalesTargetManager;

// ---------- leaderboard ----------

public class LeaderboardRowDto
{
    public string? UserId { get; init; }
    public string? Name { get; set; }
    public double Won { get; init; }
    public int WonCount { get; init; }
    public int LostCount { get; init; }
    public double Target { get; init; }
    public int AttainmentPercent { get; init; }
    public double OpenPipeline { get; init; }
    public int OpenCount { get; init; }
    public int WinRatePercent { get; init; }
}

public class GetLeaderboardResult
{
    public string Label { get; init; } = "";
    public List<LeaderboardRowDto> Rows { get; init; } = new();
}

public class GetLeaderboardRequest : IRequest<GetLeaderboardResult>
{
    public int Year { get; init; }

    /// <summary>Month, Quarter or Year.</summary>
    public string? Period { get; init; }

    /// <summary>Month number (1-12) or quarter number (1-4); ignored for Year.</summary>
    public int Number { get; init; }
}

public class GetLeaderboardHandler : IRequestHandler<GetLeaderboardRequest, GetLeaderboardResult>
{
    private readonly IQueryContext _context;
    public GetLeaderboardHandler(IQueryContext context) { _context = context; }

    public static (DateTime from, DateTime to, int m1, int m2, string label) Range(int year, string? period, int number)
    {
        if (string.Equals(period, "Month", StringComparison.OrdinalIgnoreCase))
        {
            var m = Math.Clamp(number, 1, 12);
            var f = new DateTime(year, m, 1);
            return (f, f.AddMonths(1), m, m, f.ToString("MMMM yyyy"));
        }
        if (string.Equals(period, "Quarter", StringComparison.OrdinalIgnoreCase))
        {
            var q = Math.Clamp(number, 1, 4);
            var f = new DateTime(year, (q - 1) * 3 + 1, 1);
            return (f, f.AddMonths(3), (q - 1) * 3 + 1, q * 3, $"Q{q} {year}");
        }
        var y = new DateTime(year, 1, 1);
        return (y, y.AddYears(1), 1, 12, year.ToString());
    }

    public async Task<GetLeaderboardResult> Handle(GetLeaderboardRequest request, CancellationToken cancellationToken)
    {
        var year = request.Year < 2000 ? DateTime.Now.Year : request.Year;
        var (from, to, m1, m2, label) = Range(year, request.Period, request.Number);

        var closed = await _context.Lead.AsNoTracking()
            .Where(x => !x.IsDeleted && x.DateClosingActual >= from && x.DateClosingActual < to
                        && (x.ClosingStatus == ClosingStatus.ClosedWon || x.ClosingStatus == ClosingStatus.ClosedLost))
            .Select(x => new { x.OwnerUserId, x.ClosingStatus, x.AmountClosed })
            .ToListAsync(cancellationToken);

        var open = await _context.Lead.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ClosingStatus == ClosingStatus.OnProgress && x.ConvertedCustomerId == null)
            .Select(x => new { x.OwnerUserId, x.AmountTargeted })
            .ToListAsync(cancellationToken);

        var targets = await _context.SalesTarget.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Year == year && x.Month >= m1 && x.Month <= m2)
            .Select(x => new { x.UserId, x.Amount })
            .ToListAsync(cancellationToken);

        var users = closed.Select(x => x.OwnerUserId).Concat(open.Select(x => x.OwnerUserId)).Concat(targets.Select(x => x.UserId)).Distinct().ToList();

        var rows = users.Select(u =>
        {
            var won = closed.Where(x => x.OwnerUserId == u && x.ClosingStatus == ClosingStatus.ClosedWon).ToList();
            var lost = closed.Count(x => x.OwnerUserId == u && x.ClosingStatus == ClosingStatus.ClosedLost);
            var wonAmount = won.Sum(x => x.AmountClosed ?? 0);
            var target = targets.Where(x => x.UserId == u).Sum(x => x.Amount);
            var opens = open.Where(x => x.OwnerUserId == u).ToList();
            return new LeaderboardRowDto
            {
                UserId = u,
                Won = wonAmount,
                WonCount = won.Count,
                LostCount = lost,
                Target = target,
                AttainmentPercent = target > 0 ? (int)Math.Round(wonAmount / target * 100) : 0,
                OpenPipeline = opens.Sum(x => x.AmountTargeted ?? 0),
                OpenCount = opens.Count,
                WinRatePercent = won.Count + lost > 0 ? (int)Math.Round(100.0 * won.Count / (won.Count + lost)) : 0
            };
        }).OrderByDescending(x => x.Won).ThenByDescending(x => x.OpenPipeline).ToList();

        return new GetLeaderboardResult { Label = label, Rows = rows };
    }
}

// ---------- targets ----------

public class GetTargetsResult { public List<double> Months { get; init; } = new(); }

public class GetTargetsRequest : IRequest<GetTargetsResult>
{
    public string? UserId { get; init; }
    public int Year { get; init; }
}

public class GetTargetsHandler : IRequestHandler<GetTargetsRequest, GetTargetsResult>
{
    private readonly IQueryContext _context;
    public GetTargetsHandler(IQueryContext context) { _context = context; }

    public async Task<GetTargetsResult> Handle(GetTargetsRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.SalesTarget.AsNoTracking()
            .Where(x => !x.IsDeleted && x.UserId == request.UserId && x.Year == request.Year).ToListAsync(cancellationToken);
        return new GetTargetsResult { Months = Enumerable.Range(1, 12).Select(m => rows.Where(r => r.Month == m).Sum(r => r.Amount)).ToList() };
    }
}

public class SaveTargetsResult { public int Saved { get; init; } }

public class SaveTargetsRequest : IRequest<SaveTargetsResult>
{
    public string? UserId { get; init; }
    public int Year { get; init; }

    /// <summary>12 amounts, January first.</summary>
    public List<double>? Months { get; init; }
    public string? CurrentUserId { get; set; }
}

public class SaveTargetsValidator : AbstractValidator<SaveTargetsRequest>
{
    public SaveTargetsValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Months).NotNull().Must(m => m!.Count == 12).WithMessage("Provide 12 monthly amounts.");
        RuleForEach(x => x.Months).GreaterThanOrEqualTo(0);
    }
}

public class SaveTargetsHandler : IRequestHandler<SaveTargetsRequest, SaveTargetsResult>
{
    private readonly ICommandRepository<SalesTarget> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveTargetsHandler(ICommandRepository<SalesTarget> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SaveTargetsResult> Handle(SaveTargetsRequest request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetQuery()
            .Where(x => !x.IsDeleted && x.UserId == request.UserId && x.Year == request.Year).ToListAsync(cancellationToken);

        for (var m = 1; m <= 12; m++)
        {
            var amount = request.Months![m - 1];
            var row = existing.FirstOrDefault(x => x.Month == m);
            if (row == null)
            {
                if (amount == 0) continue;
                await _repository.CreateAsync(new SalesTarget { CreatedById = request.CurrentUserId, UserId = request.UserId, Year = request.Year, Month = m, Amount = amount }, cancellationToken);
            }
            else
            {
                row.Amount = amount;
                row.UpdatedById = request.CurrentUserId;
                _repository.Update(row);
            }
        }

        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveTargetsResult { Saved = 12 };
    }
}

// ---------- stage velocity ----------

public class StageVelocityDto
{
    public string Stage { get; init; } = "";
    public int Entered { get; init; }
    public double AverageDays { get; init; }
}

public class GetStageVelocityResult { public List<StageVelocityDto> Stages { get; init; } = new(); }

public class GetStageVelocityRequest : IRequest<GetStageVelocityResult> { }

/// <summary>Average days a lead stayed in each stage, from the stage history.</summary>
public class GetStageVelocityHandler : IRequestHandler<GetStageVelocityRequest, GetStageVelocityResult>
{
    private readonly IQueryContext _context;
    public GetStageVelocityHandler(IQueryContext context) { _context = context; }

    public async Task<GetStageVelocityResult> Handle(GetStageVelocityRequest request, CancellationToken cancellationToken)
    {
        var history = await _context.LeadStageHistory.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ToStage != null)
            .Select(x => new { x.LeadId, x.ToStage, x.ChangedAtUtc })
            .ToListAsync(cancellationToken);

        var spans = new Dictionary<PipelineStage, List<double>>();
        var entered = new Dictionary<PipelineStage, int>();

        foreach (var group in history.GroupBy(x => x.LeadId))
        {
            var ordered = group.OrderBy(x => x.ChangedAtUtc).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var stage = ordered[i].ToStage!.Value;
                entered[stage] = entered.GetValueOrDefault(stage) + 1;
                if (i + 1 < ordered.Count && ordered[i + 1].ToStage != stage)
                {
                    if (!spans.ContainsKey(stage)) spans[stage] = new List<double>();
                    spans[stage].Add((ordered[i + 1].ChangedAtUtc - ordered[i].ChangedAtUtc).TotalDays);
                }
            }
        }

        var list = Enum.GetValues<PipelineStage>().Where(s => s != PipelineStage.Closed).Select(s => new StageVelocityDto
        {
            Stage = s.ToString(),
            Entered = entered.GetValueOrDefault(s),
            AverageDays = spans.TryGetValue(s, out var v) && v.Count > 0 ? Math.Round(v.Average(), 1) : 0
        }).ToList();

        return new GetStageVelocityResult { Stages = list };
    }
}
