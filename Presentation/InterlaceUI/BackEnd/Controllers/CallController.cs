using Application.Features.TelephonyManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Click-to-call and the call log (role Calls, see ModulePermissions).</summary>
[Route("api/[controller]")]
public class CallController : BaseApiController
{
    private readonly Application.Common.Services.IntegrationManager.IConnectionSettings _configuration;

    public CallController(ISender sender, Application.Common.Services.IntegrationManager.IConnectionSettings configuration) : base(sender)
    {
        _configuration = configuration;
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetCalls")]
    public async Task<ActionResult<ApiSuccessResult<GetCallsResult>>> GetCallsAsync([FromQuery] GetCallsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetCallsAsync));

    [Authorize][HttpPost("Start")]
    public async Task<ActionResult<ApiSuccessResult<StartCallResult>>> StartAsync(StartCallRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        request.UserName = User.Identity?.Name;
        var key = _configuration["Exotel:WebhookKey"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            var baseUrl = _configuration["Exotel:CallbackBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";
            request.StatusCallbackUrl = $"{baseUrl.TrimEnd('/')}/api/ChannelWebhook/Exotel?key={Uri.EscapeDataString(key)}";
        }
        return Success(await _sender.Send(request, ct), nameof(StartAsync));
    }

    [Authorize][HttpPost("SaveNote")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> SaveNoteAsync(SaveCallNoteRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveNoteAsync));
    }
}
