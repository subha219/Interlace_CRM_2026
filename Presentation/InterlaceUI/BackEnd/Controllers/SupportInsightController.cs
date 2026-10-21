using Application.Features.PortalManager;
using Application.Features.SecurityManager.Queries;
using Application.Features.SupportInsightManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Support dashboard, SLA policy and customer satisfaction (see ModulePermissions for the roles).</summary>
[Route("api/[controller]")]
public class SupportInsightController : BaseApiController
{
    public SupportInsightController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetDashboard")]
    public async Task<ActionResult<ApiSuccessResult<SupportDashboardResult>>> GetDashboardAsync(CancellationToken ct)
    {
        var result = await _sender.Send(new GetSupportDashboardRequest(), ct);

        var users = await _sender.Send(new GetUserListRequest(), ct);
        var names = (users.Data ?? new()).Where(u => u.Id != null).ToDictionary(u => u.Id!, u => $"{u.FirstName} {u.LastName}".Trim());
        var agents = result.Agents.Select(a => a with { Name = a.UserId != null && names.TryGetValue(a.UserId, out var n) ? n : "Unassigned" }).ToList();

        return Success(new SupportDashboardResult
        {
            Open = result.Open, Breached = result.Breached, DueIn24Hours = result.DueIn24Hours, Unanswered = result.Unanswered,
            ResolvedLast30Days = result.ResolvedLast30Days, AvgFirstResponseHours = result.AvgFirstResponseHours, AvgResolutionHours = result.AvgResolutionHours,
            SlaCompliancePercent = result.SlaCompliancePercent, CsatAverage = result.CsatAverage, CsatCount = result.CsatCount,
            ByPriority = result.ByPriority, ByStatus = result.ByStatus, Agents = agents
        }, nameof(GetDashboardAsync));
    }

    [Authorize][HttpGet("GetSla")]
    public async Task<ActionResult<ApiSuccessResult<GetSlaPoliciesResult>>> GetSlaAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetSlaPoliciesRequest(), ct), nameof(GetSlaAsync));

    [Authorize][HttpPost("SaveSla")]
    public async Task<ActionResult<ApiSuccessResult<SaveSlaPoliciesResult>>> SaveSlaAsync(SaveSlaPoliciesRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveSlaAsync));
    }

    [Authorize][HttpGet("GetCsat")]
    public async Task<ActionResult<ApiSuccessResult<GetCsatResult>>> GetCsatAsync([FromQuery] GetCsatRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetCsatAsync));

    [Authorize][HttpPost("RecordCsat")]
    public async Task<ActionResult<ApiSuccessResult<RecordCsatResult>>> RecordCsatAsync(RecordCsatRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(RecordCsatAsync));
    }

    [Authorize][HttpPost("ReplyToCustomer")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> ReplyToCustomerAsync(ReplyToCustomerRequest request, CancellationToken ct)
    {
        request.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(ReplyToCustomerAsync));
    }

    [Authorize][HttpPost("SendSurvey")]
    public async Task<ActionResult<ApiSuccessResult<SendCsatSurveyResult>>> SendSurveyAsync(SendCsatSurveyRequest request, CancellationToken ct)
    {
        request.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        request.UserId = CurrentUserId;
        request.UserName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
        return Success(await _sender.Send(request, ct), nameof(SendSurveyAsync));
    }
}
