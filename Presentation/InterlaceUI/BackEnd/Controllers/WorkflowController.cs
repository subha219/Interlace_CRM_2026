using Application.Features.WorkflowManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class WorkflowController : BaseApiController
{
    public WorkflowController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("ListRules")]
    public async Task<ActionResult<ApiSuccessResult<ListWorkflowRulesResult>>> ListRulesAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ListWorkflowRulesRequest(), ct), nameof(ListRulesAsync));

    [Authorize][HttpPost("SaveRule")]
    public async Task<ActionResult<ApiSuccessResult<SaveWorkflowRuleResult>>> SaveRuleAsync(SaveWorkflowRuleRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveRuleAsync));
    }

    [Authorize][HttpPost("DeleteRule")]
    public async Task<ActionResult<ApiSuccessResult<DeleteWorkflowRuleResult>>> DeleteRuleAsync(DeleteWorkflowRuleRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteRuleAsync));
    }

    [Authorize][HttpGet("ListRuns")]
    public async Task<ActionResult<ApiSuccessResult<ListWorkflowRunsResult>>> ListRunsAsync([FromQuery] ListWorkflowRunsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(ListRunsAsync));
}
