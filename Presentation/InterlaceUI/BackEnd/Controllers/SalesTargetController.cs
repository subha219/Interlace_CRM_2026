using Application.Features.SalesTargetManager;
using Application.Features.SecurityManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class SalesTargetController : BaseApiController
{
    public SalesTargetController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize]
    [HttpGet("GetLeaderboard")]
    public async Task<ActionResult<ApiSuccessResult<GetLeaderboardResult>>> GetLeaderboardAsync([FromQuery] GetLeaderboardRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request, cancellationToken);
        var users = await _sender.Send(new GetUserListRequest(), cancellationToken);
        var names = (users.Data ?? new()).ToDictionary(u => u.Id ?? "", u => $"{u.FirstName} {u.LastName}".Trim());
        foreach (var row in result.Rows)
        {
            row.Name = row.UserId != null && names.TryGetValue(row.UserId, out var n) ? n : "Unassigned";
        }
        return Success(result, nameof(GetLeaderboardAsync));
    }

    [Authorize]
    [HttpGet("GetStageVelocity")]
    public async Task<ActionResult<ApiSuccessResult<GetStageVelocityResult>>> GetStageVelocityAsync(CancellationToken cancellationToken) =>
        Success(await _sender.Send(new GetStageVelocityRequest(), cancellationToken), nameof(GetStageVelocityAsync));

    [Authorize]
    [HttpGet("GetTargets")]
    public async Task<ActionResult<ApiSuccessResult<GetTargetsResult>>> GetTargetsAsync([FromQuery] GetTargetsRequest request, CancellationToken cancellationToken) =>
        Success(await _sender.Send(request, cancellationToken), nameof(GetTargetsAsync));

    [Authorize]
    [HttpPost("SaveTargets")]
    public async Task<ActionResult<ApiSuccessResult<SaveTargetsResult>>> SaveTargetsAsync(SaveTargetsRequest request, CancellationToken cancellationToken)
    {
        request.CurrentUserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveTargetsAsync));
    }
}
