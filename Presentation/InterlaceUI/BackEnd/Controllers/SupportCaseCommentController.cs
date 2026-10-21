using Application.Features.SupportCaseCommentManager.Commands;
using Application.Features.SupportCaseCommentManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class SupportCaseCommentController : BaseApiController
{
    public SupportCaseCommentController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateSupportCaseComment")]
    public async Task<ActionResult<ApiSuccessResult<CreateSupportCaseCommentResult>>> CreateSupportCaseCommentAsync(CreateSupportCaseCommentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateSupportCaseCommentResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateSupportCaseCommentAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateSupportCaseComment")]
    public async Task<ActionResult<ApiSuccessResult<UpdateSupportCaseCommentResult>>> UpdateSupportCaseCommentAsync(UpdateSupportCaseCommentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateSupportCaseCommentResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateSupportCaseCommentAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteSupportCaseComment")]
    public async Task<ActionResult<ApiSuccessResult<DeleteSupportCaseCommentResult>>> DeleteSupportCaseCommentAsync(DeleteSupportCaseCommentRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteSupportCaseCommentResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteSupportCaseCommentAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCaseCommentList")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCaseCommentListResult>>> GetSupportCaseCommentListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetSupportCaseCommentListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCaseCommentListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCaseCommentListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCaseCommentSingle")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCaseCommentSingleResult>>> GetSupportCaseCommentSingleAsync(
        CancellationToken cancellationToken,
        [FromQuery] string id
        )
    {
        var request = new GetSupportCaseCommentSingleRequest { Id = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCaseCommentSingleResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCaseCommentSingleAsync)}",
            Content = response
        });
    }

}
