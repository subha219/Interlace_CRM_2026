using Application.Features.ChatManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Agent side of live chat (role Chats, see ModulePermissions). Visitors use PublicChatController.</summary>
[Route("api/[controller]")]
public class ChatController : BaseApiController
{
    public ChatController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetChats")]
    public async Task<ActionResult<ApiSuccessResult<GetChatsResult>>> GetChatsAsync([FromQuery] GetChatsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetChatsAsync));

    [Authorize][HttpGet("GetThread")]
    public async Task<ActionResult<ApiSuccessResult<GetChatThreadResult>>> GetThreadAsync([FromQuery] GetChatThreadRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetThreadAsync));

    [Authorize][HttpPost("Reply")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> ReplyAsync(AgentReplyRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        request.UserName = User.Identity?.Name;
        return Success(await _sender.Send(request, ct), nameof(ReplyAsync));
    }

    [Authorize][HttpPost("CreateCase")]
    public async Task<ActionResult<ApiSuccessResult<CreateCaseFromChatResult>>> CreateCaseAsync(CreateCaseFromChatRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(CreateCaseAsync));
    }

    [Authorize][HttpPost("Close")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> CloseAsync(CloseChatRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(CloseAsync));
    }
}
