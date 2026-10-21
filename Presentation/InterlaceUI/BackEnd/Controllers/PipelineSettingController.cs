using Application.Features.PipelineSettingManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class PipelineSettingController : BaseApiController
{
    public PipelineSettingController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetStages")]
    public async Task<ActionResult<ApiSuccessResult<GetPipelineStagesResult>>> GetStagesAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetPipelineStagesRequest(), ct), nameof(GetStagesAsync));

    [Authorize][HttpPost("SaveStages")]
    public async Task<ActionResult<ApiSuccessResult<SavePipelineStagesResult>>> SaveStagesAsync(SavePipelineStagesRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveStagesAsync));
    }

    [Authorize][HttpPost("ResetStages")]
    public async Task<ActionResult<ApiSuccessResult<ResetPipelineStagesResult>>> ResetStagesAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ResetPipelineStagesRequest { UserId = CurrentUserId }, ct), nameof(ResetStagesAsync));
}
