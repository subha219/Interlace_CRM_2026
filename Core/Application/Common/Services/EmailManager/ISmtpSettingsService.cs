namespace Application.Common.Services.EmailManager;

public record SmtpSettingsDto(string? Host, int Port, string? UserName, bool HasPassword, string? FromAddress, string? FromName, string Security, string Source);

public record SmtpSettingsInput(string? Host, int Port, string? UserName, string? Password, string? FromAddress, string? FromName, string? Security);

public interface ISmtpSettingsService
{
    /// <summary>The settings in use (the saved ones, or the appsettings fallback). The password is never included.</summary>
    Task<SmtpSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the settings. An empty password keeps the stored one.</summary>
    Task SaveAsync(SmtpSettingsInput input, string? userId, CancellationToken cancellationToken = default);
}
