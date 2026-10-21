using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Application.Features.InsightsManager;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LeadManager.Queries;

public record PipelineCardDto
{
    public string? Id { get; init; }
    public string? Number { get; init; }
    public string? Title { get; init; }
    public string? CompanyName { get; init; }
    public string? SalesTeamName { get; init; }
    public double Amount { get; init; }
    public double Probability { get; init; }
    public DateTime? DateClosingEstimation { get; init; }
    public bool IsStale { get; init; }
    public int Score { get; init; }
    public string? HealthLabel { get; init; }
    public string? NextAction { get; init; }
    public List<HealthFactorDto> Factors { get; init; } = new();
}

public record PipelineColumnDto
{
    public string? Key { get; init; }
    public string? Title { get; init; }
    public double Probability { get; init; }
    public int Count { get; init; }
    public double TotalAmount { get; init; }
    public double WeightedAmount { get; init; }
    public List<PipelineCardDto> Cards { get; init; } = new();
}

public class GetPipelineBoardResult
{
    public List<PipelineColumnDto>? Data { get; init; }
}

public class GetPipelineBoardRequest : IRequest<GetPipelineBoardResult>
{
    public string? SalesTeamId { get; init; }
}

public class GetPipelineBoardHandler : IRequestHandler<GetPipelineBoardRequest, GetPipelineBoardResult>
{
    private readonly IQueryContext _context;
    private readonly DealHealthService _dealHealth;

    public GetPipelineBoardHandler(IQueryContext context, DealHealthService dealHealth)
    {
        _context = context;
        _dealHealth = dealHealth;
    }

    public async Task<GetPipelineBoardResult> Handle(GetPipelineBoardRequest request, CancellationToken cancellationToken)
    {
        var query = _context.Lead
            .AsNoTracking()
            .IsDeletedEqualTo()
            .Include(x => x.SalesTeam)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SalesTeamId))
        {
            query = query.Where(x => x.SalesTeamId == request.SalesTeamId);
        }

        var leads = await query.ToListAsync(cancellationToken);
        var now = DateTime.Now;
        var health = await _dealHealth.EvaluateAsync(leads, cancellationToken);
        var config = await PipelineConfig.LoadAsync(_context, cancellationToken);

        PipelineCardDto ToCard(Lead x) => new()
        {
            Id = x.Id,
            Number = x.Number,
            Title = x.Title,
            CompanyName = x.CompanyName,
            SalesTeamName = x.SalesTeam?.Name,
            Amount = x.ClosingStatus == ClosingStatus.ClosedWon ? (x.AmountClosed ?? x.AmountTargeted ?? 0) : (x.AmountTargeted ?? 0),
            Probability = config.For(x.PipelineStage, x.ClosingStatus),
            DateClosingEstimation = x.DateClosingEstimation,
            IsStale = x.ClosingStatus == ClosingStatus.OnProgress && x.DateClosingEstimation.HasValue && x.DateClosingEstimation.Value.Date < now.Date,
            Score = health[x.Id].Score,
            HealthLabel = health[x.Id].Label,
            NextAction = health[x.Id].NextAction,
            Factors = health[x.Id].Factors
        };

        PipelineColumnDto Column(string key, string title, double probability, IEnumerable<Lead> source)
        {
            var cards = source.Select(ToCard).OrderByDescending(c => c.Score).ToList();
            return new PipelineColumnDto
            {
                Key = key,
                Title = title,
                Probability = probability,
                Count = cards.Count,
                TotalAmount = cards.Sum(c => c.Amount),
                WeightedAmount = cards.Sum(c => c.Amount * c.Probability / 100),
                Cards = cards
            };
        }

        var open = leads.Where(x => x.ClosingStatus != ClosingStatus.ClosedWon && x.ClosingStatus != ClosingStatus.ClosedLost && x.PipelineStage != PipelineStage.Closed);
        var columns = new List<PipelineColumnDto>();

        foreach (var stage in new[]
        {
            PipelineStage.Prospecting, PipelineStage.Qualification, PipelineStage.NeedAnalysis,
            PipelineStage.Proposal, PipelineStage.Negotiation, PipelineStage.DecisionMaking
        })
        {
            columns.Add(Column(stage.ToString(), config.Label(stage), config.For(stage, ClosingStatus.OnProgress),
                open.Where(x => x.PipelineStage == stage)));
        }

        columns.Add(Column("Won", "Closed Won", 100, leads.Where(x => x.ClosingStatus == ClosingStatus.ClosedWon)));
        columns.Add(Column("Lost", "Closed Lost", 0, leads.Where(x => x.ClosingStatus == ClosingStatus.ClosedLost)));

        return new GetPipelineBoardResult { Data = columns };
    }
}
