using Application.Features.ChatManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// Live chat for website visitors. Anonymous: the visitor proves who they are with the secret chat token
/// (sent in the X-Chat-Token header, never in a URL). A token only ever reaches its own chat.
/// </summary>
[Route("api/[controller]")]
[AllowAnonymous]
public class PublicChatController : BaseApiController
{
    public PublicChatController(ISender sender) : base(sender)
    {
    }

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    private string Token => Request.Headers["X-Chat-Token"].ToString();

    [EnableRateLimiting("auth")]
    [HttpPost("Start")]
    public async Task<ActionResult<ApiSuccessResult<StartChatResult>>> StartAsync(StartChatRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(StartAsync));

    [EnableRateLimiting("chat")]
    [HttpPost("Send")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> SendAsync(VisitorSendRequest request, CancellationToken ct)
    {
        request.Token = Token;
        try { return Success(await _sender.Send(request, ct), nameof(SendAsync)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
    }

    [EnableRateLimiting("chat")]
    [HttpGet("Poll")]
    public async Task<ActionResult<ApiSuccessResult<VisitorPollResult>>> PollAsync([FromQuery] DateTime? afterUtc, CancellationToken ct)
    {
        try { return Success(await _sender.Send(new VisitorPollRequest { Token = Token, AfterUtc = afterUtc }, ct), nameof(PollAsync)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
    }
}
