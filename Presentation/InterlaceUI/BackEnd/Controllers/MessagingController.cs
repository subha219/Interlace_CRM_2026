using Application.Features.MessagingManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>WhatsApp and SMS inbox, sending and templates (role Messaging, see ModulePermissions).</summary>
[Route("api/[controller]")]
public class MessagingController : BaseApiController
{
    public MessagingController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetConversations")]
    public async Task<ActionResult<ApiSuccessResult<GetConversationsResult>>> GetConversationsAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetConversationsRequest(), ct), nameof(GetConversationsAsync));

    [Authorize][HttpGet("GetThread")]
    public async Task<ActionResult<ApiSuccessResult<GetThreadResult>>> GetThreadAsync([FromQuery] GetThreadRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetThreadAsync));

    [Authorize][HttpPost("Send")]
    public async Task<ActionResult<ApiSuccessResult<SendMessageResult>>> SendAsync(SendMessageRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        request.UserName = User.Identity?.Name;
        return Success(await _sender.Send(request, ct), nameof(SendAsync));
    }

    [Authorize][HttpGet("GetRecordComms")]
    public async Task<ActionResult<ApiSuccessResult<GetRecordCommsResult>>> GetRecordCommsAsync([FromQuery] GetRecordCommsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetRecordCommsAsync));

    [Authorize][HttpGet("GetTemplates")]
    public async Task<ActionResult<ApiSuccessResult<GetTemplatesResult>>> GetTemplatesAsync([FromQuery] GetTemplatesRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetTemplatesAsync));

    [Authorize][HttpPost("SaveTemplate")]
    public async Task<ActionResult<ApiSuccessResult<SaveTemplateResult>>> SaveTemplateAsync(SaveTemplateRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveTemplateAsync));
    }

    [Authorize][HttpPost("DeleteTemplate")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DeleteTemplateAsync(DeleteTemplateRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteTemplateAsync));
    }
}
