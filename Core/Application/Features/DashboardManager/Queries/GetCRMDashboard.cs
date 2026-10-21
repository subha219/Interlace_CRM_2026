using Application.Common.CQS.Queries;
using Application.Common.Extensions;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DashboardManager.Queries;


public class GetCRMDashboardDto
{
    public CRMItem? CRMDashboard { get; init; }
}

public class GetCRMDashboardResult
{
    public GetCRMDashboardDto? Data { get; init; }
}

public class GetCRMDashboardRequest : IRequest<GetCRMDashboardResult>
{
}

public class GetCRMDashboardHandler : IRequestHandler<GetCRMDashboardRequest, GetCRMDashboardResult>
{
    private readonly IQueryContext _context;

    public GetCRMDashboardHandler(IQueryContext context)
    {
        _context = context;
    }

    public async Task<GetCRMDashboardResult> Handle(GetCRMDashboardRequest request, CancellationToken cancellationToken)
    {
        var campaignTotalAmount = await _context.Campaign
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .SumAsync(x => (double?)x.TargetRevenueAmount, cancellationToken);

        var closedTotalAmount = await _context.Lead
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .Where(x => x.ClosingStatus == ClosingStatus.ClosedWon)
            .SumAsync(x => (double?)x.AmountClosed, cancellationToken);

        var leadTotalAmount = await _context.Lead
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .SumAsync(x => (double?)x.AmountTargeted, cancellationToken);

        var budgetTotalAmount = await _context.Budget
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .SumAsync(x => (double?)x.Amount, cancellationToken);

        var expenseTotalAmount = await _context.Expense
            .AsNoTracking()
            .IsDeletedEqualTo(false)
            .SumAsync(x => (double?)x.Amount, cancellationToken);


        var cardsDashboardData = new CRMItem
        {
            CampaignTotalAmount = campaignTotalAmount,
            LeadTotalAmount = leadTotalAmount,
            BudgetTotalAmount = budgetTotalAmount,
            ExpenseTotalAmount = expenseTotalAmount,
            ClosedTotalAmount = closedTotalAmount
        };



        var result = new GetCRMDashboardResult
        {
            Data = new GetCRMDashboardDto
            {
                CRMDashboard = cardsDashboardData
            }
        };

        return result;
    }
}
