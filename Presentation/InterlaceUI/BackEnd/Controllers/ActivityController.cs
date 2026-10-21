using Application.Features.ActivityManager.Commands;
using Application.Features.ActivityManager.Queries;
using Application.Features.SecurityManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class ActivityController : BaseApiController
{
    public ActivityController(ISender sender) : base(sender)
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
    [HttpGet("GetActivityList")]
    public async Task<ActionResult<ApiSuccessResult<GetActivityListResult>>> GetActivityListAsync([FromQuery] GetActivityListRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(GetActivityListAsync));
    }

    [Authorize]
    [HttpPost("SaveActivity")]
    public async Task<ActionResult<ApiSuccessResult<ActivityResult>>> SaveActivityAsync(SaveActivityRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveActivityAsync));
    }

    [Authorize]
    [HttpPost("SetActivityStatus")]
    public async Task<ActionResult<ApiSuccessResult<ActivityResult>>> SetActivityStatusAsync(SetActivityStatusRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SetActivityStatusAsync));
    }

    [Authorize]
    [HttpPost("DeleteActivity")]
    public async Task<ActionResult<ApiSuccessResult<ActivityResult>>> DeleteActivityAsync(DeleteActivityRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(DeleteActivityAsync));
    }

    /// <summary>Id and display name of active users, for the "assigned to" picker. No other user data is returned.</summary>
    [Authorize]
    [HttpGet("GetAssignees")]
    public async Task<ActionResult<ApiSuccessResult<List<object>>>> GetAssigneesAsync(CancellationToken cancellationToken)
    {
        var users = await _sender.Send(new GetUserListRequest(), cancellationToken);
        var list = (users.Data ?? new())
            .Where(u => u.IsBlocked != true && u.IsDeleted != true)
            .Select(u => (object)new { id = u.Id, name = $"{u.FirstName} {u.LastName}".Trim() })
            .ToList();
        return Success(list, nameof(GetAssigneesAsync));
    }
}
