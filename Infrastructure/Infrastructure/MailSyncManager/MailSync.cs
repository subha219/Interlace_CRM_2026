using System.Text.Json;
using Application.Features.MailSyncManager;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.CalendarManager;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MailSyncManager;

public record MailTarget(string Type, string Id, string Name);

/// <summary>
/// Read-only Gmail to CRM sync. A message is kept only when the other side is a known customer, lead or one of their contacts;
/// then only its subject and a short preview are saved, and an Email activity is added to that record.
/// </summary>
public class MailSyncRunner
{
    public const string Scope = "openid email https://www.googleapis.com/auth/gmail.readonly";
    private const string Api = "https://gmail.googleapis.com/gmail/v1/users/me/messages";

    private readonly DataContext _db;
    private readonly GoogleCalendarClient _google;
    private readonly CalendarProtector _protector;
    private readonly NumberSequenceService _numbers;

    public MailSyncRunner(DataContext db, GoogleCalendarClient google, CalendarProtector protector, NumberSequenceService numbers)
    {
        _db = db; _google = google; _protector = protector; _numbers = numbers;
    }

    private async Task<Dictionary<string, MailTarget>> BuildTargetsAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, MailTarget>();
        void Add(string? email, string type, string id, string? name)
        {
            var key = (email ?? "").Trim().ToLowerInvariant();
            if (key.Length > 0) map.TryAdd(key, new MailTarget(type, id, name ?? type));
        }

