using System.Text.Json;
using Domain.Enums;

namespace Application.Features.AgentManager;

public record AgentDef(string Key, string Name, string Description, bool UsesAi, string RequiredRole);

/// <summary>The decisions agents make, kept free of the database so they can be tested on their own.</summary>
public static class AgentRules
{
    public const string LeadFollowUp = "lead-followup";
    public const string SupportTriage = "support-triage";
    public const string ContractRenewal = "contract-renewal";
    public const string DataQuality = "data-quality";

    public const int StaleLeadDays = 7;
    public const int RejectedQuietDays = 30;

    /// <summary>Name recorded on runs started by the scheduler instead of a person.</summary>
    public const string AutoRunName = "Automatic";
    public const int AutoRunEveryHours = 23;

    /// <summary>An automatic run is due when none has happened in the last day.</summary>
    public static bool IsAutoDue(DateTime? lastAutoRunUtc, DateTime nowUtc) =>
        lastAutoRunUtc == null || nowUtc - lastAutoRunUtc.Value >= TimeSpan.FromHours(AutoRunEveryHours);
    public const int ApprovedQuietDays = 7;

    public static readonly IReadOnlyList<AgentDef> Catalog = new List<AgentDef>
    {
        new(LeadFollowUp, "Lead follow-up", "Finds open leads nobody has touched for a week and suggests a follow-up task with a draft message. Uses the AI model for the draft when one is set up.", true, "Activities"),
        new(SupportTriage, "Support triage", "Reads new support cases and suggests a priority and, when the case has none, a category. Uses the AI model when one is set up, simple keyword rules otherwise.", true, "SupportCases"),
        new(ContractRenewal, "Contract renewal", "Finds active contracts that end soon and have no renewal, and suggests a renewal task with a draft message.", false, "Activities"),
        new(DataQuality, "Data quality", "Finds customers and leads with no e-mail or phone and suggests a task to complete them.", false, "Activities"),
    };

    public static AgentDef? Find(string? key) => Catalog.FirstOrDefault(a => a.Key == key);

    public static string DedupKey(string agent, string relatedType, string relatedId, string? kind = null) =>
        $"{agent}:{relatedType}:{relatedId}" + (kind == "SetCategory" ? ":category" : "");

    // ---------- lead follow-up ----------

    /// <summary>The reason to follow up, or null when the lead is fine. A brand-new lead is given a week before it counts as stale.</summary>
    public static string? StaleLeadReason(DateTime createdUtc, DateTime? lastTouchUtc, DateTime nowUtc)
    {
        var since = lastTouchUtc ?? createdUtc;
        var days = (int)(nowUtc - since).TotalDays;
        if (days < StaleLeadDays) return null;
        return lastTouchUtc == null ? $"No activity logged for {days} days since the lead was created." : $"No activity for {days} days.";
    }

    public static string LeadPriority(DateTime? expectedClose, DateTime nowUtc) =>
        expectedClose != null && expectedClose.Value.Date <= nowUtc.Date.AddDays(14) ? "High" : "Normal";

    // ---------- contract renewal ----------

    /// <summary>Days left when the contract ends inside its notice window (at least 30 days), else null.</summary>
    public static int? RenewalDaysLeft(DateTime? endDate, int noticeDays, DateTime today)
    {
        if (endDate == null) return null;
        var left = (int)(endDate.Value.Date - today.Date).TotalDays;
        return left >= 0 && left <= Math.Max(30, noticeDays) ? left : null;
    }

    // ---------- data quality ----------

    public static List<string> ContactGaps(string? email, string? phone)
    {
        var gaps = new List<string>();
        if (string.IsNullOrWhiteSpace(email)) gaps.Add("e-mail");
        if (string.IsNullOrWhiteSpace(phone)) gaps.Add("phone number");
        return gaps;
    }

    // ---------- support triage ----------

    private static readonly string[] CriticalWords = { "outage", "data loss", "security", "breach", "production down", "system down", "cannot log in", "can't log in" };
    private static readonly string[] HighWords = { "urgent", "asap", "immediately", "unable", "cannot", "can't", "not working", "failed", "error", "blocked", "refund" };
    private static readonly string[] LowWords = { "how do i", "how to", "question", "feature request", "suggestion", "would be nice" };

    public record Triage(CasePriority Priority, string Reason, string? Category = null);

    public static Triage RuleTriage(string? title, string? description)
    {
        var text = ((title ?? "") + " " + (description ?? "")).ToLowerInvariant();
        string? hit = CriticalWords.FirstOrDefault(text.Contains);
        if (hit != null) return new Triage(CasePriority.Critical, $"The text mentions \"{hit}\".");
        hit = HighWords.FirstOrDefault(text.Contains);
        if (hit != null) return new Triage(CasePriority.High, $"The text mentions \"{hit}\".");
        hit = LowWords.FirstOrDefault(text.Contains);
        if (hit != null) return new Triage(CasePriority.Low, $"Looks like a question or suggestion (\"{hit}\").");
        return new Triage(CasePriority.Normal, "Nothing in the text points to urgency.");
    }

