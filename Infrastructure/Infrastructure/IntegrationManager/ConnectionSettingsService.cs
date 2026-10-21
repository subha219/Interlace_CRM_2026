using System.Security.Cryptography;
using Application.Common.CQS.Queries;
using Application.Common.Repositories;
using Application.Common.Services.IntegrationManager;
using Application.Features.ConnectionManager;
using Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.IntegrationManager;

/// <summary>
/// Reads connection settings saved in the database (secrets decrypted with data protection) and falls back to the server
/// configuration. Values are cached for 30 seconds; a save refreshes the cache at once on this server.
/// </summary>
public sealed class ConnectionSettingsService : IConnectionSettings
{
    private static readonly long TtlTicks = TimeSpan.FromSeconds(30).Ticks;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopes;
    private readonly IDataProtector _protector;
    private readonly ILogger<ConnectionSettingsService> _logger;
    private readonly object _lock = new();
    private volatile Dictionary<string, string> _saved = new(StringComparer.OrdinalIgnoreCase);
    private long _loadedAt;

    public ConnectionSettingsService(IConfiguration config, IServiceScopeFactory scopes, IDataProtectionProvider provider, ILogger<ConnectionSettingsService> logger)
    {
        _config = config; _scopes = scopes; _logger = logger;
        _protector = provider.CreateProtector("Interlace.ConnectionSettings.v1");
    }

    public string? this[string key] => Resolve(key).Value;

    public string SourceOf(string key) => Resolve(key).Source;

    private (string? Value, string Source) Resolve(string key)
    {
        Refresh();
        if (_saved.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return (v, "saved");
        var c = _config[key];
        return string.IsNullOrWhiteSpace(c) ? (null, "none") : (c, "server");
    }

    private void Refresh(bool force = false)
    {
        if (!force && DateTime.UtcNow.Ticks - Interlocked.Read(ref _loadedAt) < TtlTicks) return;
        lock (_lock)
        {
            if (!force && DateTime.UtcNow.Ticks - Interlocked.Read(ref _loadedAt) < TtlTicks) return;
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IQueryContext>();
                var rows = db.IntegrationSetting.AsNoTracking().Where(x => !x.IsDeleted).ToList();
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in rows)
                {
                    if (string.IsNullOrEmpty(r.Key) || string.IsNullOrEmpty(r.Value)) continue;
                    try { map[r.Key] = r.IsSecret ? _protector.Unprotect(r.Value) : r.Value; }
                    catch (CryptographicException) { _logger.LogWarning("Could not decrypt connection setting {Key}; save it again.", r.Key); }
                }
                _saved = map;
            }
            catch (Exception ex)
            {
                // for example before the table exists; keep what we had and use the configuration file
                _logger.LogWarning(ex, "Could not read saved connection settings.");
            }
            Interlocked.Exchange(ref _loadedAt, DateTime.UtcNow.Ticks);
        }
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, string?> changes, string? userId, CancellationToken cancellationToken = default)
    {
        using (var scope = _scopes.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICommandRepository<IntegrationSetting>>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var keys = changes.Keys.ToList();
            var existing = await repo.GetQuery().Where(x => x.Key != null && keys.Contains(x.Key)).ToListAsync(cancellationToken);

            foreach (var (key, value) in changes)
            {
                var field = ConnectionRegistry.Find(key) ?? throw new ArgumentException("Unknown setting.");
                if (value == null) continue;
                var row = existing.FirstOrDefault(x => string.Equals(x.Key, field.Key, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted)
                          ?? existing.FirstOrDefault(x => string.Equals(x.Key, field.Key, StringComparison.OrdinalIgnoreCase));
                if (value.Length == 0)
                {
                    if (row != null && !row.IsDeleted) { row.IsDeleted = true; row.Value = null; row.UpdatedById = userId; repo.Update(row); }
                    continue;
                }
                var stored = field.Secret ? _protector.Protect(value) : value;
                if (row == null) await repo.CreateAsync(new IntegrationSetting { Key = field.Key, Value = stored, IsSecret = field.Secret, CreatedById = userId }, cancellationToken);
                else { row.IsDeleted = false; row.Value = stored; row.IsSecret = field.Secret; row.UpdatedById = userId; repo.Update(row); }
            }
            await uow.SaveAsync(cancellationToken);
        }

        // names only, never values
        _logger.LogInformation("Connection settings changed by {UserId}: {Keys}", userId, string.Join(", ", changes.Where(c => c.Value != null).Select(c => c.Key)));
        Refresh(force: true);
    }
}
