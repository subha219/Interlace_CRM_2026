using System.Security.Claims;
using System.Text.Json;
using Domain.Common;
using Application.Common.Services.IntegrationManager;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.DataAccessManager.EFCore.Interceptors;

/// <summary>
/// Writes one AuditLog row per created, updated or soft-deleted business entity.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredProperties = new()
    {
        nameof(BaseEntity.UpdatedAtUtc),
        nameof(BaseEntity.UpdatedById),
        nameof(BaseEntity.CreatedAtUtc),
        nameof(BaseEntity.CreatedById),
        //>>> never write secrets to the audit trail
        nameof(EmailSetting.PasswordProtected),
        nameof(WebhookEndpoint.Secret),
        nameof(ApiKey.KeyHash)
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebhookPublisher _webhooks;

    //>>> events found while saving, published only once the save succeeded
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, List<WebhookEvent>> Pending = new();

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor, IWebhookPublisher webhooks)
    {
        _httpContextAccessor = httpContextAccessor;
        _webhooks = webhooks;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null) Pending.Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) Pending.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Flush(DbContext? context)
    {
        if (context == null || !Pending.TryGetValue(context, out var list)) return;
        Pending.Remove(context);
        foreach (var e in list) _webhooks.Publish(e);
    }

    /// <summary>Notes business events (new lead, won, lost, new customer, new case) for the webhook dispatcher.</summary>
    private static void CollectWebhookEvents(DbContext context)
    {
        //>>> changes made by a workflow rule never start another rule
        if (Infrastructure.IntegrationManager.WorkflowGuard.Active) return;

        var events = new List<WebhookEvent>();

        foreach (var entry in context.ChangeTracker.Entries<Lead>())
        {
            var l = entry.Entity;
            object Payload() => new { id = l.Id, number = l.Number, title = l.Title, company = l.CompanyName, email = l.CompanyEmail, stage = l.PipelineStage?.ToString(), amount = l.ClosingStatus == ClosingStatus.ClosedWon ? l.AmountClosed : l.AmountTargeted, source = l.LeadSource, owner = l.OwnerUserId };

            if (entry.State == EntityState.Added)
            {
                events.Add(new WebhookEvent("lead.created", Payload()));
            }
            else if (entry.State == EntityState.Modified)
            {
                var closing = entry.Property(nameof(Lead.ClosingStatus));
                var stage = entry.Property(nameof(Lead.PipelineStage));
                if (!Equals(stage.OriginalValue, stage.CurrentValue))
                {
                    events.Add(new WebhookEvent("lead.stage_changed", new { id = l.Id, number = l.Number, title = l.Title, company = l.CompanyName, email = l.CompanyEmail, stage = l.PipelineStage?.ToString(), previousStage = (stage.OriginalValue as PipelineStage?)?.ToString(), amount = l.AmountTargeted, source = l.LeadSource, owner = l.OwnerUserId }));
                }
                if (!Equals(closing.OriginalValue, closing.CurrentValue))
                {
                    if (l.ClosingStatus == ClosingStatus.ClosedWon) events.Add(new WebhookEvent("lead.won", Payload()));
                    else if (l.ClosingStatus == ClosingStatus.ClosedLost) events.Add(new WebhookEvent("lead.lost", Payload()));
                }
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<Customer>().Where(x => x.State == EntityState.Added))
        {
            var c = entry.Entity;
            events.Add(new WebhookEvent("customer.created", new { id = c.Id, number = c.Number, name = c.Name, email = c.EmailAddress, source = c.Source, owner = c.OwnerUserId }));
        }

        foreach (var entry in context.ChangeTracker.Entries<SupportCase>().Where(x => x.State == EntityState.Added))
        {
            var s = entry.Entity;
            events.Add(new WebhookEvent("case.created", new { id = s.Id, number = s.Number, title = s.Title, priority = s.Priority?.ToString(), customerId = s.CustomerId, owner = s.OwnerUserId }));
        }

        if (events.Count > 0) Pending.AddOrUpdate(context, events);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context == null)
        {
            return;
        }

        CaptureStageHistory(context);
        CollectWebhookEvents(context);

        //>>> only user-driven changes are audited (seeders and background jobs have no HTTP context)
        if (_httpContextAccessor.HttpContext == null)
        {
            return;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user?.Identity?.Name ?? user?.FindFirstValue(ClaimTypes.Name);

        //>>> a new record is owned by the user who creates it unless an owner was set explicitly
        var ownerId = userId ?? user?.FindFirstValue("sub");
        if (!string.IsNullOrEmpty(ownerId))
        {
            foreach (var added in context.ChangeTracker.Entries<IHasOwner>().Where(x => x.State == EntityState.Added))
            {
                if (string.IsNullOrEmpty(added.Entity.OwnerUserId))
                {
                    added.Entity.OwnerUserId = ownerId;
                }
            }
        }

        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is not BaseEntity entity || entry.Entity is AuditLog || entry.Entity is Notification || entry.Entity is NumberSequence || entry.Entity is Token
                || entry.Entity is CalendarConnection || entry.Entity is MailConnection || entry.Entity is EmailMessage || entry.Entity is CalendarEventLink || entry.Entity is PortalSession || entry.Entity is AiUsageLog)
            {
                continue;
            }

            string action;
            List<object> changes;

            if (entry.State == EntityState.Added)
            {
                action = "Created";
                changes = new List<object>();
            }
            else if (entry.State == EntityState.Modified)
            {
                changes = BuildChanges(entry);
                var deletedNow = entry.Property(nameof(BaseEntity.IsDeleted)).IsModified
                                 && entity.IsDeleted
                                 && !Equals(entry.Property(nameof(BaseEntity.IsDeleted)).OriginalValue, true);
                if (deletedNow)
                {
                    action = "Deleted";
                }
                else if (changes.Count == 0)
                {
                    continue;
                }
                else
                {
                    action = "Updated";
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                action = "Deleted";
                changes = new List<object>();
            }
            else
            {
                continue;
            }

            var json = changes.Count == 0 ? null : JsonSerializer.Serialize(changes);
            if (json != null && json.Length > 4000)
            {
                json = json.Substring(0, 4000);
            }

            logs.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = entity.Id,
                Action = action,
                UserId = userId,
                UserName = userName,
                Changes = json,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedById = userId
            });
        }

        if (logs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(logs);
        }
    }

    /// <summary>Adds a LeadStageHistory row whenever a lead is created or its stage / closing result changes.</summary>
    private void CaptureStageHistory(DbContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var rows = new List<LeadStageHistory>();

        foreach (var entry in context.ChangeTracker.Entries<Lead>().ToList())
        {
            var lead = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                rows.Add(new LeadStageHistory
                {
                    LeadId = lead.Id, ToStage = lead.PipelineStage, ToClosing = lead.ClosingStatus, Amount = lead.AmountTargeted,
                    OwnerUserId = lead.OwnerUserId ?? userId, ChangedByUserId = userId, ChangedAtUtc = DateTime.UtcNow, CreatedById = userId
                });
            }
            else if (entry.State == EntityState.Modified)
            {
                var stage = entry.Property(nameof(Lead.PipelineStage));
                var closing = entry.Property(nameof(Lead.ClosingStatus));
                if (Equals(stage.OriginalValue, stage.CurrentValue) && Equals(closing.OriginalValue, closing.CurrentValue))
                {
                    continue;
                }

                rows.Add(new LeadStageHistory
                {
                    LeadId = lead.Id,
                    FromStage = stage.OriginalValue as PipelineStage?, ToStage = lead.PipelineStage,
                    FromClosing = closing.OriginalValue as ClosingStatus?, ToClosing = lead.ClosingStatus,
                    Amount = lead.ClosingStatus == ClosingStatus.ClosedWon ? lead.AmountClosed : lead.AmountTargeted,
                    OwnerUserId = lead.OwnerUserId, ChangedByUserId = userId, ChangedAtUtc = DateTime.UtcNow, CreatedById = userId
                });
            }
        }

        if (rows.Count > 0)
        {
            context.Set<LeadStageHistory>().AddRange(rows);
        }
    }

    private static List<object> BuildChanges(EntityEntry entry)
    {
        var list = new List<object>();

        foreach (var property in entry.Properties)
        {
            if (!property.IsModified || IgnoredProperties.Contains(property.Metadata.Name))
            {
                continue;
            }

            if (Equals(property.OriginalValue, property.CurrentValue))
            {
                continue;
            }

            list.Add(new
            {
                field = property.Metadata.Name,
                old = Short(property.OriginalValue),
                @new = Short(property.CurrentValue)
            });
        }

        return list;
    }

    private static string? Short(object? value)
    {
        var text = value?.ToString();
        return text != null && text.Length > 200 ? text.Substring(0, 200) + "..." : text;
    }
}
