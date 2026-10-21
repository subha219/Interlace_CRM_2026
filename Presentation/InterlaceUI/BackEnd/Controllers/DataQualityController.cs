using Application.Features.DataQualityManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class DataQualityController : BaseApiController
{
    public DataQualityController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private bool HasRole(string role) =>
        User.IsInRole(role) || User.Claims.Any(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == role);

    [Authorize]
    [HttpGet("GetDataQuality")]
    public async Task<ActionResult<ApiSuccessResult<GetDataQualityResult>>> GetDataQualityAsync(CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetDataQualityRequest(), cancellationToken);
        return Ok(new ApiSuccessResult<GetDataQualityResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetDataQualityAsync)}", Content = response });
    }

    [Authorize]
    [HttpPost("AssignOwner")]
    public async Task<ActionResult<ApiSuccessResult<AssignOwnerResult>>> AssignOwnerAsync(AssignOwnerRequest request, CancellationToken cancellationToken)
    {
        if (!HasRole(request.Entity == "Lead" ? "Leads" : "Customers")) return StatusCode(StatusCodes.Status403Forbidden);

        request.UserId = CurrentUserId;
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<AssignOwnerResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(AssignOwnerAsync)}", Content = response });
    }
}
