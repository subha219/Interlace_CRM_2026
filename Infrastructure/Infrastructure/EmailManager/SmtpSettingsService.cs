using Application.Common.Services.EmailManager;
using Domain.Entities;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.EmailManager;

public record ResolvedSmtp(string? Host, int Port, string? UserName, string? Password, string? FromAddress, string? FromName, string Security);

public class SmtpSettingsService : ISmtpSettingsService
{
    private const string Purpose = "Interlace.EmailSetting.Password";
    private static readonly string[] Securities = { "Auto", "SslOnConnect", "StartTls", "None" };

    private readonly DataContext _context;
    private readonly IDataProtector _protector;
    private readonly SmtpSettings _fallback;

    public SmtpSettingsService(DataContext context, IDataProtectionProvider protectionProvider, IOptions<SmtpSettings> fallback)
    {
        _context = context;
        _protector = protectionProvider.CreateProtector(Purpose);
        _fallback = fallback.Value;
    }

    private Task<EmailSetting?> LoadAsync(CancellationToken ct) =>
        _context.EmailSetting.Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);

    /// <summary>Settings including the decrypted password, for sending mail.</summary>
    public async Task<ResolvedSmtp> ResolveAsync(CancellationToken ct = default)
    {
        var row = await LoadAsync(ct);
        if (row != null && !string.IsNullOrWhiteSpace(row.Host))
        {
            string? password = null;
            if (!string.IsNullOrEmpty(row.PasswordProtected))
            {
                try { password = _protector.Unprotect(row.PasswordProtected); }
                catch { /* key ring changed: the password must be entered again */ }
            }
            return new ResolvedSmtp(row.Host, row.Port, row.UserName, password, row.FromAddress, row.FromName, row.Security ?? "Auto");
        }

        return new ResolvedSmtp(_fallback.Host, _fallback.Port, _fallback.UserName, _fallback.Password, _fallback.FromAddress, _fallback.FromName, "Auto");
    }

    public async Task<SmtpSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadAsync(cancellationToken);
        if (row != null && !string.IsNullOrWhiteSpace(row.Host))
        {
            return new SmtpSettingsDto(row.Host, row.Port, row.UserName, !string.IsNullOrEmpty(row.PasswordProtected), row.FromAddress, row.FromName, row.Security ?? "Auto", "Saved in the database");
        }

        var configured = !string.IsNullOrWhiteSpace(_fallback.Host) && !string.IsNullOrWhiteSpace(_fallback.UserName);
        return new SmtpSettingsDto(null, 587, null, false, null, null, "Auto", configured ? "appsettings.json (fallback)" : "Not configured");
    }

    public async Task SaveAsync(SmtpSettingsInput input, string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Host)) throw new Exception("Host is required.");
        if (input.Port is < 1 or > 65535) throw new Exception("Port must be between 1 and 65535.");
        var security = Securities.FirstOrDefault(x => string.Equals(x, input.Security, StringComparison.OrdinalIgnoreCase)) ?? "Auto";

        var row = await LoadAsync(cancellationToken);
        var creating = row == null;
        row ??= new EmailSetting { CreatedById = userId, CreatedAtUtc = DateTime.UtcNow };

        row.Host = input.Host.Trim();
        row.Port = input.Port;
        row.UserName = string.IsNullOrWhiteSpace(input.UserName) ? null : input.UserName.Trim();
        row.FromAddress = string.IsNullOrWhiteSpace(input.FromAddress) ? null : input.FromAddress.Trim();
        row.FromName = string.IsNullOrWhiteSpace(input.FromName) ? null : input.FromName.Trim();
        row.Security = security;
        if (!string.IsNullOrEmpty(input.Password))
        {
            row.PasswordProtected = _protector.Protect(input.Password);
        }
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedById = userId;

        if (creating) _context.EmailSetting.Add(row);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
