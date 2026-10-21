using Application.Features.SupportCaseManager.Commands;
using Application.Features.SupportCaseManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class SupportCaseController : BaseApiController
{
    public SupportCaseController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateSupportCase")]
    public async Task<ActionResult<ApiSuccessResult<CreateSupportCaseResult>>> CreateSupportCaseAsync(CreateSupportCaseRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateSupportCaseResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateSupportCaseAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateSupportCase")]
    public async Task<ActionResult<ApiSuccessResult<UpdateSupportCaseResult>>> UpdateSupportCaseAsync(UpdateSupportCaseRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateSupportCaseResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateSupportCaseAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteSupportCase")]
    public async Task<ActionResult<ApiSuccessResult<DeleteSupportCaseResult>>> DeleteSupportCaseAsync(DeleteSupportCaseRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteSupportCaseResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteSupportCaseAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCaseList")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCaseListResult>>> GetSupportCaseListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetSupportCaseListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCaseListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCaseListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCaseStatusList")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCaseStatusListResult>>> GetSupportCaseStatusListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetSupportCaseStatusListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCaseStatusListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCaseStatusListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCasePriorityList")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCasePriorityListResult>>> GetSupportCasePriorityListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetSupportCasePriorityListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCasePriorityListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCasePriorityListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCaseChannelList")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCaseChannelListResult>>> GetSupportCaseChannelListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetSupportCaseChannelListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCaseChannelListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCaseChannelListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSupportCaseSingle")]
    public async Task<ActionResult<ApiSuccessResult<GetSupportCaseSingleResult>>> GetSupportCaseSingleAsync(
        CancellationToken cancellationToken,
        [FromQuery] string id
        )
    {
        var request = new GetSupportCaseSingleRequest { Id = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSupportCaseSingleResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSupportCaseSingleAsync)}",
            Content = response
        });
    }

}
