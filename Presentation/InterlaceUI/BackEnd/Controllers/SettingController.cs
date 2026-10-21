using Application.Features.SettingManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class SettingController : BaseApiController
{
    public SettingController(ISender sender) : base(sender)
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
    [HttpGet("GetSmtpSettings")]
    public async Task<ActionResult<ApiSuccessResult<GetSmtpSettingsResult>>> GetSmtpSettingsAsync(CancellationToken cancellationToken) =>
        Success(await _sender.Send(new GetSmtpSettingsRequest(), cancellationToken), nameof(GetSmtpSettingsAsync));

    [Authorize]
    [HttpPost("SaveSmtpSettings")]
    public async Task<ActionResult<ApiSuccessResult<SaveSmtpSettingsResult>>> SaveSmtpSettingsAsync(SaveSmtpSettingsRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveSmtpSettingsAsync));
    }

    [Authorize]
    [HttpPost("SendTestEmail")]
    public async Task<ActionResult<ApiSuccessResult<SendTestEmailResult>>> SendTestEmailAsync(SendTestEmailRequest request, CancellationToken cancellationToken) =>
        Success(await _sender.Send(request, cancellationToken), nameof(SendTestEmailAsync));
}
