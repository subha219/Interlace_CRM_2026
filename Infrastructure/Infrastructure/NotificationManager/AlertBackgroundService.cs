using Domain.Entities;
using Domain.Enums;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.NotificationManager;

/// <summary>
/// Raises in-app alerts for overdue invoices, SLA breaches, stale leads and expiring quotations.
/// Each alert is created once (DedupKey), so running often is safe.
/// </summary>
public class AlertBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertBackgroundService> _logger;

    public AlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //>>> give the application (and database creation / seeding) time to start
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();
                var created = await RunAsync(context, stoppingToken);
                if (created > 0)
                {
                    _logger.LogInformation("AlertBackgroundService created {Count} notification(s)", created);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AlertBackgroundService run failed");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    public static async Task<int> RunAsync(DataContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var today = now.Date;
        var pending = new List<Notification>();

        var existing = (await context.Notification.AsNoTracking()
            .Where(x => x.DedupKey != null)
            .Select(x => x.DedupKey!)
            .ToListAsync(cancellationToken)).ToHashSet();

        void Add(string key, string title, string message, string type, string url, string? userId = null)
        {
            if (!existing.Add(key))
            {
                return;
            }

            pending.Add(new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                DedupKey = key,
                UserId = userId,
                IsRead = false,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        //>>> overdue invoices
        var overdue = await context.Invoice.AsNoTracking()
            .Where(x => !x.IsDeleted && x.DueDate != null && x.DueDate < today
                        && (x.BalanceDue ?? 0) > 0
                        && x.InvoiceStatus != InvoiceStatus.Paid && x.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(x => new { x.Id, x.Number, x.DueDate, x.BalanceDue })
            .ToListAsync(cancellationToken);
        foreach (var x in overdue)
        {
            Add("invoice-overdue:" + x.Id, "Invoice overdue",
                $"{x.Number} was due on {x.DueDate:yyyy-MM-dd}. Balance due: {x.BalanceDue:N2}", "Danger", "/Invoices/InvoiceList");
        }

        //>>> support cases past their SLA (resolution) or without a first reply in time
        var sla = await Application.Features.SupportCaseManager.SlaConfig.LoadAsync(context, cancellationToken);
        var breached = await context.SupportCase.AsNoTracking()
            .Where(x => !x.IsDeleted && x.DueDate != null && x.DueDate < now
                        && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed)
            .Select(x => new { x.Id, x.Number, x.Title, x.DueDate, x.Priority })
            .ToListAsync(cancellationToken);
        foreach (var x in breached)
        {
            Add("case-sla:" + x.Id, "Support SLA breached",
                $"{x.Number} \"{x.Title}\" passed its SLA on {x.DueDate:yyyy-MM-dd HH:mm}", "Danger", "/SupportCases/SupportCaseList");

            var boss = sla.EscalationUser(x.Priority);
            if (!string.IsNullOrEmpty(boss))
            {
                Add("case-escalate:" + x.Id, "Support case escalated",
                    $"{x.Number} \"{x.Title}\" is overdue (due {x.DueDate:yyyy-MM-dd HH:mm}) and needs attention.", "Danger", "/SupportCases/SupportCaseList", boss);
            }
        }

        var unanswered = await context.SupportCase.AsNoTracking()
            .Where(x => !x.IsDeleted && x.FirstResponseDue != null && x.FirstResponseDue < now && x.FirstRespondedAt == null
                        && x.Status != CaseStatus.Resolved && x.Status != CaseStatus.Closed)
            .Select(x => new { x.Id, x.Number, x.Title, x.FirstResponseDue })
            .ToListAsync(cancellationToken);
        foreach (var x in unanswered)
        {
            Add("case-first:" + x.Id, "No first response yet",
                $"{x.Number} \"{x.Title}\" should have had a first reply by {x.FirstResponseDue:yyyy-MM-dd HH:mm}", "Warning", "/SupportCases/SupportCaseList");
        }

        //>>> leads whose estimated closing date has passed
        var staleLeads = await context.Lead.AsNoTracking()
            .Where(x => !x.IsDeleted && x.DateClosingEstimation != null && x.DateClosingEstimation < today
                        && x.ClosingStatus != ClosingStatus.ClosedWon && x.ClosingStatus != ClosingStatus.ClosedLost
                        && x.PipelineStage != PipelineStage.Closed)
            .Select(x => new { x.Id, x.Number, x.Title, x.DateClosingEstimation })
            .ToListAsync(cancellationToken);
        foreach (var x in staleLeads)
        {
            Add("lead-stale:" + x.Id, "Lead is past its closing date",
                $"{x.Number} \"{x.Title}\" was expected to close on {x.DateClosingEstimation:yyyy-MM-dd}", "Warning", "/Pipelines/PipelineBoard");
        }

        //>>> quotations expiring within 3 days
        var limit = today.AddDays(3);
        var expiring = await context.Quotation.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ValidUntil != null && x.ValidUntil >= today && x.ValidUntil <= limit
                        && (x.QuotationStatus == QuotationStatus.Draft || x.QuotationStatus == QuotationStatus.Sent))
            .Select(x => new { x.Id, x.Number, x.ValidUntil })
            .ToListAsync(cancellationToken);
        foreach (var x in expiring)
        {
            Add("quotation-expiring:" + x.Id, "Quotation expiring soon",
                $"{x.Number} is valid until {x.ValidUntil:yyyy-MM-dd}", "Warning", "/Quotations/QuotationList");
        }

        //>>> contracts: renewal notice window reached, and contracts that ended without renewal
        var contractRows = await context.Contract.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == "Active" && x.EndDate != null)
            .Select(x => new { x.Id, x.Number, x.Title, x.EndDate, x.RenewalNoticeDays, x.AutoRenew, x.OwnerUserId })
            .ToListAsync(cancellationToken);
        foreach (var x in contractRows)
        {
            var end = x.EndDate!.Value.Date;
            if (end < today)
            {
                Add("contract-expired:" + x.Id, "Contract expired", $"{x.Number} \"{x.Title}\" ended on {end:yyyy-MM-dd} and was not renewed.", "Danger", "/Contracts/ContractList", x.OwnerUserId);
            }
            else if ((end - today).TotalDays <= x.RenewalNoticeDays)
            {
                Add("contract-renew:" + x.Id, "Contract up for renewal", $"{x.Number} \"{x.Title}\" ends on {end:yyyy-MM-dd}" + (x.AutoRenew ? " (auto-renew is on)." : "."), "Warning", "/Contracts/ContractList", x.OwnerUserId);
            }
        }

        //>>> churn risk: customers whose health score is critical (once per month)
        var health = await Application.Features.CustomerHealthManager.CustomerHealthEngine.ComputeAsync(context, cancellationToken);
        foreach (var h in health.Where(x => x.Band == "Critical"))
        {
            Add($"churn:{h.CustomerId}:{today:yyyy-MM}", "Customer at risk of churning",
                $"{h.Name} has a health score of {h.Total}/100.", "Danger", "/Dashboards/CustomerHealth", h.OwnerUserId);
        }

        //>>> activities: remind the assignee when something is due within a day, and again when it is overdue
        var soon = now.AddHours(24);
        var due = await context.Activity.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == ActivityStatus.Open && x.AssignedToUserId != null
                        && (x.EndAt ?? x.StartAt) != null && (x.EndAt ?? x.StartAt) <= soon)
            .Select(x => new { x.Id, x.Number, x.Subject, x.AssignedToUserId, When = (x.EndAt ?? x.StartAt)!.Value })
            .ToListAsync(cancellationToken);
        foreach (var x in due)
        {
            if (x.When < now)
            {
                Add("activity-overdue:" + x.Id, "Activity overdue", $"{x.Number} \"{x.Subject}\" was due {x.When:yyyy-MM-dd HH:mm}", "Danger", "/Activities/ActivityList", x.AssignedToUserId);
            }
            else
            {
                Add("activity-due:" + x.Id, "Activity due soon", $"{x.Number} \"{x.Subject}\" is due {x.When:yyyy-MM-dd HH:mm}", "Warning", "/Activities/ActivityList", x.AssignedToUserId);
            }
        }

        if (pending.Count == 0)
        {
            return 0;
        }

        context.Notification.AddRange(pending);
        await context.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }
}