        var customers = await _db.Customer.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Id, x.Name, x.EmailAddress }).ToListAsync(ct);
        var customerNames = customers.ToDictionary(x => x.Id, x => x.Name);
        foreach (var c in customers) Add(c.EmailAddress, "Customer", c.Id, c.Name);
        foreach (var c in await _db.CustomerContact.AsNoTracking().Where(x => !x.IsDeleted && x.EmailAddress != null && x.CustomerId != null).Select(x => new { x.CustomerId, x.EmailAddress }).ToListAsync(ct))
            Add(c.EmailAddress, "Customer", c.CustomerId!, customerNames.GetValueOrDefault(c.CustomerId!));

        var leads = await _db.Lead.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Id, x.CompanyName, x.Title, x.CompanyEmail }).ToListAsync(ct);
        var leadNames = leads.ToDictionary(x => x.Id, x => x.CompanyName ?? x.Title);
        foreach (var l in leads) Add(l.CompanyEmail, "Lead", l.Id, l.CompanyName ?? l.Title);
        foreach (var c in await _db.LeadContact.AsNoTracking().Where(x => !x.IsDeleted && x.Email != null && x.LeadId != null).Select(x => new { x.LeadId, x.Email }).ToListAsync(ct))
            Add(c.Email, "Lead", c.LeadId!, leadNames.GetValueOrDefault(c.LeadId!));
        return map;
    }

    public async Task<string> SyncUserAsync(string userId, CancellationToken ct)
    {
        var conn = await _db.MailConnection.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == userId && x.IsActive, ct);
        if (conn == null) return "Not connected.";
        if (!_google.IsConfigured) return "Google is not configured on the server.";

        string status;
        try
        {
            var refresh = _protector.UnprotectToken(conn.RefreshTokenProtected) ?? throw new GoogleAuthException("The saved access cannot be read. Please connect again.");
            var access = await _google.AccessTokenAsync(refresh, ct);
            var nowUtc = DateTime.UtcNow;
            var nowMs = new DateTimeOffset(nowUtc).ToUnixTimeMilliseconds();

            // 1. which messages are new since the last run
            var ids = new List<string>();
            string? page = null;
            for (var i = 0; i < 3 && ids.Count < MailRules.MaxMessagesPerRun; i++)
            {
                var url = $"{Api}?maxResults=100&q={Uri.EscapeDataString("after:" + MailRules.QueryAfterSeconds(conn.CursorMs, nowUtc))}" + (page == null ? "" : "&pageToken=" + Uri.EscapeDataString(page));
                using var list = await _google.GetJsonAsync(access, url, ct);
                if (list == null) break;
                if (list.RootElement.TryGetProperty("messages", out var msgs))
                    foreach (var m in msgs.EnumerateArray()) if (m.TryGetProperty("id", out var id) && id.GetString() is { } s) ids.Add(s);
                page = list.RootElement.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
                if (page == null) break;
            }
            ids = ids.Distinct().Take(MailRules.MaxMessagesPerRun).ToList();
            var known = (await _db.EmailMessage.AsNoTracking().Where(x => x.UserId == userId && x.GmailId != null && ids.Contains(x.GmailId)).Select(x => x.GmailId!).ToListAsync(ct)).ToHashSet();
            var fresh = ids.Where(i => !known.Contains(i)).ToList();

            // 2. keep only mail with people the CRM knows
            var saved = 0;
            if (fresh.Count > 0)
            {
                var targets = await BuildTargetsAsync(ct);
                var mine = (conn.Email ?? "").ToLowerInvariant();
                foreach (var id in fresh)
                {
                    using var doc = await _google.GetJsonAsync(access, $"{Api}/{Uri.EscapeDataString(id)}?format=metadata&metadataHeaders=From&metadataHeaders=To&metadataHeaders=Cc&metadataHeaders=Subject", ct);
                    if (doc == null) continue;
                    var root = doc.RootElement;
                    var labels = root.TryGetProperty("labelIds", out var l) ? l.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>();
                    if (MailRules.IsSkippable(labels)) continue;

                    string? Header(string name) => root.TryGetProperty("payload", out var p) && p.TryGetProperty("headers", out var hs)
                        ? hs.EnumerateArray().Where(h => string.Equals(h.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase)).Select(h => h.GetProperty("value").GetString()).FirstOrDefault() : null;
                    string? from = Header("From"), to = Header("To"), cc = Header("Cc"), subject = Header("Subject");

                    var direction = MailRules.Direction(from, mine);
                    var match = MailRules.Counterparties(direction, from, to, cc, mine).Select(a => targets.GetValueOrDefault(a)).FirstOrDefault(t => t != null);
                    if (match == null) continue;

                    var sentUtc = MailRules.FromInternalDate(root.TryGetProperty("internalDate", out var idt) ? idt.GetString() : null) ?? nowUtc;
                    var snippet = MailRules.Clip(root.TryGetProperty("snippet", out var sn) ? sn.GetString() : null, 500);
                    var local = sentUtc.ToLocalTime();

                    var activity = new Activity
                    {
                        Number = _numbers.GenerateNumber(nameof(Activity), "", "ACT"), Type = ActivityType.Email, Status = ActivityStatus.Done, Priority = "Normal",
                        Subject = MailRules.ActivitySubject(direction, match.Name, subject), Description = snippet, StartAt = local, EndAt = local, CompletedAt = local,
                        RelatedType = match.Type, RelatedId = match.Id, RelatedName = match.Name, OwnerUserId = userId, AssignedToUserId = userId, CreatedById = userId
                    };
                    _db.Activity.Add(activity);
                    _db.EmailMessage.Add(new EmailMessage
                    {
                        UserId = userId, GmailId = id, ThreadId = root.TryGetProperty("threadId", out var th) ? th.GetString() : null,
                        Subject = MailRules.Clip(subject, 300), FromAddress = MailRules.Clip(from, 255), ToAddresses = MailRules.Clip(to, 1000), Snippet = snippet,
                        Direction = direction, SentAtUtc = sentUtc, RelatedType = match.Type, RelatedId = match.Id, RelatedName = match.Name, ActivityId = activity.Id, CreatedById = userId
                    });
                    saved++;
                }
            }

            conn.CursorMs = nowMs - 600_000;
            status = $"OK: looked at {fresh.Count} new message(s), saved {saved} with known contacts.";
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

public class MailSyncService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MailSyncService> _logger;

    public MailSyncService(IServiceScopeFactory scopeFactory, ILogger<MailSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(180), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var google = scope.ServiceProvider.GetRequiredService<GoogleCalendarClient>();
                if (google.IsConfigured)
                {
                    var users = await db.MailConnection.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive).Select(x => x.UserId!).ToListAsync(stoppingToken);
                    foreach (var userId in users)
                    {
                        //>>> a fresh scope per user so one failure cannot poison the others
                        using var inner = _scopeFactory.CreateScope();
                        await inner.ServiceProvider.GetRequiredService<MailSyncRunner>().SyncUserAsync(userId, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "MailSyncService run failed"); }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
