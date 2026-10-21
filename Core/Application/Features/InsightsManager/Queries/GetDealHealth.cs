using Application.Common.CQS.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.InsightsManager.Queries;

public class GetDealHealthResult
{
    public string? LeadId { get; init; }
    public string? Number { get; init; }
    public string? Title { get; init; }
    public DealHealthResult? Health { get; init; }
}

public class GetDealHealthRequest : IRequest<GetDealHealthResult>
{
    public string? LeadId { get; init; }
}

public class GetDealHealthHandler : IRequestHandler<GetDealHealthRequest, GetDealHealthResult>
{
    private readonly IQueryContext _context;
    private readonly DealHealthService _dealHealth;

    public GetDealHealthHandler(IQueryContext context, DealHealthService dealHealth)
    {
        _context = context;
        _dealHealth = dealHealth;
    }

    public async Task<GetDealHealthResult> Handle(GetDealHealthRequest request, CancellationToken cancellationToken)
    {
        var lead = await _context.Lead.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.LeadId && !x.IsDeleted, cancellationToken);

        if (lead == null)
        {
            throw new Exception($"Entity not found: {request.LeadId}");
        }

        var map = await _dealHealth.EvaluateAsync(new[] { lead }, cancellationToken);

        return new GetDealHealthResult
        {
            LeadId = lead.Id,
            Number = lead.Number,
            Title = lead.Title,
            Health = map[lead.Id]
        };
    }
}
