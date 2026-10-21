using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ReportManager;

public static class ReportScheduleRules
{
    public static readonly string[] Frequencies = { "Daily", "Weekly", "Monthly" };
    public static readonly string[] AttachFormats = { "Csv", "Pdf", "Both" };

    public static string NormalizeAttach(string? a) => AttachFormats.FirstOrDefault(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase)) ?? "Csv";
    public const int MaxRecipients = 10;

    /// <summary>The most recent moment this schedule should have fired, at or before now (local time).</summary>
    public static DateTime LatestSlot(string? frequency, int day, int hour, DateTime nowLocal)
    {
        var slot = nowLocal.Date.AddHours(hour);
        if (frequency == "Weekly")
        {
            var back = ((int)nowLocal.DayOfWeek - day + 7) % 7;
            slot = slot.AddDays(-back);
        }
        else if (frequency == "Monthly")
        {
            var d = Math.Clamp(day, 1, 28);
            slot = new DateTime(nowLocal.Year, nowLocal.Month, d).AddHours(hour);
            if (slot > nowLocal) slot = slot.AddMonths(-1);
            return slot;
        }
        if (slot > nowLocal) slot = slot.AddDays(frequency == "Weekly" ? -7 : -1);
        return slot;
    }

    /// <summary>Due when the latest slot is newer than the last run. A missed slot fires once, late.</summary>
    public static bool IsDue(string? frequency, int day, int hour, DateTime? lastRunUtc, DateTime nowLocal)
    {
        var slot = LatestSlot(frequency, day, hour, nowLocal);
        return lastRunUtc == null || lastRunUtc.Value.ToLocalTime() < slot;
    }

    public static List<string> ParseRecipients(string? text) =>
        (text ?? "").Split(new[] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static bool IsEmail(string s)
    {
        try { var a = new MailAddress(s); return a.Address == s && s.Contains('.'); } catch { return false; }
    }

    /// <summary>Turns what the report page saved into a run request. Returns null when the saved text is unusable.</summary>
    public static RunReportRequest? ToRequest(string? definitionJson, string? entity)
    {
        if (string.IsNullOrWhiteSpace(definitionJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var r = doc.RootElement;
            string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var group = S("mode") == "group";
            var cols = new List<string>();
            if (!group && r.TryGetProperty("columns", out var c) && c.ValueKind == JsonValueKind.Array)
                foreach (var x in c.EnumerateArray()) if (x.ValueKind == JsonValueKind.String) cols.Add(x.GetString()!);
            var filters = new List<ReportFilter>();
            if (r.TryGetProperty("filters", out var f) && f.ValueKind == JsonValueKind.Array)
                foreach (var x in f.EnumerateArray())
                {
                    string? P(string n) => x.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    if (P("field") != null && P("op") != null) filters.Add(new ReportFilter(P("field")!, P("op")!, P("value")));
                }
            return new RunReportRequest
            {
                Entity = entity, Columns = cols.Count > 0 ? cols : null, Filters = filters,
                GroupBy = group ? S("groupBy") : null, Metric = group ? S("metric") : null,
                SortBy = !group && !string.IsNullOrEmpty(S("sortBy")) ? S("sortBy") : null,
                SortDescending = r.TryGetProperty("sortDescending", out var sd) && sd.ValueKind == JsonValueKind.True,
                Limit = 200
            };
        }
        catch (JsonException) { return null; }
    }

    public const int MaxCsvBytes = 5 * 1024 * 1024;

    /// <summary>
    /// All rows as CSV (UTF-8 with a byte order mark so Excel reads it right). Values are quoted when needed, and a value that
    /// starts with = + - @ is prefixed with an apostrophe so a spreadsheet never runs it as a formula.
    /// </summary>
    public static string BuildCsv(RunReportResult result)
    {
        static string Cell(object? v)
        {
            var s = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (s.Length > 0 && (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r') && !double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) s = "'" + s;
            return s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        var sb = new StringBuilder();
        sb.Append(string.Join(",", result.Columns.Select(c => Cell(c.Label)))).Append("\r\n");
        foreach (var row in result.Rows) sb.Append(string.Join(",", row.Select(Cell))).Append("\r\n");
        return sb.ToString();
    }

    public static string SafeFileName(string? name, string extension = ".csv")
    {
        var clean = new string((name ?? "report").Where(ch => char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_').ToArray()).Trim().Replace(' ', '_');
        return (clean.Length == 0 ? "report" : clean.Length > 60 ? clean[..60] : clean) + extension;
    }

    /// <summary>A simple HTML table for the e-mail body (first rows only), with every value encoded.</summary>
    public static string BuildHtml(string name, RunReportResult result, int maxRows = 100)
    {
        static string e(string? s) => WebUtility.HtmlEncode(s) ?? "";
        var sb = new StringBuilder();
        sb.Append($"<p>Scheduled report <strong>{e(name)}</strong>: {result.Total} record(s).</p>");
        sb.Append("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;font-family:sans-serif;font-size:13px\"><tr>");
        foreach (var c in result.Columns) sb.Append($"<th style=\"background:#f1f1f1;text-align:left\">{e(c.Label)}</th>");
        sb.Append("</tr>");
        foreach (var row in result.Rows.Take(maxRows))
        {
            sb.Append("<tr>");
            foreach (var v in row) sb.Append($"<td>{e(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "")}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        if (result.Rows.Count > maxRows || result.Total > result.Rows.Count) sb.Append($"<p>Only the first {Math.Min(maxRows, result.Rows.Count)} row(s) are shown. Open the report in the CRM to see everything.</p>");
        if (result.Truncated) sb.Append("<p>Note: only the newest 20,000 records were read.</p>");
        return sb.ToString();
    }
}

public record ReportScheduleDto(string? Id, string? ReportId, string? Frequency, int Day, int Hour, string? Recipients, bool IsActive, DateTime? LastRunAtUtc, string? LastStatus, string? Attach = null);
public class ListSchedulesResult { public List<ReportScheduleDto> Data { get; init; } = new(); }
public class ListSchedulesRequest : IRequest<ListSchedulesResult> { public string? UserId { get; set; } }

public class ListSchedulesHandler : IRequestHandler<ListSchedulesRequest, ListSchedulesResult>
{
    private readonly IQueryContext _context;
    public ListSchedulesHandler(IQueryContext context) { _context = context; }

    public async Task<ListSchedulesResult> Handle(ListSchedulesRequest request, CancellationToken cancellationToken)
    {
        var rows = await _context.ReportSchedule.AsNoTracking().Where(x => !x.IsDeleted && x.OwnerUserId == request.UserId).ToListAsync(cancellationToken);
        return new ListSchedulesResult { Data = rows.Select(x => new ReportScheduleDto(x.Id, x.ReportId, x.Frequency, x.Day, x.Hour, x.Recipients, x.IsActive, x.LastRunAtUtc, x.LastStatus, ReportScheduleRules.NormalizeAttach(x.Attach))).ToList() };
    }
}

public class SaveScheduleResult { public string? Id { get; init; } }

public class SaveScheduleRequest : IRequest<SaveScheduleResult>
{
    public string? ReportId { get; init; }
    public string? Frequency { get; init; }
    public int Day { get; init; }
    public int Hour { get; init; }
    public string? Recipients { get; init; }
    public string? Attach { get; init; }
    public bool IsActive { get; init; } = true;
    public string? UserId { get; set; }
}

public class SaveScheduleValidator : AbstractValidator<SaveScheduleRequest>
{
    public SaveScheduleValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Frequency).Must(f => ReportScheduleRules.Frequencies.Contains(f ?? "")).WithMessage("Choose Daily, Weekly or Monthly.");
        RuleFor(x => x.Hour).InclusiveBetween(0, 23);
        RuleFor(x => x.Attach).Must(a => string.IsNullOrEmpty(a) || ReportScheduleRules.AttachFormats.Contains(a, StringComparer.OrdinalIgnoreCase)).WithMessage("Attach CSV, PDF or both.");
        RuleFor(x => x.Day).InclusiveBetween(0, 28);
        RuleFor(x => x.Day).InclusiveBetween(0, 6).When(x => x.Frequency == "Weekly").WithMessage("Weekday must be 0 (Sunday) to 6.");
        RuleFor(x => x.Day).InclusiveBetween(1, 28).When(x => x.Frequency == "Monthly").WithMessage("Day of month must be 1 to 28.");
        RuleFor(x => x.Recipients).Custom((text, ctx) =>
        {
            var list = ReportScheduleRules.ParseRecipients(text);
            if (list.Count == 0) ctx.AddFailure("Add at least one e-mail address.");
            if (list.Count > ReportScheduleRules.MaxRecipients) ctx.AddFailure($"At most {ReportScheduleRules.MaxRecipients} recipients.");
            foreach (var r in list) if (!ReportScheduleRules.IsEmail(r)) ctx.AddFailure($"Not a valid e-mail address: {r}");
        });
    }
}

public class SaveScheduleHandler : IRequestHandler<SaveScheduleRequest, SaveScheduleResult>
{
    private readonly ICommandRepository<ReportSchedule> _repository;
    private readonly IQueryContext _context;
    private readonly IUnitOfWork _unitOfWork;
    public SaveScheduleHandler(ICommandRepository<ReportSchedule> repository, IQueryContext context, IUnitOfWork unitOfWork) { _repository = repository; _context = context; _unitOfWork = unitOfWork; }

    public async Task<SaveScheduleResult> Handle(SaveScheduleRequest request, CancellationToken cancellationToken)
    {
        var owns = await _context.ReportDefinition.AsNoTracking().AnyAsync(x => !x.IsDeleted && x.Id == request.ReportId && x.OwnerUserId == request.UserId, cancellationToken);
        if (!owns) throw new Exception("Only the owner of a report can schedule it.");

        var row = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.ReportId == request.ReportId && x.OwnerUserId == request.UserId, cancellationToken);
        var creating = row == null;
        row ??= new ReportSchedule { CreatedById = request.UserId, OwnerUserId = request.UserId, ReportId = request.ReportId, LastRunAtUtc = DateTime.UtcNow };
        row.Frequency = request.Frequency;
        row.Day = request.Frequency == "Daily" ? 0 : request.Day;
        row.Hour = request.Hour;
        row.Recipients = string.Join(", ", ReportScheduleRules.ParseRecipients(request.Recipients));
        row.IsActive = request.IsActive;
        row.Attach = ReportScheduleRules.NormalizeAttach(request.Attach);
        row.UpdatedById = request.UserId;
        if (creating) await _repository.CreateAsync(row, cancellationToken); else _repository.Update(row);
        await _unitOfWork.SaveAsync(cancellationToken);
        return new SaveScheduleResult { Id = row.Id };
    }
}

public class DeleteScheduleRequest : IRequest<bool> { public string? ReportId { get; init; } public string? UserId { get; set; } }

public class DeleteScheduleHandler : IRequestHandler<DeleteScheduleRequest, bool>
{
    private readonly ICommandRepository<ReportSchedule> _repository;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteScheduleHandler(ICommandRepository<ReportSchedule> repository, IUnitOfWork unitOfWork) { _repository = repository; _unitOfWork = unitOfWork; }

    public async Task<bool> Handle(DeleteScheduleRequest request, CancellationToken cancellationToken)
    {
        var row = await _repository.GetQuery().FirstOrDefaultAsync(x => !x.IsDeleted && x.ReportId == request.ReportId && x.OwnerUserId == request.UserId, cancellationToken);
        if (row == null) return true;
        row.UpdatedById = request.UserId;
        _repository.Delete(row);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
}
