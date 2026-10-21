using Application.Features.EmailManager.Commands;
using Application.Features.EmailManager.Queries;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

[Route("api/[controller]")]
public class EmailController : BaseApiController
{
    public EmailController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize]
    [HttpPost("SendEmail")]
    public async Task<ActionResult<ApiSuccessResult<SendCrmEmailResult>>> SendEmailAsync(SendCrmEmailRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        request.UserName = $"{User.FindFirst("FirstName")?.Value} {User.FindFirst("LastName")?.Value}".Trim();
        request.UserEmail = User.FindFirst("email")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        return Success(await _sender.Send(request, cancellationToken), nameof(SendEmailAsync));
    }

    [Authorize]
    [HttpGet("GetEmailLogList")]
    public async Task<ActionResult<ApiSuccessResult<GetEmailLogListResult>>> GetEmailLogListAsync([FromQuery] GetEmailLogListRequest request, CancellationToken cancellationToken) =>
        Success(await _sender.Send(request, cancellationToken), nameof(GetEmailLogListAsync));

    [Authorize]
    [HttpGet("GetEmailTemplateList")]
    public async Task<ActionResult<ApiSuccessResult<GetEmailTemplateListResult>>> GetEmailTemplateListAsync(CancellationToken cancellationToken) =>
        Success(await _sender.Send(new GetEmailTemplateListRequest(), cancellationToken), nameof(GetEmailTemplateListAsync));

    [Authorize]
    [HttpPost("SaveEmailTemplate")]
    public async Task<ActionResult<ApiSuccessResult<SaveEmailTemplateResult>>> SaveEmailTemplateAsync(SaveEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveEmailTemplateAsync));
    }

    [Authorize]
    [HttpPost("DeleteEmailTemplate")]
    public async Task<ActionResult<ApiSuccessResult<SaveEmailTemplateResult>>> DeleteEmailTemplateAsync(DeleteEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(DeleteEmailTemplateAsync));
    }
}
