using Application.Features.MyWorkManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>Personal dashboard: only the sections the caller's roles allow, only their own records.</summary>
[Route("api/[controller]")]
public class MyWorkController : BaseApiController
{
    public MyWorkController(ISender sender) : base(sender)
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
    [HttpGet("GetMyWork")]
    public async Task<ActionResult<ApiSuccessResult<GetMyWorkResult>>> GetMyWorkAsync(CancellationToken cancellationToken)
    {
        var request = new GetMyWorkRequest
        {
            UserId = CurrentUserId,
            Leads = HasRole("Leads"),
            Customers = HasRole("Customers"),
            Support = HasRole("SupportCases"),
            Sales = HasRole("Quotations"),
            Activities = HasRole("Activities")
        };
        var response = await _sender.Send(request, cancellationToken);
        return Ok(new ApiSuccessResult<GetMyWorkResult> { Code = StatusCodes.Status200OK, Message = $"Success executing {nameof(GetMyWorkAsync)}", Content = response });
    }
}
