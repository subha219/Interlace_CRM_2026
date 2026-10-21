using Application.Features.IntegrationManager;
using InterlaceUI.BackEnd.Common.Base;
using InterlaceUI.BackEnd.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>API keys and webhooks. Everything needs the Integrations role.</summary>
[Route("api/[controller]")]
public class IntegrationController : BaseApiController
{
    public IntegrationController(ISender sender) : base(sender)
    {
    }

    private string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value
        ?? throw new UnauthorizedAccessException("User id not found in token.");

    private ActionResult<ApiSuccessResult<T>> Success<T>(T content, string action) =>
        Ok(new ApiSuccessResult<T> { Code = StatusCodes.Status200OK, Message = $"Success executing {action}", Content = content });

    [Authorize][HttpGet("ListApiKeys")]
    public async Task<ActionResult<ApiSuccessResult<ListApiKeysResult>>> ListApiKeysAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ListApiKeysRequest(), ct), nameof(ListApiKeysAsync));

    [Authorize][HttpPost("CreateApiKey")]
    public async Task<ActionResult<ApiSuccessResult<CreateApiKeyResult>>> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(CreateApiKeyAsync));
    }

    [Authorize][HttpPost("RevokeApiKey")]
    public async Task<ActionResult<ApiSuccessResult<RevokeApiKeyResult>>> RevokeApiKeyAsync(RevokeApiKeyRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(RevokeApiKeyAsync));
    }

    [Authorize][HttpGet("ListWebhooks")]
    public async Task<ActionResult<ApiSuccessResult<ListWebhooksResult>>> ListWebhooksAsync(CancellationToken ct) =>
        Success(await _sender.Send(new ListWebhooksRequest(), ct), nameof(ListWebhooksAsync));

    [Authorize][HttpPost("SaveWebhook")]
    public async Task<ActionResult<ApiSuccessResult<SaveWebhookResult>>> SaveWebhookAsync(SaveWebhookRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(SaveWebhookAsync));
    }

    [Authorize][HttpPost("DeleteWebhook")]
    public async Task<ActionResult<ApiSuccessResult<DeleteWebhookResult>>> DeleteWebhookAsync(DeleteWebhookRequest request, CancellationToken ct)
    {
        request.UserId = CurrentUserId;
        return Success(await _sender.Send(request, ct), nameof(DeleteWebhookAsync));
    }

    [Authorize][HttpGet("ListDeliveries")]
    public async Task<ActionResult<ApiSuccessResult<ListDeliveriesResult>>> ListDeliveriesAsync([FromQuery] ListDeliveriesRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(ListDeliveriesAsync));

    [Authorize][HttpPost("TestWebhook")]
    public async Task<ActionResult<ApiSuccessResult<TestWebhookResult>>> TestWebhookAsync(TestWebhookRequest request, CancellationToken ct) =>
        Success(await _sender.Send(request, ct), nameof(TestWebhookAsync));
}
