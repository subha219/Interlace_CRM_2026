using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InterlaceUI.BackEnd.Common.Health;

/// <summary>Reports unhealthy when the application cannot reach its database.</summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DataContext _context;

    public DatabaseHealthCheck(DataContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database reachable")
                : HealthCheckResult.Unhealthy("Database not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}
