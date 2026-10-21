using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Application.Features.MessagingManager;

public record ChannelSendRequest(string To, string? Text, string? TemplateName, string? ProviderTemplateId, string? Language, IReadOnlyList<KeyValuePair<string, string>> Variables);
public record ChannelSendResult(bool Success, string? ExternalId, string? Error);

/// <summary>One messaging provider (WhatsApp Cloud API, MSG91, ...). Providers only send; everything else lives in the handlers.</summary>
public interface IChannelProvider
{
    string Channel { get; }
    bool IsConfigured { get; }
    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken);
}

public record InboundMessage(string From, string Id, string Text, string? Name, DateTime? AtUtc);
public record StatusUpdate(string Id, string Status, string? Error);
public record WebhookEvents(List<InboundMessage> Messages, List<StatusUpdate> Statuses);

public static class ChannelRules
{
    public const string WhatsApp = "WhatsApp";
    public const string Sms = "SMS";
    public static readonly string[] Channels = { WhatsApp, Sms };
    public const string DefaultCountryCode = "91";
    public static readonly TimeSpan SessionWindow = TimeSpan.FromHours(24);

    /// <summary>Digits with country code (no plus). A bare 10-digit number gets the default country code. Returns null when it cannot be a phone number.</summary>
    public static string? NormalizePhone(string? raw, string defaultCountry = DefaultCountryCode)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        var hasPlus = (raw ?? "").TrimStart().StartsWith("+") || (raw ?? "").TrimStart().StartsWith("00");
        if (raw!.TrimStart().StartsWith("00")) digits = digits[2..];
        if (!hasPlus)
        {
            if (digits.Length == 10) digits = defaultCountry + digits;
            else if (digits.Length == 11 && digits.StartsWith("0")) digits = defaultCountry + digits[1..];
        }
        return digits.Length is >= 11 and <= 15 ? digits : null;
    }

    /// <summary>The last ten digits, used to match a number written in any format.</summary>
    public static string LastTen(string? raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    /// <summary>WhatsApp allows free text only within 24 hours of the customer's last message; outside that, an approved template is required.</summary>
    public static bool SessionOpen(DateTime? lastInboundUtc, DateTime nowUtc) => lastInboundUtc != null && nowUtc - lastInboundUtc.Value <= SessionWindow;

    public static List<string> VariableNames(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

    public static string Render(string? body, IEnumerable<KeyValuePair<string, string>> values)
    {
        var text = body ?? "";
        foreach (var kv in values) text = text.Replace("{{" + kv.Key + "}}", kv.Value ?? "");
        return text;
    }

    private static readonly Regex Stop = new(@"^\s*(stop|stop all|unsubscribe|cancel|opt ?out)\s*[.!]*\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static bool IsStopKeyword(string? text) => !string.IsNullOrEmpty(text) && Stop.IsMatch(text);

    /// <summary>Meta signs the raw body with the app secret: header "sha256=&lt;hex&gt;".</summary>
    public static bool VerifySignature(byte[] body, string? header, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header) || !header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
        var given = header[7..].Trim().ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }

    public static string MapStatus(string? whatsAppStatus) => whatsAppStatus switch
    {
        "sent" => "Sent", "delivered" => "Delivered", "read" => "Read", "failed" => "Failed", _ => "Sent"
    };

    /// <summary>Reads a WhatsApp Cloud API webhook body. Unknown shapes give empty lists, never an exception.</summary>
    public static WebhookEvents ParseWhatsApp(string? json)
    {
        var result = new WebhookEvents(new(), new());
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) return result;
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;
                    var names = new Dictionary<string, string>();
                    if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array)
                        foreach (var c in contacts.EnumerateArray())
                            if (c.TryGetProperty("wa_id", out var wa) && c.TryGetProperty("profile", out var p) && p.TryGetProperty("name", out var n))
                                names[wa.GetString() ?? ""] = n.GetString() ?? "";

                    if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                        foreach (var m in messages.EnumerateArray())
                        {
                            var from = m.TryGetProperty("from", out var f) ? f.GetString() : null;
                            var id = m.TryGetProperty("id", out var i) ? i.GetString() : null;
                            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(id)) continue;
                            var type = m.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                            var text = type == "text" && m.TryGetProperty("text", out var tx) && tx.TryGetProperty("body", out var b) ? b.GetString() ?? "" : $"[{type}]";
                            DateTime? at = m.TryGetProperty("timestamp", out var ts) && long.TryParse(ts.GetString(), out var secs) ? DateTimeOffset.FromUnixTimeSeconds(secs).UtcDateTime : null;
                            result.Messages.Add(new InboundMessage(from, id, text.Length > 4000 ? text[..4000] : text, names.GetValueOrDefault(from), at));
                        }

                    if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                        foreach (var s in statuses.EnumerateArray())
                        {
                            var id = s.TryGetProperty("id", out var i) ? i.GetString() : null;
                            if (string.IsNullOrEmpty(id)) continue;
                            string? err = null;
                            if (s.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0 && errs[0].TryGetProperty("title", out var title)) err = title.GetString();
                            result.Statuses.Add(new StatusUpdate(id, MapStatus(s.TryGetProperty("status", out var st) ? st.GetString() : null), err));
                        }
                }
            }
        }
        catch (JsonException) { }
        return result;
    }
}
