using Application.Features.DashboardManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class DashboardController : BaseApiController
{
    public DashboardController(ISender sender) : base(sender)
    {
    }

    private bool Has(params string[] roles) => roles.Any(r => User.IsInRole(r) || User.Claims.Any(c => (c.Type == "role" || c.Type == "roles" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == r));

    //>>> the totals come from several modules, so each figure is blanked unless the user holds that module's role
    private GetCRMDashboardResult TrimCrm(GetCRMDashboardResult r)
    {
        var s = r.Data?.CRMDashboard;
        if (s == null) return r;
        var leads = Has("Leads", "Pipelines");
        return new GetCRMDashboardResult
        {
            Data = new GetCRMDashboardDto
            {
                CRMDashboard = new CRMItem
                {
                    CampaignTotalAmount = Has("Campaigns") ? s.CampaignTotalAmount : null,
                    LeadTotalAmount = leads ? s.LeadTotalAmount : null,
                    ClosedTotalAmount = leads ? s.ClosedTotalAmount : null,
                    BudgetTotalAmount = Has("Budgets") ? s.BudgetTotalAmount : null,
                    ExpenseTotalAmount = Has("Expenses") ? s.ExpenseTotalAmount : null,
                }
            }
        };
    }

    private GetCardsDashboardResult TrimCards(GetCardsDashboardResult r)
    {
        var s = r.Data?.CardsDashboard;
        if (s == null) return r;
        var sales = Has("SalesOrders", "Invoices");
        var purchase = Has("PurchaseOrders");
        return new GetCardsDashboardResult
        {
            Data = new GetCardsDashboardDto
            {
                CardsDashboard = new CardsItem
                {
                    SalesTotal = sales ? s.SalesTotal : null,
                    SalesReturnTotal = sales ? s.SalesReturnTotal : null,
                    DeliveryOrderTotal = sales ? s.DeliveryOrderTotal : null,
                    PurchaseTotal = purchase ? s.PurchaseTotal : null,
                    PurchaseReturnTotal = purchase ? s.PurchaseReturnTotal : null,
                    GoodsReceiveTotal = purchase ? s.GoodsReceiveTotal : null,
                    TransferOutTotal = sales && purchase ? s.TransferOutTotal : null,
                    TransferInTotal = sales && purchase ? s.TransferInTotal : null,
                }
            }
        };
    }


    [Authorize]
    [HttpGet("GetCRMDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetCRMDashboardResult>>> GetCRMDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetCRMDashboardRequest { };
        var response = TrimCrm(await _sender.Send(request, cancellationToken));

        return Ok(new ApiSuccessResult<GetCRMDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetCRMDashboardAsync)}",
            Content = response
        });
    }




    [Authorize]
    [HttpGet("GetLeadPipelineFunnel")]
    public async Task<ActionResult<ApiSuccessResult<GetLeadPipelineFunnelResult>>> GetLeadPipelineFunnelAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetLeadPipelineFunnelRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetLeadPipelineFunnelResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetLeadPipelineFunnelAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetSalesTeamLeadClosing")]
    public async Task<ActionResult<ApiSuccessResult<GetSalesTeamLeadClosingResult>>> GetSalesTeamLeadClosingAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetSalesTeamLeadClosingRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSalesTeamLeadClosingResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSalesTeamLeadClosingAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetCampaignByStatus")]
    public async Task<ActionResult<ApiSuccessResult<GetCampaignByStatusResult>>> GetCampaignByStatusAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetCampaignByStatusRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetCampaignByStatusResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetCampaignByStatusAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetLeadActivityByType")]
    public async Task<ActionResult<ApiSuccessResult<GetLeadActivityByTypeResult>>> GetLeadActivityByTypeAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetLeadActivityByTypeRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetLeadActivityByTypeResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetLeadActivityByTypeAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetCardsDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetCardsDashboardResult>>> GetCardsDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetCardsDashboardRequest { };
        var response = TrimCards(await _sender.Send(request, cancellationToken));

        return Ok(new ApiSuccessResult<GetCardsDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetCardsDashboardAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetSalesDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetSalesDashboardResult>>> GetSalesDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetSalesDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSalesDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSalesDashboardAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetPurchaseDashboard")]
    public async Task<ActionResult<ApiSuccessResult<GetPurchaseDashboardResult>>> GetPurchaseDashboardAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetPurchaseDashboardRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPurchaseDashboardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPurchaseDashboardAsync)}",
            Content = response
        });
    }




}


