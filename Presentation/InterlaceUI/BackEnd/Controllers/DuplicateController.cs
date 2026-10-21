using Application.Features.DuplicateManager.Commands;
using Application.Features.DuplicateManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class DuplicateController : BaseApiController
{
    public DuplicateController(ISender sender) : base(sender)
    {
    }

    private bool HasRole(string role) =>
        User.IsInRole(role) || User.Claims.Any(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == role);

    private static string RoleFor(string? entity) => entity == "Lead" ? "Leads" : "Customers";

    [Authorize]
    [HttpGet("GetDuplicateGroups")]
    public async Task<ActionResult<ApiSuccessResult<GetDuplicateGroupsResult>>> GetDuplicateGroupsAsync([FromQuery] GetDuplicateGroupsRequest request, CancellationToken cancellationToken)
    {
        if (!HasRole(RoleFor(request.Entity))) return StatusCode(StatusCodes.Status403Forbidden);

        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetDuplicateGroupsResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetDuplicateGroupsAsync)}",
            Content = response
        });
    }

    [Authorize]
    [HttpPost("MergeRecords")]
    public async Task<ActionResult<ApiSuccessResult<MergeRecordsResult>>> MergeRecordsAsync(MergeRecordsRequest request, CancellationToken cancellationToken)
    {
        if (!HasRole(RoleFor(request.Entity))) return StatusCode(StatusCodes.Status403Forbidden);

        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<MergeRecordsResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(MergeRecordsAsync)}",
            Content = response
        });
    }
}
