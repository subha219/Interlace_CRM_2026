using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DashboardManager.Queries;


public class GetCardsDashboardDto
{
    public CardsItem? CardsDashboard { get; init; }
}

public class GetCardsDashboardResult
{
    public GetCardsDashboardDto? Data { get; init; }
}

public class GetCardsDashboardRequest : IRequest<GetCardsDashboardResult>
{
}

public class GetCardsDashboardHandler : IRequestHandler<GetCardsDashboardRequest, GetCardsDashboardResult>
{
    private readonly IQueryContext _context;

    public GetCardsDashboardHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetCardsDashboardResult> Handle(GetCardsDashboardRequest request, CancellationToken cancellationToken)
    {
        var salesTotal = await _context.SalesOrderItem
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .SumAsync(x => (double?)x.Quantity, cancellationToken);

        var purchaseTotal = await _context.PurchaseOrderItem
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .SumAsync(x => (double?)x.Quantity, cancellationToken);

        var cardsDashboardData = new CardsItem
        {
            SalesTotal = salesTotal,
            PurchaseTotal = purchaseTotal,
        };



        var result = new GetCardsDashboardResult
        {
            Data = new GetCardsDashboardDto
            {
                CardsDashboard = cardsDashboardData
            }
        };

        return result;
    }
}
