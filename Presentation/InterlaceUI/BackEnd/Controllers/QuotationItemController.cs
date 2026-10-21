using Application.Features.QuotationItemManager.Commands;
using Application.Features.QuotationItemManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class QuotationItemController : BaseApiController
{
    public QuotationItemController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateQuotationItem")]
    public async Task<ActionResult<ApiSuccessResult<CreateQuotationItemResult>>> CreateQuotationItemAsync(CreateQuotationItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateQuotationItemResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateQuotationItemAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateQuotationItem")]
    public async Task<ActionResult<ApiSuccessResult<UpdateQuotationItemResult>>> UpdateQuotationItemAsync(UpdateQuotationItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateQuotationItemResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateQuotationItemAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteQuotationItem")]
    public async Task<ActionResult<ApiSuccessResult<DeleteQuotationItemResult>>> DeleteQuotationItemAsync(DeleteQuotationItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteQuotationItemResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteQuotationItemAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetQuotationItemList")]
    public async Task<ActionResult<ApiSuccessResult<GetQuotationItemListResult>>> GetQuotationItemListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetQuotationItemListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetQuotationItemListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetQuotationItemListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetQuotationItemByQuotationIdList")]
    public async Task<ActionResult<ApiSuccessResult<GetQuotationItemByQuotationIdListResult>>> GetQuotationItemByQuotationIdListAsync(
    CancellationToken cancellationToken,
    [FromQuery] string quotationId
    )
    {
        var request = new GetQuotationItemByQuotationIdListRequest { QuotationId = quotationId };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetQuotationItemByQuotationIdListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetQuotationItemByQuotationIdListAsync)}",
            Content = response
        });
    }


}


