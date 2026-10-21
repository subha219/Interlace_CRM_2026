using Application.Features.CampaignManager.Queries;
using Application.Features.LeadManager.Commands;
using Application.Features.LeadManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class LeadController : BaseApiController
{
    public LeadController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("CreateLead")]
    public async Task<ActionResult<ApiSuccessResult<CreateLeadResult>>> CreateLeadAsync(CreateLeadRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<CreateLeadResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(CreateLeadAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("UpdateLead")]
    public async Task<ActionResult<ApiSuccessResult<UpdateLeadResult>>> UpdateLeadAsync(UpdateLeadRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<UpdateLeadResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(UpdateLeadAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("DeleteLead")]
    public async Task<ActionResult<ApiSuccessResult<DeleteLeadResult>>> DeleteLeadAsync(DeleteLeadRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<DeleteLeadResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(DeleteLeadAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetLeadList")]
    public async Task<ActionResult<ApiSuccessResult<GetLeadListResult>>> GetLeadListAsync(
        CancellationToken cancellationToken,
        [FromQuery] bool isDeleted = false
        )
    {
        var request = new GetLeadListRequest { IsDeleted = isDeleted };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetLeadListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetLeadListAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetLeadSingle")]
    public async Task<ActionResult<ApiSuccessResult<GetLeadSingleResult>>> GetLeadSingleAsync(
    CancellationToken cancellationToken,
    [FromQuery] string id
    )
    {
        var request = new GetLeadSingleRequest { Id = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetLeadSingleResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetLeadSingleAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetLeadByCampaignIdList")]
    public async Task<ActionResult<ApiSuccessResult<GetLeadByCampaignIdListResult>>> GetLeadByCampaignIdListAsync(
    CancellationToken cancellationToken,
    [FromQuery] string id
    )
    {
        var request = new GetLeadByCampaignIdListRequest { CampaignId = id };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetLeadByCampaignIdListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetLeadByCampaignIdListAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpGet("GetPipelineStageList")]
    public async Task<ActionResult<ApiSuccessResult<GetPipelineStageListResult>>> GetPipelineStageListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetPipelineStageListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPipelineStageListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPipelineStageListAsync)}",
            Content = response
        });
    }




    [Authorize]
    [HttpGet("GetClosingStatusList")]
    public async Task<ActionResult<ApiSuccessResult<GetClosingStatusListResult>>> GetClosingStatusListAsync(
        CancellationToken cancellationToken
        )
    {
        var request = new GetClosingStatusListRequest { };
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetClosingStatusListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetClosingStatusListAsync)}",
            Content = response
        });
    }


    [Authorize]
    [HttpPost("MoveLeadStage")]
    public async Task<ActionResult<ApiSuccessResult<MoveLeadStageResult>>> MoveLeadStageAsync(MoveLeadStageRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<MoveLeadStageResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(MoveLeadStageAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("ConvertLead")]
    public async Task<ActionResult<ApiSuccessResult<ConvertLeadResult>>> ConvertLeadAsync(ConvertLeadRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<ConvertLeadResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(ConvertLeadAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetPipelineBoard")]
    public async Task<ActionResult<ApiSuccessResult<GetPipelineBoardResult>>> GetPipelineBoardAsync([FromQuery] GetPipelineBoardRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetPipelineBoardResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetPipelineBoardAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpGet("GetSalesForecast")]
    public async Task<ActionResult<ApiSuccessResult<GetSalesForecastResult>>> GetSalesForecastAsync([FromQuery] GetSalesForecastRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetSalesForecastResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetSalesForecastAsync)}",
            Content = response
        });
    }
}
