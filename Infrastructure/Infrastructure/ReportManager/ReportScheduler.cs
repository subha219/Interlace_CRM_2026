using System.Text;
using Application.Common.Services.EmailManager;
using Application.Features.ReportManager;
using Application.Features.SecurityManager.Queries;
using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Infrastructure.SecurityManager.AspNetIdentity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ReportManager;

/// <summary>
/// Sends scheduled reports by e-mail. A report runs only while its owner still holds the module role for the
/// report's record type; otherwise the schedule records "owner no longer has access" and sends nothing.
/// </summary>
public class ReportScheduler : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportScheduler> _logger;

    public ReportScheduler(IServiceScopeFactory scopeFactory, ILogger<ReportScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "ReportScheduler run failed"); }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var now = DateTime.Now;

        var schedules = await db.ReportSchedule.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(ct);
        foreach (var s in schedules)
        {
            if (!ReportScheduleRules.IsDue(s.Frequency, s.Day, s.Hour, s.LastRunAtUtc, now)) continue;

            //>>> mark first, so a slow or failing send is never repeated every 15 minutes
            s.LastRunAtUtc = DateTime.UtcNow;
            s.LastStatus = await SendAsync(scope.ServiceProvider, db, s, ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<string> SendAsync(IServiceProvider sp, DataContext db, ReportSchedule s, CancellationToken ct)
    {
        try
        {
            var report = await db.ReportDefinition.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == s.ReportId, ct);
            if (report == null) return "The report no longer exists.";
            if (report.OwnerUserId != s.OwnerUserId) return "The report changed owner.";

            if (!ReportCatalog.ModuleRole.TryGetValue(report.Entity ?? "", out var role)) return "Unknown record type.";
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = await users.FindByIdAsync(s.OwnerUserId ?? "");
            if (owner == null || owner.IsBlocked == true) return "The owner is not an active user.";
            if (!await users.IsInRoleAsync(owner, role)) return "The owner no longer has access to this record type.";

            var request = ReportScheduleRules.ToRequest(report.DefinitionJson, report.Entity);
            if (request == null) return "The saved report could not be read.";

            var sender = sp.GetRequiredService<ISender>();
            var result = await sender.Send(request, ct);

            //>>> user columns hold ids; show names
            var userColumns = result.Columns.Select((c, i) => (c, i)).Where(x => x.c.Type == "user").Select(x => x.i).ToList();
            if (userColumns.Count > 0)
            {
                var list = await sender.Send(new GetUserListRequest(), ct);
                var names = (list.Data ?? new()).Where(u => u.Id != null).ToDictionary(u => u.Id!, u => $"{u.FirstName} {u.LastName}".Trim());
                foreach (var row in result.Rows)
                    foreach (var i in userColumns)
                    {
                        var id = row[i]?.ToString();
                        row[i] = string.IsNullOrEmpty(id) || id == "(none)" ? "(none)" : names.GetValueOrDefault(id, "(unknown)");
                    }
            }

            var email = sp.GetRequiredService<IEmailService>();
            var html = ReportScheduleRules.BuildHtml(report.Name ?? "Report", result);
            var attach = ReportScheduleRules.NormalizeAttach(s.Attach);
            var files = new List<EmailAttachment>();
            var tooBig = false;
            if (attach is "Csv" or "Both")
            {
                var csv = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ReportScheduleRules.BuildCsv(result))).ToArray();
                if (csv.Length <= ReportScheduleRules.MaxCsvBytes) files.Add(new EmailAttachment(ReportScheduleRules.SafeFileName(report.Name), "text/csv", csv)); else tooBig = true;
            }
            if (attach is "Pdf" or "Both")
            {
                var pdf = ReportPdf.Build(report.Name ?? "Report", result);
                if (pdf.Length <= ReportScheduleRules.MaxCsvBytes) files.Add(new EmailAttachment(ReportScheduleRules.SafeFileName(report.Name, ".pdf"), "application/pdf", pdf)); else tooBig = true;
            }
            var attachments = files.Count > 0 ? files.ToArray() : null;
            if (tooBig) html += "<p>The full data was too large to attach. Open the report in the CRM to export it.</p>";
            var failed = new List<string>();
            foreach (var to in ReportScheduleRules.ParseRecipients(s.Recipients).Take(ReportScheduleRules.MaxRecipients))
            {
                var r = await email.SendEmailWithResultAsync(to, $"Report: {report.Name}", html, null, null, attachments);
                if (!r.Success) failed.Add(to);
            }
            return failed.Count == 0 ? "Sent" : "Could not send to: " + string.Join(", ", failed);
        }
        catch (Exception ex)
        {
            return "Failed: " + ex.GetType().Name;
        }
    }
}
