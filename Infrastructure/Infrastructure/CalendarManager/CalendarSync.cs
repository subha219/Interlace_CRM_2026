using Application.Features.CalendarManager;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.CalendarManager;

/// <summary>Encrypts refresh tokens and signs the short-lived OAuth state value.</summary>
public class CalendarProtector
{
    private readonly IDataProtector _tokens;
    private readonly ITimeLimitedDataProtector _state;

    public CalendarProtector(IDataProtectionProvider provider)
    {
        _tokens = provider.CreateProtector("Interlace.Calendar.Tokens");
        _state = provider.CreateProtector("Interlace.Calendar.State").ToTimeLimitedDataProtector();
    }

    public string ProtectToken(string token) => _tokens.Protect(token);
    public string? UnprotectToken(string? protectedToken)
    {
        try { return string.IsNullOrEmpty(protectedToken) ? null : _tokens.Unprotect(protectedToken); } catch { return null; }
    }

    public string NewState(string userId) => _state.Protect(userId, TimeSpan.FromMinutes(10));
    public string? ReadState(string? state)
    {
        try { return string.IsNullOrEmpty(state) ? null : _state.Unprotect(state); } catch { return null; }
    }
}

/// <summary>One-way sync, CRM to Google: open Task, Call and Meeting activities with a start time become calendar events.</summary>
public class CalendarSyncRunner
{
    private readonly DataContext _db;
    private readonly GoogleCalendarClient _google;
    private readonly OutlookCalendarClient _outlook;
    private readonly CalendarProtector _protector;

    public CalendarSyncRunner(DataContext db, GoogleCalendarClient google, OutlookCalendarClient outlook, CalendarProtector protector)
    {
        _db = db; _google = google; _outlook = outlook; _protector = protector;
    }

    public async Task<string> SyncUserAsync(string userId, CancellationToken ct)
    {
        var conn = await _db.CalendarConnection.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == userId && x.IsActive, ct);
        if (conn == null) return "Not connected.";
        var isOutlook = conn.Provider == "Outlook";
        if (isOutlook ? !_outlook.IsConfigured : !_google.IsConfigured) return (isOutlook ? "Microsoft" : "Google") + " is not configured on the server.";

        string status;
        try
        {
            var refresh = _protector.UnprotectToken(conn.RefreshTokenProtected) ?? throw new GoogleAuthException("The saved access cannot be read. Please connect again.");
            string access;
            if (isOutlook)
            {
                var t = await _outlook.AccessTokenAsync(refresh, ct);
                access = t.AccessToken;
                if (t.NewRefreshToken != null) conn.RefreshTokenProtected = _protector.ProtectToken(t.NewRefreshToken);
            }
            else access = await _google.AccessTokenAsync(refresh, ct);

            var now = DateTime.Now;
            var from = now.AddDays(-1);
            var to = now.AddDays(90);
            var types = new[] { ActivityType.Task, ActivityType.Call, ActivityType.Meeting };

            var window = await _db.Activity.AsNoTracking()
                .Where(x => !x.IsDeleted && x.Status == ActivityStatus.Open && x.StartAt != null && x.StartAt >= from && x.StartAt <= to && x.Type != null && types.Contains(x.Type.Value)
                            && (x.AssignedToUserId == userId || (x.AssignedToUserId == null && x.OwnerUserId == userId)))
                .OrderBy(x => x.StartAt).Take(300).ToListAsync(ct);

            var links = await _db.CalendarEventLink.Where(x => !x.IsDeleted && x.UserId == userId).ToListAsync(ct);
            var linkedIds = links.Select(l => l.ActivityId!).ToList();
            var linked = await _db.Activity.AsNoTracking().Where(x => linkedIds.Contains(x.Id)).ToListAsync(ct);

            CalendarActivity Map(Activity a) => new(a.Id, a.Subject, a.Description, a.StartAt, a.EndAt, a.Status == ActivityStatus.Open, a.IsDeleted,
                a.AssignedToUserId == userId || (a.AssignedToUserId == null && a.OwnerUserId == userId));

            var plan = CalendarRules.Plan(window.Select(Map), linked.Select(Map), links.Select(l => new CalendarLink(l.Id, l.ActivityId!, l.ExternalEventId!, l.Hash)));

            foreach (var d in plan.Deletes)
            {
                if (isOutlook) await _outlook.DeleteEventAsync(access, d.EventId, ct);
                else await _google.DeleteEventAsync(access, d.EventId, ct);
                var link = links.First(l => l.Id == d.LinkId);
                link.IsDeleted = true;
            }
            var created = 0;
            var updated = 0;
            foreach (var u in plan.Upserts)
            {
                var id = isOutlook
                    ? await _outlook.UpsertEventAsync(access, u.ExistingEventId, CalendarRules.BuildOutlookEvent(u.Activity), ct)
                    : await _google.UpsertEventAsync(access, u.ExistingEventId, CalendarRules.BuildEvent(u.Activity), ct);
                if (u.ExistingLinkId == null)
                {
                    _db.CalendarEventLink.Add(new CalendarEventLink { ActivityId = u.Activity.Id, UserId = userId, ExternalEventId = id, Hash = u.Hash, SyncedAtUtc = DateTime.UtcNow });
                    created++;
                }
                else
                {
                    var link = links.First(l => l.Id == u.ExistingLinkId);
                    link.ExternalEventId = id; link.Hash = u.Hash; link.SyncedAtUtc = DateTime.UtcNow;
                    updated++;
                }
            }
            status = $"OK: {created} added, {updated} updated, {plan.Deletes.Count} removed.";
        }
        catch (GoogleAuthException ex)
        {
            conn.IsActive = false;
            status = ex.Message;
        }
        catch (Exception ex)
        {
            status = "Failed: " + ex.Message;
        }

        conn.LastSyncUtc = DateTime.UtcNow;
        conn.LastStatus = status.Length > 290 ? status[..290] : status;
        await _db.SaveChangesAsync(ct);
        return conn.LastStatus;
    }
}

public class CalendarSyncService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CalendarSyncService> _logger;

    public CalendarSyncService(IServiceScopeFactory scopeFactory, ILogger<CalendarSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                {
                    var users = await db.CalendarConnection.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).Select(x => x.UserId!).ToListAsync(stoppingToken);
                    foreach (var userId in users)
                    {
                        //>>> a fresh scope per user so one failure cannot poison the others
                        using var inner = _scopeFactory.CreateScope();
                        await inner.ServiceProvider.GetRequiredService<CalendarSyncRunner>().SyncUserAsync(userId, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "CalendarSyncService run failed"); }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
