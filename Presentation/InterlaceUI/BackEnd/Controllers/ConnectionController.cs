using Application.Features.ConnectionManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Where an administrator saves the keys for Google, WhatsApp, SMS, calls, single sign-on and the AI model (role Integrations).</summary>
[Route("api/[controller]")]
public class ConnectionController : BaseApiController
{
    public ConnectionController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetConnections")]
    public async Task<ActionResult<ApiSuccessResult<GetConnectionsResult>>> GetConnectionsAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetConnectionsRequest(), ct), nameof(GetConnectionsAsync));

    [Authorize][HttpPost("Save")]
    public async Task<ActionResult<ApiSuccessResult<SaveConnectionsResult>>> SaveAsync(SaveConnectionsRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveAsync));
    }
}
