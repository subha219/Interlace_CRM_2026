using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SupportCaseManager;

public static class SupportCaseSla
{
    public static int GetResolutionHours(CasePriority? priority)
    {
        return priority switch
        {
            CasePriority.Critical => 4,
            CasePriority.High => 8,
            CasePriority.Normal => 24,
            CasePriority.Low => 72,
            _ => 24
        };
    }

    public static DateTime CalculateDueDate(CasePriority? priority, DateTime? from = null)
    {
        return (from ?? DateTime.Now).AddHours(GetResolutionHours(priority));
    }
}

/// <summary>Company SLA targets per priority, falling back to the built-in defaults.</summary>
public class SlaConfig
{
    public static int DefaultFirstResponseHours(CasePriority? priority) => priority switch
    {
        CasePriority.Critical => 1,
        CasePriority.High => 2,
        CasePriority.Normal => 8,
        CasePriority.Low => 24,
        _ => 8
    };

    private readonly Dictionary<CasePriority, SlaPolicyRow> _rows = new();
    public record SlaPolicyRow(int FirstResponseHours, int ResolutionHours, string? EscalationUserId);

    public static async Task<SlaConfig> LoadAsync(IEntityDbSet context, CancellationToken cancellationToken)
    {
        var config = new SlaConfig();
        var rows = await context.SlaPolicy.AsNoTracking().Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var r in rows) config._rows[r.Priority] = new SlaPolicyRow(r.FirstResponseHours, r.ResolutionHours, r.EscalationUserId);
        return config;
    }

    public int ResolutionHours(CasePriority? priority) =>
        priority != null && _rows.TryGetValue(priority.Value, out var r) ? r.ResolutionHours : SupportCaseSla.GetResolutionHours(priority);

    public int FirstResponseHours(CasePriority? priority) =>
        priority != null && _rows.TryGetValue(priority.Value, out var r) ? r.FirstResponseHours : DefaultFirstResponseHours(priority);

    public string? EscalationUser(CasePriority? priority) =>
        priority != null && _rows.TryGetValue(priority.Value, out var r) ? r.EscalationUserId : null;

    public DateTime ResolutionDue(CasePriority? priority, DateTime? from = null) => (from ?? DateTime.Now).AddHours(ResolutionHours(priority));
    public DateTime FirstResponseDueAt(CasePriority? priority, DateTime? from = null) => (from ?? DateTime.Now).AddHours(FirstResponseHours(priority));
}
