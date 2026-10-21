using Application.Features.InvoiceItemManager.Commands;
using Application.Features.InvoiceItemManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class InvoiceItemController : BaseApiController
{
    public InvoiceItemController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateInvoiceItem")]
    public async Task<ActionResult<ApiSuccessResult<CreateInvoiceItemResult>>> CreateInvoiceItemAsync(CreateInvoiceItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateInvoiceItemResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateInvoiceItemAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateInvoiceItem")]
    public async Task<ActionResult<ApiSuccessResult<UpdateInvoiceItemResult>>> UpdateInvoiceItemAsync(UpdateInvoiceItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateInvoiceItemResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateInvoiceItemAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteInvoiceItem")]
    public async Task<ActionResult<ApiSuccessResult<DeleteInvoiceItemResult>>> DeleteInvoiceItemAsync(DeleteInvoiceItemRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteInvoiceItemResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteInvoiceItemAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetInvoiceItemList")]
    public async Task<ActionResult<ApiSuccessResult<GetInvoiceItemListResult>>> GetInvoiceItemListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetInvoiceItemListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetInvoiceItemListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetInvoiceItemListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetInvoiceItemByInvoiceIdList")]
    public async Task<ActionResult<ApiSuccessResult<GetInvoiceItemByInvoiceIdListResult>>> GetInvoiceItemByInvoiceIdListAsync(
    CancellationToken cancellationToken,
    [FromQuery] string invoiceId
    )
    {
        var request = new GetInvoiceItemByInvoiceIdListRequest { InvoiceId = invoiceId };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetInvoiceItemByInvoiceIdListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetInvoiceItemByInvoiceIdListAsync)}",
            Content = response
        });
    }


}


