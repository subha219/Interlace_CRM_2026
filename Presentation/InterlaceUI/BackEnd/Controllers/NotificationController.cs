using Application.Features.NotificationManager.Commands;
using Application.Features.NotificationManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class NotificationController : BaseApiController
{
    public NotificationController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetNotificationList")]
    public async Task<ActionResult<ApiSuccessResult<GetNotificationListResult>>> GetNotificationListAsync([FromQuery] GetNotificationListRequest request, CancellationToken cancellationToken)
    {
        request.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetNotificationListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetNotificationListAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("MarkNotificationRead")]
    public async Task<ActionResult<ApiSuccessResult<MarkNotificationReadResult>>> MarkNotificationReadAsync(MarkNotificationReadRequest request, CancellationToken cancellationToken)
    {
        request.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<MarkNotificationReadResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(MarkNotificationReadAsync)}",
            Content = response
        });
    }
}
