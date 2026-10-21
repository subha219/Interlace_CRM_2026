using Application.Features.CustomFieldManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// Definitions are managed with the CustomFields role. Values follow the role of the record's module
/// (Leads, Customers or SupportCases), checked here because the filter only sees the controller.
/// </summary>
[Route("api/[controller]")]
public class CustomFieldController : BaseApiController
{
    public CustomFieldController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private bool HasRole(string role) =>
        User.IsInRole(role) || User.Claims.Any(c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) && c.Value == role);

    private static string ModuleRole(string? entity) => entity switch
    {
        "Lead" => "Leads",
        "Customer" => "Customers",
        "SupportCase" => "SupportCases",
        _ => "__none__"
    };

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize]
    [HttpGet("GetDefinitions")]
    public async Task<ActionResult<ApiSuccessResult<GetCustomFieldDefinitionsResult>>> GetDefinitionsAsync([FromQuery] GetCustomFieldDefinitionsRequest request, CancellationToken cancellationToken) =>
        Success(await _sender.Send(request, cancellationToken), nameof(GetDefinitionsAsync));

    [Authorize]
    [HttpPost("SaveDefinition")]
    public async Task<ActionResult<ApiSuccessResult<SaveCustomFieldDefinitionResult>>> SaveDefinitionAsync(SaveCustomFieldDefinitionRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveDefinitionAsync));
    }

    [Authorize]
    [HttpPost("DeleteDefinition")]
    public async Task<ActionResult<ApiSuccessResult<DeleteCustomFieldDefinitionResult>>> DeleteDefinitionAsync(DeleteCustomFieldDefinitionRequest request, CancellationToken cancellationToken)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(DeleteDefinitionAsync));
    }

    [Authorize]
    [HttpGet("GetValues")]
    public async Task<ActionResult<ApiSuccessResult<GetCustomFieldValuesResult>>> GetValuesAsync([FromQuery] GetCustomFieldValuesRequest request, CancellationToken cancellationToken)
    {
        if (!HasRole(ModuleRole(request.Entity))) return StatusCode(StatusCodes.Status403Forbidden);
        return Success(await _sender.Send(request, cancellationToken), nameof(GetValuesAsync));
    }

    [Authorize]
    [HttpPost("SaveValues")]
    public async Task<ActionResult<ApiSuccessResult<SaveCustomFieldValuesResult>>> SaveValuesAsync(SaveCustomFieldValuesRequest request, CancellationToken cancellationToken)
    {
        if (!HasRole(ModuleRole(request.Entity))) return StatusCode(StatusCodes.Status403Forbidden);
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, cancellationToken), nameof(SaveValuesAsync));
    }
}
