using Application.Features.AiManager;
using Application.Features.SecurityManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// AI summaries, reply drafts and confirmed copilot actions. A person can use them only on record types whose module role
/// they hold. Governance (switch, limits, usage) needs the AiGovernance role. See ModulePermissions.
/// </summary>
[Route("api/[controller]")]
public class AiController : BaseApiController
{
    public AiController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private bool HasRole(string role) =>
        User.IsInRole(role) || User.Claims.Any(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == role);

    private static string? RoleFor(string? entity) => entity switch { "Customer" => "Customers", "Lead" => "Leads", "SupportCase" => "SupportCases", _ => null };

    private bool CanUse(string? entity) { var r = RoleFor(entity); return r != null && HasRole(r); }

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpPost("Summarize")]
    public async Task<ActionResult<ApiSuccessResult<AiTextResult>>> SummarizeAsync(SummarizeRecordRequest request, CancellationToken ct)
    {
        if (!CanUse(request.Entity)) return Forbid();
        request.UserId = CurrentUserId;
        try { return Success(await _sender.Send(request, ct), nameof(SummarizeAsync)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize][HttpPost("DraftReply")]
    public async Task<ActionResult<ApiSuccessResult<AiTextResult>>> DraftReplyAsync(DraftCaseReplyRequest request, CancellationToken ct)
    {
        if (!HasRole("SupportCases")) return Forbid();
        request.UserId = CurrentUserId;
        try { return Success(await _sender.Send(request, ct), nameof(DraftReplyAsync)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize][HttpPost("ProposeAction")]
    public async Task<ActionResult<ApiSuccessResult<ProposeActionResult>>> ProposeActionAsync(ProposeActionRequest request, CancellationToken ct)
    {
        if (!CanUse(request.Entity) || !HasRole("Activities")) return Forbid();
        request.UserId = CurrentUserId;
        try { return Success(await _sender.Send(request, ct), nameof(ProposeActionAsync)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize][HttpPost("ExecuteAction")]
    public async Task<ActionResult<ApiSuccessResult<ExecuteActionResult>>> ExecuteActionAsync(ExecuteActionRequest request, CancellationToken ct)
    {
        if (!CanUse(request.Entity) || !HasRole("Activities")) return Forbid();
        request.UserId = CurrentUserId;
        try { return Success(await _sender.Send(request, ct), nameof(ExecuteActionAsync)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize][HttpGet("GetGovernance")]
    public async Task<ActionResult<object>> GetGovernanceAsync(CancellationToken ct)
    {
        var r = await _sender.Send(new GetAiGovernanceRequest(), ct);
        var users = await _sender.Send(new GetUserListRequest(), ct);
        var names = (users.Data ?? new()).Where(u => u.Id != null).ToDictionary(u => u.Id!, u => $"{u.FirstName} {u.LastName}".Trim());
        string Name(string? id) => id != null && names.TryGetValue(id, out var n) ? n : "-";
        var content = new
        {
            r.Settings, r.UsedToday, r.UsedThisMonth, r.FailedThisMonth,
            ByUser = r.ByUser.Select(x => new { name = Name(x.UserId), count = x.Count }),
            Recent = r.Recent.Select(x => new { x.At, user = Name(x.UserId), x.Feature, x.RelatedType, x.Success, x.Error, x.DurationMs, x.Chars })
        };
        return Ok(new ApiSuccessResult<object> { Code = StatusCodes.Status200OK, Message = "Success executing GetGovernanceAsync", Content = content });
    }

    [Authorize][HttpPost("SaveSettings")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> SaveSettingsAsync(SaveAiSettingsRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveSettingsAsync));
    }
}
