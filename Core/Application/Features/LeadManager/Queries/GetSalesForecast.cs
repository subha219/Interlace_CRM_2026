using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.LeadManager.Queries;

public record ForecastBucketDto
{
    public string? Key { get; init; }
    public string? Name { get; init; }
    public int Count { get; init; }
    public double Amount { get; init; }
    public double Weighted { get; init; }
}

public record ForecastTeamDto
{
    public string? Name { get; init; }
    public int OpenCount { get; init; }
    public double OpenAmount { get; init; }
    public double Weighted { get; init; }
    public int WonCount { get; init; }
    public double WonAmount { get; init; }
    public int LostCount { get; init; }
    public double WinRate { get; init; }
}

public class GetSalesForecastResult
{
    public double OpenAmount { get; init; }
    public double WeightedAmount { get; init; }
    public double WonAmount { get; init; }
    public int WonCount { get; init; }
    public int LostCount { get; init; }
    public int OpenCount { get; init; }
    public double WinRate { get; init; }
    public double AverageDealSize { get; init; }
    public List<ForecastBucketDto> ByStage { get; init; } = new();
    public List<ForecastBucketDto> ByMonth { get; init; } = new();
    public List<ForecastTeamDto> ByTeam { get; init; } = new();
}

public class GetSalesForecastRequest : IRequest<GetSalesForecastResult>
{
    public int MonthsAhead { get; init; } = 6;
}

public class GetSalesForecastHandler : IRequestHandler<GetSalesForecastRequest, GetSalesForecastResult>
{
    private readonly IQueryContext _context;

    public GetSalesForecastHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetSalesForecastResult> Handle(GetSalesForecastRequest request, CancellationToken cancellationToken)
    {
        var leads = await _context.Lead
            .AsNoTracking()
            .IsDeletedEqualTo()
            .Include(x => x.SalesTeam)
            .ToListAsync(cancellationToken);

        var config = await PipelineConfig.LoadAsync(_context, cancellationToken);

        bool IsWon(Domain.Entities.Lead x) => x.ClosingStatus == ClosingStatus.ClosedWon;
        bool IsLost(Domain.Entities.Lead x) => x.ClosingStatus == ClosingStatus.ClosedLost;
        bool IsOpen(Domain.Entities.Lead x) => !IsWon(x) && !IsLost(x) && x.PipelineStage != PipelineStage.Closed;
        double Amt(Domain.Entities.Lead x) => x.AmountTargeted ?? 0;
        double Wtd(Domain.Entities.Lead x) => Amt(x) * config.For(x.PipelineStage, x.ClosingStatus) / 100;

        var open = leads.Where(IsOpen).ToList();
        var won = leads.Where(IsWon).ToList();
        var lost = leads.Where(IsLost).ToList();

        var byStage = open
            .GroupBy(x => x.PipelineStage ?? PipelineStage.Prospecting)
            .OrderBy(g => (int)g.Key)
            .Select(g => new ForecastBucketDto
            {
                Key = g.Key.ToString(),
                Name = g.Key.ToFriendlyName(),
                Count = g.Count(),
                Amount = g.Sum(Amt),
                Weighted = g.Sum(Wtd)
            }).ToList();

        var start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var months = Math.Clamp(request.MonthsAhead, 1, 24);
        var byMonth = new List<ForecastBucketDto>();
        for (int i = 0; i < months; i++)
        {
            var m = start.AddMonths(i);
            var inMonth = open.Where(x => x.DateClosingEstimation.HasValue
                && x.DateClosingEstimation.Value.Year == m.Year
                && x.DateClosingEstimation.Value.Month == m.Month).ToList();
            //>>> overdue estimates are counted in the current month
            if (i == 0)
            {
                inMonth.AddRange(open.Where(x => x.DateClosingEstimation.HasValue && x.DateClosingEstimation.Value < start));
            }
            byMonth.Add(new ForecastBucketDto
            {
                Key = m.ToString("yyyy-MM"),
                Name = m.ToString("MMM yyyy"),
                Count = inMonth.Count,
                Amount = inMonth.Sum(Amt),
                Weighted = inMonth.Sum(Wtd)
            });
        }

        var teamNames = leads.Select(x => x.SalesTeam?.Name ?? "Unassigned").Distinct().OrderBy(x => x).ToList();
        var byTeam = teamNames.Select(name =>
        {
            var l = leads.Where(x => (x.SalesTeam?.Name ?? "Unassigned") == name).ToList();
            var o = l.Where(IsOpen).ToList();
            var w = l.Where(IsWon).ToList();
            var s = l.Count(IsLost);
            var closed = w.Count + s;
            return new ForecastTeamDto
            {
                Name = name,
                OpenCount = o.Count,
                OpenAmount = o.Sum(Amt),
                Weighted = o.Sum(Wtd),
                WonCount = w.Count,
                WonAmount = w.Sum(x => x.AmountClosed ?? x.AmountTargeted ?? 0),
                LostCount = s,
                WinRate = closed == 0 ? 0 : Math.Round((double)w.Count / closed * 100, 1)
            };
        }).ToList();

        var closedTotal = won.Count + lost.Count;
        var wonAmount = won.Sum(x => x.AmountClosed ?? x.AmountTargeted ?? 0);

        return new GetSalesForecastResult
        {
            OpenAmount = open.Sum(Amt),
            WeightedAmount = open.Sum(Wtd),
            WonAmount = wonAmount,
            WonCount = won.Count,
            LostCount = lost.Count,
            OpenCount = open.Count,
            WinRate = closedTotal == 0 ? 0 : Math.Round((double)won.Count / closedTotal * 100, 1),
            AverageDealSize = won.Count == 0 ? 0 : Math.Round(wonAmount / won.Count, 2),
            ByStage = byStage,
            ByMonth = byMonth,
            ByTeam = byTeam
        };
    }
}
