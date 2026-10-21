using System.Text.Json;
using Application.Common.Services.IntegrationManager;
using Application.Features.ActivityManager.Commands;
using Application.Features.EmailManager.Commands;
using Application.Features.WorkflowManager;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.IntegrationManager;

/// <summary>
/// Runs workflow rules for each business event. Rules run one after another, at most 20 per event,
/// and changes made by a rule never raise further events (no loops).
/// </summary>
public class WorkflowDispatcher : BackgroundService
{
    private const int MaxRulesPerEvent = 20;

    private readonly WebhookPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowDispatcher> _logger;

    public WorkflowDispatcher(WebhookPublisher publisher, IServiceScopeFactory scopeFactory, ILogger<WorkflowDispatcher> logger)
    {
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in _publisher.WorkflowReader.ReadAllAsync(stoppingToken))
            {
                try { await HandleAsync(evt, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logger.LogError(ex, "Workflow handling failed for {Event}", evt.Name); }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static Dictionary<string, string?> ToPayload(object data)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var element = JsonSerializer.SerializeToElement(data);
        if (element.ValueKind != JsonValueKind.Object) return result;
        foreach (var p in element.EnumerateObject())
        {
            result[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => p.Value.GetString(),
                _ => p.Value.ToString()
            };
        }
        return result;
    }

    private async Task HandleAsync(WebhookEvent evt, CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        var rules = await context.WorkflowRule.Where(x => !x.IsDeleted && x.IsActive && x.TriggerEvent == evt.Name)
            .OrderBy(x => x.CreatedAtUtc).Take(MaxRulesPerEvent).ToListAsync(token);
        if (rules.Count == 0) return;

        var payload = ToPayload(evt.Data);
        var entity = WorkflowModel.EntityOf(evt.Name);
        var label = payload.GetValueOrDefault("title") ?? payload.GetValueOrDefault("name") ?? payload.GetValueOrDefault("number");

        foreach (var rule in rules)
        {
            List<WorkflowCondition> conditions;
            try { conditions = WorkflowModel.ParseConditions(rule.ConditionsJson); }
            catch (JsonException) { continue; }
            if (!WorkflowModel.Matches(conditions, payload)) continue;

            var details = new List<string>();
            var success = true;
            WorkflowGuard.Active = true;
            try
            {
                foreach (var action in WorkflowModel.ParseActions(rule.ActionsJson))
                {
                    try { details.Add(await RunActionAsync(scope.ServiceProvider, context, entity, payload, action, token)); }
                    catch (Exception ex)
                    {
                        success = false;
                        details.Add($"{WorkflowModel.Str(action, "type")}: failed - {ex.GetBaseException().Message}");
                    }
                }
            }
            finally
            {
                WorkflowGuard.Active = false;
            }

            var text = string.Join(" | ", details);
            context.WorkflowRun.Add(new WorkflowRun
            {
                RuleId = rule.Id, RuleName = rule.Name, EventName = evt.Name, RecordId = payload.GetValueOrDefault("id"), RecordLabel = label,
                Success = success, Detail = text.Length > 1900 ? text.Substring(0, 1900) : text, RunAtUtc = DateTime.UtcNow
            });
            rule.RunCount++;
            rule.LastRunAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(token);
        }
    }

    private static string? ResolveUser(string? spec, Dictionary<string, string?> payload) =>
        string.IsNullOrWhiteSpace(spec) || spec == "owner" ? payload.GetValueOrDefault("owner") : spec;

    private static async Task<string> RunActionAsync(IServiceProvider sp, DataContext context, string entity, Dictionary<string, string?> payload,
        Dictionary<string, JsonElement> action, CancellationToken token)
    {
        var type = WorkflowModel.Str(action, "type");
        var recordId = payload.GetValueOrDefault("id");
        var recordName = payload.GetValueOrDefault("title") ?? payload.GetValueOrDefault("name");

        switch (type)
        {
            case "create_activity":
            {
                var days = int.TryParse(WorkflowModel.Str(action, "dueInDays"), out var d) ? Math.Clamp(d, 0, 365) : 1;
                var assignee = ResolveUser(WorkflowModel.Str(action, "assignTo"), payload);
                var due = DateTime.Now.Date.AddDays(days).AddHours(9);
                await sp.GetRequiredService<ISender>().Send(new SaveActivityRequest
                {
                    Subject = WorkflowModel.Render(WorkflowModel.Str(action, "subject"), payload),
                    Description = "Created by a workflow rule.",
                    Type = Enum.Parse<ActivityType>(WorkflowModel.Str(action, "activityType") ?? "Task"),
                    Priority = "Normal",
                    StartAt = due,
                    EndAt = due,
                    RelatedType = entity,
                    RelatedId = recordId,
                    RelatedName = recordName,
                    AssignedToUserId = assignee,
                    UserId = assignee ?? payload.GetValueOrDefault("owner")
                }, token);
                return $"Activity created (due in {days} day(s))";
            }
            case "notify":
            {
                var user = ResolveUser(WorkflowModel.Str(action, "to"), payload);
                if (string.IsNullOrEmpty(user)) return "Notification skipped: the record has no owner";
                context.Notification.Add(new Notification
                {
                    Title = WorkflowModel.Render(WorkflowModel.Str(action, "title") ?? "Workflow", payload),
                    Message = WorkflowModel.Render(WorkflowModel.Str(action, "message"), payload),
                    Type = "workflow",
                    UserId = user,
                    Url = entity == "Lead" ? "/Leads/LeadList" : entity == "Customer" ? "/Customers/CustomerList" : "/SupportCases/SupportCaseList"
                });
                await context.SaveChangesAsync(token);
                return "Notification sent";
            }
            case "set_owner":
            {
                var user = WorkflowModel.Str(action, "userId");
                if (string.IsNullOrEmpty(recordId)) return "Owner not changed: no record";
                switch (entity)
                {
                    case "Lead": { var x = await context.Lead.FirstOrDefaultAsync(l => l.Id == recordId, token); if (x != null) x.OwnerUserId = user; break; }
                    case "Customer": { var x = await context.Customer.FirstOrDefaultAsync(l => l.Id == recordId, token); if (x != null) x.OwnerUserId = user; break; }
                    case "SupportCase": { var x = await context.SupportCase.FirstOrDefaultAsync(l => l.Id == recordId, token); if (x != null) x.OwnerUserId = user; break; }
                }
                await context.SaveChangesAsync(token);
                return "Owner changed";
            }
            case "add_tag":
            {
                var tag = WorkflowModel.Str(action, "tag")!.Trim();
                if (string.IsNullOrEmpty(recordId)) return "Tag not added: no record";
                static string Merge(string? tags, string tag)
                {
                    var list = (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    if (!list.Contains(tag, StringComparer.OrdinalIgnoreCase)) list.Add(tag);
                    return string.Join(", ", list);
                }
                if (entity == "Lead") { var x = await context.Lead.FirstOrDefaultAsync(l => l.Id == recordId, token); if (x != null) x.Tags = Merge(x.Tags, tag); }
                else if (entity == "Customer") { var x = await context.Customer.FirstOrDefaultAsync(l => l.Id == recordId, token); if (x != null) x.Tags = Merge(x.Tags, tag); }
                await context.SaveChangesAsync(token);
                return $"Tag \"{tag}\" added";
            }
            case "send_email":
            {
                var to = payload.GetValueOrDefault("email");
                if (string.IsNullOrWhiteSpace(to)) return "E-mail skipped: the record has no e-mail address";
                var result = await sp.GetRequiredService<ISender>().Send(new SendCrmEmailRequest
                {
                    To = to,
                    Subject = WorkflowModel.Render(WorkflowModel.Str(action, "subject"), payload),
                    Body = WorkflowModel.Render(WorkflowModel.Str(action, "body"), payload),
                    RelatedType = entity,
                    RelatedId = recordId,
                    RelatedName = recordName,
                    IsMarketing = string.Equals(WorkflowModel.Str(action, "marketing"), "true", StringComparison.OrdinalIgnoreCase),
                    UserId = payload.GetValueOrDefault("owner"),
                    UserName = "Workflow"
                }, token);
                return result.Success ? $"E-mail sent to {to}" : $"E-mail not sent: {result.Error}";
            }
            default:
                return $"Unknown action {type}";
        }
    }
}
