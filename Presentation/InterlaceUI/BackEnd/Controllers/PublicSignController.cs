using Application.Features.SignatureManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Signer side of e-signature. Anonymous: the secret link token (X-Sign-Token header) opens exactly one request.</summary>
[Route("api/[controller]")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class PublicSignController : BaseApiController
{
    public PublicSignController(ISender sender) : base(sender)
    {
    }

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    private string Token => Request.Headers["X-Sign-Token"].ToString();

    private async Task<ActionResult<ApiSuccessResult<T>>> RunAsync<T>(Func<Task<T>> work, string action)
    {
        try { return Success(await work(), action); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
    }

    [HttpGet("Get")]
    public Task<ActionResult<ApiSuccessResult<SigningViewResult>>> GetAsync(CancellationToken ct) =>
        RunAsync(() => _sender.Send(new GetSigningViewRequest { Token = Token }, ct), nameof(GetAsync));

    [HttpPost("SendCode")]
    public Task<ActionResult<ApiSuccessResult<bool>>> SendCodeAsync(CancellationToken ct) =>
        RunAsync(() => _sender.Send(new SendSigningCodeRequest { Token = Token }, ct), nameof(SendCodeAsync));

    [HttpPost("Sign")]
    public Task<ActionResult<ApiSuccessResult<bool>>> SignAsync(SignDocumentRequest request, CancellationToken ct)
    {
        request.Token = Token;
        request.Ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        request.UserAgent = Request.Headers.UserAgent.ToString();
        return RunAsync(() => _sender.Send(request, ct), nameof(SignAsync));
    }

    [HttpPost("Decline")]
    public Task<ActionResult<ApiSuccessResult<bool>>> DeclineAsync(DeclineDocumentRequest request, CancellationToken ct)
    {
        request.Token = Token;
        return RunAsync(() => _sender.Send(request, ct), nameof(DeclineAsync));
    }
}
