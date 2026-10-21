using System.Text;
using Application.Features.MessagingManager;
using Application.Features.TelephonyManager;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterlaceUI.BackEnd.Controllers;

/// <summary>
/// Public endpoint Meta calls for WhatsApp. GET answers the one-time verification; POST must carry a valid
/// X-Hub-Signature-256 made with WhatsApp:AppSecret, otherwise it is refused (nothing is trusted without it).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChannelWebhookController : ControllerBase
{
    private readonly ISender _sender;
    private readonly Application.Common.Services.IntegrationManager.IConnectionSettings _configuration;
    private readonly ILogger<ChannelWebhookController> _logger;

    public ChannelWebhookController(ISender sender, Application.Common.Services.IntegrationManager.IConnectionSettings configuration, ILogger<ChannelWebhookController> logger)
    {
        _sender = sender;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("WhatsApp")]
    public IActionResult VerifyWhatsApp([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = _configuration["WhatsApp:VerifyToken"];
        if (mode == "subscribe" && !string.IsNullOrEmpty(expected) && token == expected && !string.IsNullOrEmpty(challenge))
            return Content(challenge, "text/plain");
        return StatusCode(StatusCodes.Status403Forbidden);
    }

    [AllowAnonymous]
    [HttpPost("WhatsApp")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> ReceiveWhatsAppAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (!ChannelRules.VerifySignature(bytes, Request.Headers["X-Hub-Signature-256"].FirstOrDefault(), _configuration["WhatsApp:AppSecret"]))
        {
            _logger.LogWarning("WhatsApp webhook refused: missing or wrong signature.");
            return Unauthorized();
        }

        try { await _sender.Send(new ProcessWhatsAppWebhookRequest { Json = Encoding.UTF8.GetString(bytes) }, ct); }
        catch (Exception ex) { _logger.LogError(ex, "WhatsApp webhook processing failed."); }
        return Ok(); // always acknowledge a signed call, otherwise Meta keeps retrying
    }

    /// <summary>Exotel call status. Exotel cannot sign requests, so the callback URL carries a secret key (Exotel:WebhookKey).</summary>
    [AllowAnonymous]
    [HttpPost("Exotel")]
    [HttpGet("Exotel")]
    public async Task<IActionResult> ReceiveExotelAsync([FromQuery] string? key, CancellationToken ct)
    {
        if (!CallRules.KeyMatches(key, _configuration["Exotel:WebhookKey"]))
        {
            _logger.LogWarning("Exotel callback refused: missing or wrong key.");
            return Unauthorized();
        }

        string? Get(string name) =>
            Request.HasFormContentType && Request.Form.TryGetValue(name, out var f) && f.Count > 0 ? f[0] : Request.Query.TryGetValue(name, out var q) && q.Count > 0 ? q[0] : null;

        var duration = int.TryParse(Get("ConversationDuration") ?? Get("Duration"), out var d) ? d : 0;
        try
        {
            await _sender.Send(new ProcessCallCallbackRequest
            {
                CallSid = Get("CallSid"), Status = Get("Status") ?? Get("CallStatus"), DurationSeconds = duration, RecordingUrl = Get("RecordingUrl"),
                Direction = Get("Direction"), From = Get("From"), To = Get("To")
            }, ct);
        }
        catch (Exception ex) { _logger.LogError(ex, "Exotel callback processing failed."); }
        return Ok();
    }
}
