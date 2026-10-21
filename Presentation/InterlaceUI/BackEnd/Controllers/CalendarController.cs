using Infrastructure.CalendarManager;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Connect a user's Google Calendar. Callback is anonymous (Google redirects the browser); it trusts only the signed, 10-minute state value.</summary>
[Route("api/[controller]")]
public class CalendarController : BaseApiController
{
    private readonly DataContext _db;
    private readonly GoogleCalendarClient _google;
    private readonly CalendarProtector _protector;
    private readonly OutlookCalendarClient _outlook;
    private readonly CalendarSyncRunner _runner;

    public CalendarController(ISender sender, DataContext db, GoogleCalendarClient google, OutlookCalendarClient outlook, CalendarProtector protector, CalendarSyncRunner runner) : base(sender)
    {
        _db = db; _google = google; _outlook = outlook; _protector = protector; _runner = runner;
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private string RedirectUri => $"{Request.Scheme}://{Request.Host}/api/Calendar/Callback";
    private string OutlookRedirectUri => $"{Request.Scheme}://{Request.Host}/api/Calendar/OutlookCallback";

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize]
    [HttpGet("GetStatus")]
    public async Task<ActionResult<ApiSuccessResult<object>>> GetStatusAsync(CancellationToken ct)
    {
        var uid = CurrentUserId;
        var c = await _db.CalendarConnection.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == uid, ct);
        object content = new { configured = _google.IsConfigured, redirectUri = RedirectUri, connected = c != null && c.IsActive, provider = c?.Provider ?? "Google", outlookConfigured = _outlook.IsConfigured, outlookRedirectUri = OutlookRedirectUri, email = c?.Email, lastSyncUtc = c?.LastSyncUtc, lastStatus = c?.LastStatus, needsReconnect = c != null && !c.IsActive };
        return Success(content, nameof(GetStatusAsync));
    }

    [Authorize]
    [HttpGet("GetConnectUrl")]
    public ActionResult<ApiSuccessResult<string>> GetConnectUrlAsync([FromQuery] string? provider = null)
    {
        if (string.Equals(provider, "outlook", StringComparison.OrdinalIgnoreCase))
        {
            if (!_outlook.IsConfigured) return BadRequest(new { message = "Microsoft is not connected. An administrator can add the Microsoft client under Settings > Connections." });
            return Success(_outlook.BuildAuthUrl(OutlookRedirectUri, _protector.NewState(CurrentUserId)), nameof(GetConnectUrlAsync));
        }
        if (!_google.IsConfigured) return BadRequest(new { message = "Google is not connected. An administrator can add the client id and secret under Settings > Connections." });
        return Success(_google.BuildAuthUrl(RedirectUri, _protector.NewState(CurrentUserId)), nameof(GetConnectUrlAsync));
    }

    [AllowAnonymous]
    [HttpGet("Callback")]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        const string page = "/Profiles/GoogleCalendar";
        var userId = _protector.ReadState(state);
        if (userId == null) return Redirect($"{page}?error=expired");
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code)) return Redirect($"{page}?error=denied");

        try
        {
            var (refresh, email) = await _google.ExchangeCodeAsync(code, RedirectUri, ct);
            var row = await _db.CalendarConnection.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == userId, ct);
            if (row == null) { row = new CalendarConnection { UserId = userId, Provider = "Google" }; _db.CalendarConnection.Add(row); }
            await ForgetLinksIfSwitchingAsync(row, "Google", userId, ct);
            row.Email = email;
            row.RefreshTokenProtected = _protector.ProtectToken(refresh);
            row.IsActive = true;
            row.LastStatus = "Connected. The first sync runs within 10 minutes, or press Sync now.";
            await _db.SaveChangesAsync(ct);
            return Redirect($"{page}?connected=1");
        }
        catch (Exception)
        {
            return Redirect($"{page}?error=failed");
        }
    }

    /// <summary>Switching to the other calendar forgets which events were made in the old one (they stay there).</summary>
    private async Task ForgetLinksIfSwitchingAsync(CalendarConnection row, string provider, string userId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(row.Provider) && row.Provider != provider)
            foreach (var l in await _db.CalendarEventLink.Where(x => !x.IsDeleted && x.UserId == userId).ToListAsync(ct)) l.IsDeleted = true;
        row.Provider = provider;
    }

    [AllowAnonymous]
    [HttpGet("OutlookCallback")]
    public async Task<IActionResult> OutlookCallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        const string page = "/Profiles/GoogleCalendar";
        var userId = _protector.ReadState(state);
        if (userId == null) return Redirect($"{page}?error=expired");
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code)) return Redirect($"{page}?error=denied");

        try
        {
            var (refresh, email) = await _outlook.ExchangeCodeAsync(code, OutlookRedirectUri, ct);
            var row = await _db.CalendarConnection.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == userId, ct);
            if (row == null) { row = new CalendarConnection { UserId = userId, Provider = "Outlook" }; _db.CalendarConnection.Add(row); }
            await ForgetLinksIfSwitchingAsync(row, "Outlook", userId, ct);
            row.Email = email;
            row.RefreshTokenProtected = _protector.ProtectToken(refresh);
            row.IsActive = true;
            row.LastStatus = "Connected. The first sync runs within 10 minutes, or press Sync now.";
            await _db.SaveChangesAsync(ct);
            return Redirect($"{page}?connected=1");
        }
        catch (Exception)
        {
            return Redirect($"{page}?error=failed");
        }
    }

    [Authorize]
    [HttpPost("SyncNow")]
    public async Task<ActionResult<ApiSuccessResult<string>>> SyncNowAsync(CancellationToken ct) =>
        Success(await _runner.SyncUserAsync(CurrentUserId, ct), nameof(SyncNowAsync));

    [Authorize]
    [HttpPost("Disconnect")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DisconnectAsync(CancellationToken ct)
    {
        var uid = CurrentUserId;
        foreach (var c in await _db.CalendarConnection.Where(x => !x.IsDeleted && x.UserId == uid).ToListAsync(ct)) c.IsDeleted = true;
        foreach (var l in await _db.CalendarEventLink.Where(x => !x.IsDeleted && x.UserId == uid).ToListAsync(ct)) l.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return Success(true, nameof(DisconnectAsync));
    }
}
