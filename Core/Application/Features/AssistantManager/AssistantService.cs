using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.CQS.Queries;
using Application.Features.AiManager;
using Application.Features.InsightsManager;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AssistantManager;

public record AssistantAnswer(string Answer, string Mode, List<string> Suggestions, List<AssistantLink> Links);
public record AssistantLink(string Title, string Url);

public class AssistantService
{
    private static readonly Regex NumberToken = new(@"\b([A-Za-z]{2,5}[-/]?\d[\w/-]*)\b", RegexOptions.Compiled);

    private readonly IQueryContext _context;
    private readonly InsightsService _insights;
    private readonly AiGovernor _llm;

    public AssistantService(IQueryContext context, InsightsService insights, AiGovernor llm)
    {
        _context = context;
        _insights = insights;
        _llm = llm;
    }

    private static readonly List<string> DefaultSuggestions = new()
    {
        "What should I do today?",
        "Which invoices are overdue?",
        "Which deals are at risk?",
        "How is the forecast?",
        "Any support cases past their SLA?"
    };

    public async Task<AssistantAnswer> AskAsync(string? question, string? userId = null, CancellationToken cancellationToken = default)
    {
        var q = (question ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            return new AssistantAnswer("Ask me about your pipeline, invoices, support cases or a specific record such as INV-0001.", "rules", DefaultSuggestions, new());
        }

        var briefing = await _insights.GetBriefingAsync(cancellationToken);
        var lower = q.ToLowerInvariant();

        //>>> 1. "draft ... <number>" -> follow-up email
        var wantsDraft = lower.Contains("draft") || lower.Contains("email") || lower.Contains("remind") || lower.Contains("follow up") || lower.Contains("follow-up");
        if (wantsDraft)
        {
            var draft = await TryDraftAsync(q, userId, cancellationToken);
            if (draft != null)
            {
                return draft;
            }
        }

        //>>> 2. language model, grounded in a live snapshot
        if (_llm.IsConfigured)
        {
            var snapshot = JsonSerializer.Serialize(briefing, new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var system = "You are the assistant inside a CRM. Answer the user's question using ONLY the JSON snapshot of live CRM data. " +
                         "Be concise (under 150 words), use short bullet lines starting with '- ', quote real numbers and record numbers from the data, " +
                         "and say clearly when the snapshot does not contain the answer. Never invent data.";
            var user = $"Snapshot (generated {briefing.GeneratedAt:yyyy-MM-dd HH:mm}):\n{snapshot}\n\nQuestion: {q}";
            //>>> goes through governance: switch, limits and usage log; falls back to rules when refused or unreachable
            var run = await _llm.RunAsync(userId, "Ask", null, null, system, user, 700, cancellationToken);
            if (run.Text != null)
            {
                return new AssistantAnswer(run.Text, "ai", DefaultSuggestions, LinksFor(lower));
            }
            var fallback = Rules(lower, briefing);
            return run.Error == null ? fallback : fallback with { Answer = fallback.Answer + "\n\n(AI is not available right now: " + run.Error + ")" };
        }

        return Rules(lower, briefing);
    }

    private static List<AssistantLink> LinksFor(string lower)
    {
        var links = new List<AssistantLink>();
        if (lower.Contains("invoice") || lower.Contains("overdue") || lower.Contains("receivable")) links.Add(new("Invoices", "/Invoices/InvoiceList"));
        if (lower.Contains("deal") || lower.Contains("pipeline") || lower.Contains("lead") || lower.Contains("risk")) links.Add(new("Pipeline board", "/Pipelines/PipelineBoard"));
        if (lower.Contains("forecast") || lower.Contains("win")) links.Add(new("Sales forecast", "/Pipelines/SalesForecast"));
        if (lower.Contains("case") || lower.Contains("sla") || lower.Contains("support")) links.Add(new("Support cases", "/SupportCases/SupportCaseList"));
        if (lower.Contains("quot")) links.Add(new("Quotations", "/Quotations/QuotationList"));
        return links;
    }

    private static string Money(double v) => v.ToString("N2");

    private AssistantAnswer Rules(string lower, TodayBriefingResult b)
    {
        double K(string key) => b.Kpis.First(x => x.Key == key).Value;
        var sb = new StringBuilder();

        if (lower.Contains("overdue") || lower.Contains("receivable") || lower.Contains("unpaid") || lower.Contains("collect") || lower.Contains("invoice"))
        {
            sb.AppendLine($"Receivables are {Money(K("receivables"))}, of which {Money(K("overdue"))} is overdue.");
            var items = b.Priorities.Where(p => p.Kind == "Invoice").ToList();
            if (items.Count == 0) sb.AppendLine("No invoice is overdue right now.");
            foreach (var p in items) sb.AppendLine($"- {p.Title}: {p.Detail} Balance {Money(p.Amount ?? 0)}");
            foreach (var a in b.Ageing.Where(x => x.Count > 0)) sb.AppendLine($"- {a.Name}: {a.Count} invoice(s), {Money(a.Amount)}");
            return Reply(sb, lower);
        }

        if (lower.Contains("risk") || lower.Contains("deal") || lower.Contains("pipeline") || lower.Contains("lead"))
        {
            sb.AppendLine($"Open pipeline {Money(K("pipeline"))}, weighted {Money(K("forecast"))}. Healthy {b.HealthyDeals}, needs attention {b.AttentionDeals}, at risk {b.AtRiskDealCount}.");
            foreach (var d in b.AtRiskDeals.Take(5)) sb.AppendLine($"- {d.Number} {d.Title} ({Money(d.Amount)}, score {d.Score}, {d.Label}): {d.NextAction}");
            if (b.AtRiskDeals.Count == 0) sb.AppendLine("All open deals look healthy.");
            return Reply(sb, lower);
        }

        if (lower.Contains("forecast") || lower.Contains("win rate") || lower.Contains("revenue") || lower.Contains("sales"))
        {
            var rev = b.Kpis.First(x => x.Key == "revenue");
            sb.AppendLine($"Cash received this month: {Money(rev.Value)} (last month {Money(rev.Previous ?? 0)}).");
            sb.AppendLine($"Weighted forecast {Money(K("forecast"))} from an open pipeline of {Money(K("pipeline"))}. Win rate {K("winrate")}%.");
            foreach (var s in b.Pipeline) sb.AppendLine($"- {s.Name}: {s.Count} deal(s), {Money(s.Amount)}, weighted {Money(s.Weighted)}");
            return Reply(sb, lower);
        }

        if (lower.Contains("case") || lower.Contains("sla") || lower.Contains("support") || lower.Contains("ticket"))
        {
            sb.AppendLine($"{K("cases")} open case(s).");
            var items = b.Priorities.Where(p => p.Kind == "Support").ToList();
            if (items.Count == 0) sb.AppendLine("No case is past or close to its SLA.");
            foreach (var p in items) sb.AppendLine($"- {p.Title} {p.Detail}");
            return Reply(sb, lower);
        }

        if (lower.Contains("quot"))
        {
            sb.AppendLine($"{K("quotes")} quotation(s) are waiting for a decision.");
            foreach (var p in b.Priorities.Where(p => p.Kind == "Quote")) sb.AppendLine($"- {p.Title}");
            return Reply(sb, lower);
        }

        if (lower.Contains("today") || lower.Contains("priorit") || lower.Contains("focus") || lower.Contains("what should") || lower.Contains("first") || lower.Contains("next"))
        {
            if (b.Priorities.Count == 0) sb.AppendLine("Nothing urgent. A good moment to follow up on healthy deals.");
            else sb.AppendLine("Your top priorities:");
            foreach (var p in b.Priorities.Take(8)) sb.AppendLine($"- [{p.Severity}] {p.Title}. {p.Detail}");
            return Reply(sb, lower);
        }

        sb.AppendLine($"Cash this month {Money(K("revenue"))}, open pipeline {Money(K("pipeline"))}, receivables {Money(K("receivables"))} ({Money(K("overdue"))} overdue), {K("cases")} open case(s).");
        sb.AppendLine($"{b.Priorities.Count} item(s) need action. Try one of the suggestions below, or ask about a record number to draft a follow-up (for example: draft reminder for INV-0001).");
        return Reply(sb, lower);
    }

    private static AssistantAnswer Reply(StringBuilder sb, string lower) =>
        new(sb.ToString().TrimEnd(), "rules", DefaultSuggestions, LinksFor(lower));

    //>>> ---- email drafting ----

    private async Task<AssistantAnswer?> TryDraftAsync(string question, string? userId, CancellationToken ct)
    {
        foreach (Match m in NumberToken.Matches(question))
        {
            var number = m.Groups[1].Value;

            var invoice = await _context.Invoice.AsNoTracking().Include(x => x.Customer)
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Number == number, ct);
            if (invoice != null)
            {
                var name = invoice.Customer?.Name ?? "there";
                var overdue = invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.Now.Date && (invoice.BalanceDue ?? 0) > 0;
                var subject = overdue ? $"Payment reminder: invoice {invoice.Number}" : $"Invoice {invoice.Number}";
                var body = $"Hi {name},\n\n" +
                           (overdue
                               ? $"This is a friendly reminder that invoice {invoice.Number} (due {invoice.DueDate:yyyy-MM-dd}) still has an open balance of {Money(invoice.BalanceDue ?? 0)}.\n\nCould you let me know when we can expect the payment? If it has already been sent, please ignore this note and share the reference so I can match it.\n"
                               : $"Please find invoice {invoice.Number} for {Money(invoice.AfterTaxAmount ?? 0)}, due {invoice.DueDate:yyyy-MM-dd}.\n\nLet me know if you need anything from our side.\n") +
                           "\nThank you,";
                return await FinishDraftAsync(invoice.Customer?.EmailAddress, subject, body, $"Invoice {invoice.Number} for {invoice.Customer?.Name}, balance {Money(invoice.BalanceDue ?? 0)}, due {invoice.DueDate:yyyy-MM-dd}", userId, ct);
            }

            var quotation = await _context.Quotation.AsNoTracking().Include(x => x.Customer)
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Number == number, ct);
            if (quotation != null)
            {
                var name = quotation.Customer?.Name ?? "there";
                var subject = $"Following up on quotation {quotation.Number}";
                var body = $"Hi {name},\n\nI wanted to follow up on quotation {quotation.Number} for {Money(quotation.AfterTaxAmount ?? 0)}, which is valid until {quotation.ValidUntil:yyyy-MM-dd}.\n\nDo you have any questions, or is there anything you would like us to adjust? I am happy to jump on a short call.\n\nBest regards,";
                return await FinishDraftAsync(quotation.Customer?.EmailAddress, subject, body, $"Quotation {quotation.Number} for {quotation.Customer?.Name}, {Money(quotation.AfterTaxAmount ?? 0)}, valid until {quotation.ValidUntil:yyyy-MM-dd}", userId, ct);
            }

            var lead = await _context.Lead.AsNoTracking()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Number == number, ct);
            if (lead != null)
            {
                var contact = await _context.LeadContact.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.LeadId == lead.Id).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
                var name = contact?.FullName ?? lead.CompanyName ?? "there";
                var subject = $"Next steps for {lead.Title}";
                var body = $"Hi {name},\n\nThank you for your time so far on {lead.Title}. I would like to agree the next step and a realistic timeline" +
                           (lead.DateClosingEstimation.HasValue ? $" (we were aiming for {lead.DateClosingEstimation:yyyy-MM-dd})" : "") +
                           ".\n\nWould you have 20 minutes this week for a quick call?\n\nBest regards,";
                return await FinishDraftAsync(contact?.Email ?? lead.CompanyEmail, subject, body, $"Lead {lead.Number} {lead.Title} at {lead.CompanyName}, stage {lead.PipelineStage}", userId, ct);
            }
        }

        return null;
    }

    private async Task<AssistantAnswer> FinishDraftAsync(string? to, string subject, string body, string facts, string? userId, CancellationToken ct)
    {
        var mode = "rules";
        if (_llm.IsConfigured)
        {
            var run = await _llm.RunAsync(userId, "EmailDraft", null, null,
                "You write short, warm, professional business emails. Keep the facts exactly as given. Reply with the email only, subject on the first line starting with 'Subject:'.",
                $"Facts: {facts}\nBase draft:\nSubject: {subject}\n\n{body}\n\nImprove the wording slightly. Do not add facts.", 700, ct);
            if (run.Text != null)
            {
                return new AssistantAnswer(run.Text + (to != null ? $"\n\n(To: {to})" : ""), "ai", DefaultSuggestions, new());
            }
        }

        var answer = $"Subject: {subject}\n\n{body}" + (to != null ? $"\n\n(To: {to})" : "");
        return new AssistantAnswer(answer, mode, DefaultSuggestions, new());
    }
}
