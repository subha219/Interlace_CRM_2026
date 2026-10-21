using Application.Features.SavedViewManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Saved grid views. Every user manages their own; shared views are readable by all.</summary>
[Route("api/[controller]")]
public class SavedViewController : BaseApiController
{
    public SavedViewController(ISender sender) : base(sender)
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
    [HttpGet("GetSavedViewList")]
    public async Task<ActionResult<ApiSuccessResult<GetSavedViewListResult>>> GetSavedViewListAsync([FromQuery] GetSavedViewListRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(GetSavedViewListAsync));
    }

    [Authorize]
    [HttpPost("SaveSavedView")]
    public async Task<ActionResult<ApiSuccessResult<SaveSavedViewResult>>> SaveSavedViewAsync(SaveSavedViewRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        request.UserName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveSavedViewAsync));
    }

    [Authorize]
    [HttpPost("DeleteSavedView")]
    public async Task<ActionResult<ApiSuccessResult<DeleteSavedViewResult>>> DeleteSavedViewAsync(DeleteSavedViewRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(DeleteSavedViewAsync));
    }
}
