using Application.Features.AgentManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>AI agents (role Agents, see ModulePermissions). Agents only propose; approving needs the role of the change itself.</summary>
[Route("api/[controller]")]
public class AgentController : BaseApiController
{
    public AgentController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("GetAgents")]
    public async Task<ActionResult<ApiSuccessResult<GetAgentsResult>>> GetAgentsAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetAgentsRequest(), ct), nameof(GetAgentsAsync));

    [Authorize][HttpPost("SaveSetting")]
    public async Task<ActionResult<ApiSuccessResult<bool>>> SaveSettingAsync(SaveAgentSettingRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveSettingAsync));
    }

    [Authorize][HttpPost("Run")]
    public async Task<ActionResult<ApiSuccessResult<RunAgentResult>>> RunAsync(RunAgentRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        request.UserName = User.Identity?.Name;
        return Success(await _sender.Send(request, ct), nameof(RunAsync));
    }

    [Authorize][HttpGet("GetProposals")]
    public async Task<ActionResult<ApiSuccessResult<GetProposalsResult>>> GetProposalsAsync([FromQuery] GetProposalsRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(GetProposalsAsync));

    [Authorize][HttpGet("GetRuns")]
    public async Task<ActionResult<ApiSuccessResult<GetRunsResult>>> GetRunsAsync(CancellationToken ct) =>
        Success(await _sender.Send(new GetRunsRequest(), ct), nameof(GetRunsAsync));

    [Authorize][HttpPost("Decide")]
    public async Task<ActionResult<ApiSuccessResult<DecideProposalResult>>> DecideAsync(DecideProposalRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        request.UserName = User.Identity?.Name;
        request.UserRoles = User.Claims
            .Where(c => c.Type == "role" || c.Type == "roles" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).Distinct().ToList();
        return Success(await _sender.Send(request, ct), nameof(DecideAsync));
    }
}
