using Application.Features.AuditLogManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class AuditLogController : BaseApiController
{
    public AuditLogController(ISender sender) : base(sender)
    {
    }

    [Authorize]
    [HttpGet("GetAuditLogList")]
    public async Task<ActionResult<ApiSuccessResult<GetAuditLogListResult>>> GetAuditLogListAsync([FromQuery] GetAuditLogListRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);

        return Ok(new ApiSuccessResult<GetAuditLogListResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(GetAuditLogListAsync)}",
            Content = response
        });
    }
}