    /// <summary>Strict: only {"priority":"Low|Normal|High|Critical","reason":"..."} is accepted, anything else returns null.</summary>
    public static Triage? ParseTriage(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;
        var start = answer.IndexOf('{'); var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var doc = JsonDocument.Parse(answer.Substring(start, end - start + 1));
            var r = doc.RootElement;
            if (!r.TryGetProperty("priority", out var p) || p.ValueKind != JsonValueKind.String) return null;
            if (!Enum.TryParse<CasePriority>(p.GetString(), true, out var priority) || !Enum.IsDefined(typeof(CasePriority), priority)) return null;
            var reason = r.TryGetProperty("reason", out var rs) && rs.ValueKind == JsonValueKind.String ? rs.GetString() : null;
            reason = string.IsNullOrWhiteSpace(reason) ? "Suggested by the AI model." : reason.Trim();
            var category = r.TryGetProperty("category", out var cg) && cg.ValueKind == JsonValueKind.String ? cg.GetString() : null;
            category = string.IsNullOrWhiteSpace(category) ? null : (category.Trim().Length > 100 ? null : category.Trim());
            return new Triage(priority, reason.Length > 300 ? reason[..300] : reason, category);
        }
        catch (JsonException) { return null; }
    }

    // ---------- payloads ----------

    public static string TaskPayload(int dueInDays, string priority, string? draft = null) =>
        string.IsNullOrWhiteSpace(draft) ? JsonSerializer.Serialize(new { dueInDays, priority }) : JsonSerializer.Serialize(new { dueInDays, priority, draft });
    public static string CategoryPayload(string id, string name) => JsonSerializer.Serialize(new { id, name });

    public static (string Id, string Name)? ParseCategoryPayload(string? payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload ?? "");
            var id = doc.RootElement.GetProperty("id").GetString();
            var name = doc.RootElement.GetProperty("name").GetString();
            return string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) ? null : (id, name);
        }
        catch (Exception) { return null; }
    }

    public static string? ParseDraft(string? payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload ?? "");
            return doc.RootElement.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.String ? Clip(d.GetString(), DraftMaxChars) : null;
        }
        catch (Exception) { return null; }
    }

    // ---------- categories and drafts ----------

    public const int DraftMaxChars = 600;

    /// <summary>The category whose name appears in the text (the longest name wins), or null. Names under 3 letters are ignored.</summary>
    public static (string Id, string Name)? MatchCategory(string? title, string? description, IEnumerable<(string Id, string Name)> categories)
    {
        var text = ((title ?? "") + " " + (description ?? "")).ToLowerInvariant();
        return categories.Where(c => c.Name.Trim().Length >= 3 && text.Contains(c.Name.Trim().ToLowerInvariant()))
            .OrderByDescending(c => c.Name.Trim().Length).Select(c => ((string, string)?)(c.Id, c.Name)).FirstOrDefault();
    }

    /// <summary>A message written by the AI model is used only when it is plain text of a sensible size and holds no link or markup.</summary>
    public static string? CleanDraft(string? text)
    {
        var t = (text ?? "").Replace("\r\n", "\n").Trim().Trim('"').Trim();
        if (t.Length < 20) return null;
        var lower = t.ToLowerInvariant();
        if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains("www.") || lower.Contains('<') || lower.Contains('>')) return null;
        return Clip(t, DraftMaxChars);
    }

    public static string LeadDraft(string? leadTitle, string? company)
    {
        var subject = !string.IsNullOrWhiteSpace(company) ? company.Trim() : !string.IsNullOrWhiteSpace(leadTitle) ? leadTitle.Trim() : "our earlier conversation";
        return Clip($"Hello,\n\nI wanted to follow up on {subject}. Is now a good time to talk about next steps? I am happy to answer any questions.\n\nThank you", DraftMaxChars)!;
    }

    public static string RenewalDraft(string? number, string? title, DateTime? end)
    {
        var name = $"{number} {title}".Trim();
        var when = end == null ? "soon" : "on " + end.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return Clip($"Hello,\n\nYour agreement {name} ends {when}. Would you like to renew it? I can prepare the renewal terms for you.\n\nThank you", DraftMaxChars)!;
    }
    public static string PriorityPayload(int from, int to) => JsonSerializer.Serialize(new { from, to });

    public static (int DueInDays, string Priority)? ParseTaskPayload(string? payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload ?? "");
            var r = doc.RootElement;
            var days = r.GetProperty("dueInDays").GetInt32();
            var pr = r.GetProperty("priority").GetString();
            return days is >= 0 and <= 365 && pr is "Low" or "Normal" or "High" ? (days, pr!) : null;
        }
        catch (Exception) { return null; }
    }

    public static (int From, int To)? ParsePriorityPayload(string? payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload ?? "");
            var from = doc.RootElement.GetProperty("from").GetInt32();
            var to = doc.RootElement.GetProperty("to").GetInt32();
            return Enum.IsDefined(typeof(CasePriority), from) && Enum.IsDefined(typeof(CasePriority), to) ? (from, to) : null;
        }
        catch (Exception) { return null; }
    }

    public static string Clip(string? s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length > n ? s[..n] : s;
}
