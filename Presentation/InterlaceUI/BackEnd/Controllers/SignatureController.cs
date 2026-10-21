using Application.Features.SignatureManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Staff side of e-signature (roles Signatures or Contracts, see ModulePermissions).</summary>
[Route("api/[controller]")]
public class SignatureController : BaseApiController
{
    public SignatureController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetRequests")]
    public async Task<ActionResult<ApiSuccessResult<GetSignRequestsResult>>> GetRequestsAsync([FromQuery] GetSignRequestsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetRequestsAsync));

    [Authorize][HttpPost("Create")]
    public async Task<ActionResult<ApiSuccessResult<CreateSignRequestResult>>> CreateAsync(CreateSignRequestRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        request.UserName = User.Identity?.Name;
        request.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        return Success(await _sender.Send(request, ct), nameof(CreateAsync));
    }

    [Authorize][HttpPost("Cancel")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> CancelAsync(CancelSignRequestRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(CancelAsync));
    }
}
