using Infrastructure.CalendarManager;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Infrastructure.MailSyncManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Connect a user's Gmail (read-only). Callback is anonymous (Google redirects the browser); it trusts only the signed, 10-minute state value.</summary>
[Route("api/[controller]")]
public class MailSyncController : BaseApiController
{
    private readonly DataContext _db;
    private readonly GoogleCalendarClient _google;
    private readonly CalendarProtector _protector;
    private readonly MailSyncRunner _runner;

    public MailSyncController(ISender sender, DataContext db, GoogleCalendarClient google, CalendarProtector protector, MailSyncRunner runner) : base(sender)
    {
        _db = db; _google = google; _protector = protector; _runner = runner;
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private string RedirectUri => $"{Request.Scheme}://{Request.Host}/api/MailSync/Callback";

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize]
    [HttpGet("GetStatus")]
    public async Task<ActionResult<ApiSuccessResult<object>>> GetStatusAsync(CancellationToken ct)
    {
        var uid = CurrentUserId;
        var c = await _db.MailConnection.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == uid, ct);
        var saved = await _db.EmailMessage.AsNoTracking().CountAsync(x => !x.IsDeleted && x.UserId == uid, ct);
        object content = new { configured = _google.IsConfigured, redirectUri = RedirectUri, connected = c != null && c.IsActive, email = c?.Email, lastSyncUtc = c?.LastSyncUtc, lastStatus = c?.LastStatus, needsReconnect = c != null && !c.IsActive, savedCount = saved };
        return Success(content, nameof(GetStatusAsync));
    }

    [Authorize]
    [HttpGet("GetConnectUrl")]
    public ActionResult<ApiSuccessResult<string>> GetConnectUrlAsync()
    {
        if (!_google.IsConfigured) return BadRequest(new { message = "Google is not connected. An administrator can add the client id and secret under Settings > Connections." });
        return Success(_google.BuildAuthUrl(RedirectUri, _protector.NewState(CurrentUserId), MailSyncRunner.Scope), nameof(GetConnectUrlAsync));
    }

    [AllowAnonymous]
    [HttpGet("Callback")]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        const string page = "/Profiles/GoogleMail";
        var userId = _protector.ReadState(state);
        if (userId == null) return Redirect($"{page}?error=expired");
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code)) return Redirect($"{page}?error=denied");

        try
        {
            var (refresh, email) = await _google.ExchangeCodeAsync(code, RedirectUri, ct);
            var row = await _db.MailConnection.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == userId, ct);
            if (row == null) { row = new MailConnection { UserId = userId }; _db.MailConnection.Add(row); }
            row.Email = email;
            row.RefreshTokenProtected = _protector.ProtectToken(refresh);
            row.IsActive = true;
            row.CursorMs = null; // look back over the last 30 days once
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
    public async Task<ActionResult<ApiSuccessResult<bool>>> DisconnectAsync([FromQuery] bool removeSaved, CancellationToken ct)
    {
        var uid = CurrentUserId;
        foreach (var c in await _db.MailConnection.Where(x => !x.IsDeleted && x.UserId == uid).ToListAsync(ct)) c.IsDeleted = true;
        if (removeSaved) foreach (var m in await _db.EmailMessage.Where(x => !x.IsDeleted && x.UserId == uid).ToListAsync(ct)) m.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return Success(true, nameof(DisconnectAsync));
    }
}
