using Application.Features.QuotationManager.Commands;
using Application.Features.QuotationManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class QuotationController : BaseApiController
{
    public QuotationController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateQuotation")]
    public async Task<ActionResult<ApiSuccessResult<CreateQuotationResult>>> CreateQuotationAsync(CreateQuotationRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateQuotationResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateQuotationAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateQuotation")]
    public async Task<ActionResult<ApiSuccessResult<UpdateQuotationResult>>> UpdateQuotationAsync(UpdateQuotationRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateQuotationResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateQuotationAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteQuotation")]
    public async Task<ActionResult<ApiSuccessResult<DeleteQuotationResult>>> DeleteQuotationAsync(DeleteQuotationRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteQuotationResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteQuotationAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetQuotationList")]
    public async Task<ActionResult<ApiSuccessResult<GetQuotationListResult>>> GetQuotationListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetQuotationListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetQuotationListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetQuotationListAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetQuotationStatusList")]
    public async Task<ActionResult<ApiSuccessResult<GetQuotationStatusListResult>>> GetQuotationStatusListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetQuotationStatusListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetQuotationStatusListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetQuotationStatusListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetQuotationSingle")]
    public async Task<ActionResult<ApiSuccessResult<GetQuotationSingleResult>>> GetQuotationSingleAsync(
    CancellationToken cancellationToken,
    [FromQuery] string id
    )
    {
        var request = new GetQuotationSingleRequest { Id = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetQuotationSingleResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetQuotationSingleAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ConvertQuotationToInvoice")]
    public async Task<ActionResult<ApiSuccessResult<ConvertQuotationToInvoiceResult>>> ConvertQuotationToInvoiceAsync(ConvertQuotationToInvoiceRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ConvertQuotationToInvoiceResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ConvertQuotationToInvoiceAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ConvertQuotationToSalesOrder")]
    public async Task<ActionResult<ApiSuccessResult<ConvertQuotationToSalesOrderResult>>> ConvertQuotationToSalesOrderAsync(ConvertQuotationToSalesOrderRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ConvertQuotationToSalesOrderResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ConvertQuotationToSalesOrderAsync)}",
            Content = response
        });
    }
}
