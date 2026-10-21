using Application.Features.PaymentManager.Commands;
using Application.Features.PaymentManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class PaymentController : BaseApiController
{
    public PaymentController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreatePayment")]
    public async Task<ActionResult<ApiSuccessResult<CreatePaymentResult>>> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreatePaymentResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreatePaymentAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdatePayment")]
    public async Task<ActionResult<ApiSuccessResult<UpdatePaymentResult>>> UpdatePaymentAsync(UpdatePaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdatePaymentResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdatePaymentAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeletePayment")]
    public async Task<ActionResult<ApiSuccessResult<DeletePaymentResult>>> DeletePaymentAsync(DeletePaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeletePaymentResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeletePaymentAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetPaymentList")]
    public async Task<ActionResult<ApiSuccessResult<GetPaymentListResult>>> GetPaymentListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetPaymentListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPaymentListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPaymentListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetPaymentMethodList")]
    public async Task<ActionResult<ApiSuccessResult<GetPaymentMethodListResult>>> GetPaymentMethodListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetPaymentMethodListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPaymentMethodListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPaymentMethodListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetPaymentSingle")]
    public async Task<ActionResult<ApiSuccessResult<GetPaymentSingleResult>>> GetPaymentSingleAsync(
        CancellationToken cancellationToken,
        [FromQuery] string id
        )
    {
        var request = new GetPaymentSingleRequest { Id = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPaymentSingleResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPaymentSingleAsync)}",
            Content = response
        });
    }

}
