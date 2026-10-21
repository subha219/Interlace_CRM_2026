using Application.Features.AssistantManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class AssistantController : BaseApiController
{
    public AssistantController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpPost("Ask")]
    public async Task<ActionResult<ApiSuccessResult<AskAssistantResult>>> AskAsync(AskAssistantRequest request, CancellationToken cancellationToken)
    {
        request.UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<AskAssistantResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(AskAsync)}",
            Content = response
        });
    }
}
