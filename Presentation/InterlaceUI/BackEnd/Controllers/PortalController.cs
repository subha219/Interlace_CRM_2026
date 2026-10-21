using Application.Features.PortalManager;
using FluentValidation;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// Customer portal. Customers are not CRM users: they sign in with a one-time e-mail link and then send a
/// session token in the X-Portal-Token header. Every call is limited to that customer's own records.
/// </summary>
[Route("api/[controller]")]
[AllowAnonymous]
public class PortalController : BaseApiController
{
    public PortalController(ISender sender) : base(sender)
    {
    }

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    private async Task<PortalIdentity?> IdentityAsync(CancellationToken ct, bool end = false)
    {
        var token = Request.Headers["X-Portal-Token"].ToString();
        try { return await _sender.Send(new ResolvePortalSessionRequest { Token = token, EndSession = end }, ct); }
        catch (UnauthorizedAccessException) { return null; }
    }

    [EnableRateLimiting("auth")]
    [HttpPost("RequestLogin")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> RequestLoginAsync(RequestPortalLoginRequest request, CancellationToken ct)
    {
        request.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        return Success(await _sender.Send(request, ct), nameof(RequestLoginAsync));
    }

    [EnableRateLimiting("auth")]
    [HttpPost("Exchange")]
    public async Task<ActionResult<ApiSuccessResult<PortalSessionResult>>> ExchangeAsync(ExchangePortalLoginRequest request, CancellationToken ct)
    {
        try { return Success(await _sender.Send(request, ct), nameof(ExchangeAsync)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
    }

    [HttpPost("Logout")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> LogoutAsync(CancellationToken ct)
    {
        await IdentityAsync(ct, end: true);
        return Success(true, nameof(LogoutAsync));
    }

    [HttpGet("Overview")]
    public async Task<ActionResult<ApiSuccessResult<PortalOverviewResult>>> OverviewAsync(CancellationToken ct)
    {
        var id = await IdentityAsync(ct);
        if (id == null) return Unauthorized();
        return Success(await _sender.Send(new GetPortalOverviewRequest { Identity = id }, ct), nameof(OverviewAsync));
    }

    [HttpGet("Case")]
    public async Task<ActionResult<ApiSuccessResult<PortalCaseResult>>> CaseAsync([FromQuery] string? id, CancellationToken ct)
    {
        var who = await IdentityAsync(ct);
        if (who == null) return Unauthorized();
        try { return Success(await _sender.Send(new GetPortalCaseRequest { Identity = who, CaseId = id }, ct), nameof(CaseAsync)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("OpenCase")]
    public async Task<ActionResult<ApiSuccessResult<OpenPortalCaseResult>>> OpenCaseAsync(OpenPortalCaseRequest request, CancellationToken ct)
    {
        var who = await IdentityAsync(ct);
        if (who == null) return Unauthorized();
        request.Identity = who;
        return Success(await _sender.Send(request, ct), nameof(OpenCaseAsync));
    }

    [HttpPost("AddComment")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> AddCommentAsync(AddPortalCommentRequest request, CancellationToken ct)
    {
        var who = await IdentityAsync(ct);
        if (who == null) return Unauthorized();
        request.Identity = who;
        try { return Success(await _sender.Send(request, ct), nameof(AddCommentAsync)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
