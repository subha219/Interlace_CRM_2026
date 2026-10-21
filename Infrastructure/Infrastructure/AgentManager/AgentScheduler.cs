using Application.Features.AgentManager;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AgentManager;

/// <summary>
/// Runs the agents an administrator switched on and set to "run automatically", about once a day.
/// The agents only write suggestions; a person still approves each one.
/// </summary>
public class AgentScheduler : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentScheduler> _logger;

    public AgentScheduler(IServiceScopeFactory scopeFactory, ILogger<AgentScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "AgentScheduler run failed"); }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var keys = await db.AgentSetting.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Enabled && x.AutoRun == true).Select(x => x.AgentKey).ToListAsync(ct);

        foreach (var key in keys)
        {
            if (AgentRules.Find(key) == null) continue;
            var last = await db.AgentRun.AsNoTracking()
                .Where(x => !x.IsDeleted && x.AgentKey == key && x.RunByName == AgentRules.AutoRunName)
                .OrderByDescending(x => x.StartedAtUtc).Select(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
            if (!AgentRules.IsAutoDue(last, DateTime.UtcNow)) continue;

            try { await sender.Send(new RunAgentRequest { AgentKey = key, UserId = null, UserName = AgentRules.AutoRunName }, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _logger.LogWarning("Automatic run of agent {Agent} failed: {Type}", key, ex.GetType().Name); }
        }
    }
}
