using Application.Features.ReportManager;
using Application.Features.SecurityManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// Report builder. A user can report only on record types whose module role they hold
/// (Leads, Customers, SupportCases, Quotations, Activities).
/// </summary>
[Route("api/[controller]")]
public class ReportController : BaseApiController
{
    public ReportController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private bool HasRole(string role) =>
        User.IsInRole(role) || User.Claims.Any(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == role);

    private List<string> AllowedEntities() =>
        ReportCatalog.ModuleRole.Where(m => HasRole(m.Value)).Select(m => m.Key).ToList();

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    private async Task<Dictionary<string, string>> UserNamesAsync(CancellationToken ct)
    {
        var users = await _sender.Send(new GetUserListRequest(), ct);
        return (users.Data ?? new()).Where(u => u.Id != null)
            .ToDictionary(u => u.Id!, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    [Authorize]
    [HttpGet("GetCatalog")]
    public async Task<ActionResult<ApiSuccessResult<GetReportCatalogResult>>> GetCatalogAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetReportCatalogRequest { AllowedEntities = AllowedEntities() }, ct), nameof(GetCatalogAsync));

    [Authorize]
    [HttpPost("Run")]
    public async Task<ActionResult<ApiSuccessResult<RunReportResult>>> RunAsync(RunReportRequest request, CancellationToken ct)
    {
        if (!AllowedEntities().Contains(request.Entity ?? "")) return StatusCode(StatusCodes.Status403Forbidden);

        var result = await _sender.Send(request, ct);

        //>>> user columns hold ids; show names
        var userColumns = result.Columns.Select((c, i) => (c, i)).Where(x => x.c.Type == "user").Select(x => x.i).ToList();
        if (userColumns.Count > 0)
        {
            var names = await UserNamesAsync(ct);
            foreach (var row in result.Rows)
            {
                foreach (var i in userColumns)
                {
                    var id = row[i]?.ToString();
                    row[i] = string.IsNullOrEmpty(id) || id == "(none)" ? "(none)" : names.GetValueOrDefault(id, "(unknown)");
                }
            }
        }
        return Success(result, nameof(RunAsync));
    }

    [Authorize]
    [HttpGet("ListReports")]
    public async Task<ActionResult<ApiSuccessResult<ListReportsResult>>> ListReportsAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ListReportsRequest { UserId = CurrentUserId, AllowedEntities = AllowedEntities() }, ct), nameof(ListReportsAsync));

    [Authorize]
    [HttpPost("SaveReport")]
    public async Task<ActionResult<ApiSuccessResult<SaveReportResult>>> SaveReportAsync(SaveReportRequest request, CancellationToken ct)
    {
        if (!AllowedEntities().Contains(request.Entity ?? "")) return StatusCode(StatusCodes.Status403Forbidden);
        request.UserId = CurrentUserId;
        request.UserName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
        return Success(await _sender.Send(request, ct), nameof(SaveReportAsync));
    }

    [Authorize]
    [HttpGet("ListSchedules")]
    public async Task<ActionResult<ApiSuccessResult<ListSchedulesResult>>> ListSchedulesAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ListSchedulesRequest { UserId = CurrentUserId }, ct), nameof(ListSchedulesAsync));

    [Authorize]
    [HttpPost("SaveSchedule")]
    public async Task<ActionResult<ApiSuccessResult<SaveScheduleResult>>> SaveScheduleAsync(SaveScheduleRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveScheduleAsync));
    }

    [Authorize]
    [HttpPost("DeleteSchedule")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> DeleteScheduleAsync(DeleteScheduleRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteScheduleAsync));
    }

    [Authorize]
    [HttpPost("DeleteReport")]
    public async Task<ActionResult<ApiSuccessResult<DeleteReportResult>>> DeleteReportAsync(DeleteReportRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteReportAsync));
    }
}
