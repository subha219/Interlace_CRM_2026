using Application.Features.InsightsManager;
using Application.Features.InsightsManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class InsightsController : BaseApiController
{
    public InsightsController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetTodayBriefing")]
    public async Task<ActionResult<ApiSuccessResult<TodayBriefingResult>>> GetTodayBriefingAsync([FromQuery] GetTodayBriefingRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        //>>> money figures are for people who work with invoices and payments, the rest follow their own module
        bool Has(params string[] roles) => roles.Any(User.IsInRole);
        var money = Has("Invoices", "Payments");
        bool KpiAllowed(string key) => key switch
        {
            "revenue" or "receivables" or "overdue" => money,
            "cases" => Has("SupportCases"),
            "quotes" => Has("Quotations"),
            _ => Has("Pipelines"),
        };
        response = new TodayBriefingResult
        {
            GeneratedAt = response.GeneratedAt, Kpis = response.Kpis.Where(k => KpiAllowed(k.Key)).ToList(), Priorities = response.Priorities,
            AtRiskDeals = response.AtRiskDeals, Pipeline = response.Pipeline, HealthyDeals = response.HealthyDeals, AttentionDeals = response.AttentionDeals,
            AtRiskDealCount = response.AtRiskDealCount, Ageing = money ? response.Ageing : new(), RevenueByMonth = money ? response.RevenueByMonth : new()
        };

        return Ok(new ApiSuccessResult<TodayBriefingResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetTodayBriefingAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetDealHealth")]
    public async Task<ActionResult<ApiSuccessResult<GetDealHealthResult>>> GetDealHealthAsync([FromQuery] GetDealHealthRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetDealHealthResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetDealHealthAsync)}",
            Content = response
        });
    }
}
