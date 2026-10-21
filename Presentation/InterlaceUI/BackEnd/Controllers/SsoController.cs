using Application.Common.Services.SecurityManager;
using Application.Features.SecurityManager.Queries;
using Infrastructure.SsoManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Single sign-on with Google or Microsoft (OpenID Connect). Everything here is anonymous by nature; see SsoService for the checks.</summary>
[Route("api/[controller]")]
[AllowAnonymous]
public class SsoController : BaseApiController
{
    private const string BindCookie = "sso_bind";
    private readonly SsoService _sso;
    private readonly ISecurityService _security;
    private readonly ILogger<SsoController> _logger;

    public SsoController(ISender sender, SsoService sso, ISecurityService security, ILogger<SsoController> logger) : base(sender)
    {
        _sso = sso; _security = security; _logger = logger;
    }

    private string RedirectUri => SsoService.RedirectUri(Request.Scheme, Request.Host.ToString());

    [HttpGet("Providers")]
    public ActionResult<ApiSuccessResult<object>> Providers() =>
        Ok(new ApiSuccessResult<object>
        {
            Code = StatusCodes.Status200OK, Message = "Success executing Providers",
            Content = new { providers = _sso.Providers(), passwordLogin = _sso.PasswordLoginEnabled, redirectUri = RedirectUri }
        });

    [EnableRateLimiting("auth")]
    [HttpGet("Start")]
    public IActionResult Start([FromQuery] string? provider)
    {
        try
        {
            var (url, bind) = _sso.Start(provider ?? "", RedirectUri);
            Response.Cookies.Append(BindCookie, bind, new CookieOptions
            {
                HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromMinutes(10), Path = "/api/Sso"
            });
            return Redirect(url);
        }
        catch (SsoException ex) { return Redirect($"/Accounts/Login?sso_error={ex.Code}"); }
    }

    [EnableRateLimiting("auth")]
    [HttpGet("Callback")]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        var bind = Request.Cookies[BindCookie];
        Response.Cookies.Delete(BindCookie, new CookieOptions { Path = "/api/Sso" });
        if (!string.IsNullOrEmpty(error)) return Redirect("/Accounts/Login?sso_error=denied");

        try
        {
            var identity = await _sso.CompleteAsync(code, state, bind, RedirectUri, ct);
            return Redirect("/Accounts/SsoDone#code=" + Uri.EscapeDataString(_sso.CreateHandoff(identity)));
        }
        catch (SsoException ex)
        {
            _logger.LogWarning("SSO sign-in refused: {Code} {Message}", ex.Code, ex.Message);
            return Redirect($"/Accounts/Login?sso_error={ex.Code}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSO sign-in failed");
            return Redirect("/Accounts/Login?sso_error=failed");
        }
    }

    public class ExchangeRequest { public string? Code { get; init; } }

    [EnableRateLimiting("auth")]
    [HttpPost("Exchange")]
    public async Task<ActionResult<ApiSuccessResult<LoginResult>>> ExchangeAsync(ExchangeRequest request, CancellationToken ct)
    {
        var identity = _sso.Redeem(request.Code);
        if (identity == null) return Unauthorized(new { message = "The sign-in link has expired. Please try again." });

        var dto = await _security.LoginExternalAsync(identity.Email, identity.FirstName, identity.LastName, identity.Provider, ct);
        return Ok(new ApiSuccessResult<LoginResult>
        {
            Code = StatusCodes.Status200OK, Message = "Success executing Exchange", Content = new LoginResult { Data = dto }
        });
    }
}
