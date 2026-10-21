namespace Application.Common.Services.IntegrationManager;

/// <summary>
/// Connection settings (keys for Google, WhatsApp, SMS, telephony, single sign-on, the AI model ...).
/// A value saved by an administrator in the app wins; otherwise the server's configuration file or environment is used.
/// An empty value reads as null.
/// </summary>
public interface IConnectionSettings
{
    string? this[string key] { get; }

    /// <summary>"saved" (set in the app), "server" (configuration file or environment) or "none".</summary>
    string SourceOf(string key);

    /// <summary>For each key: null leaves it unchanged, an empty text removes the saved value, anything else replaces it.</summary>
    Task SaveAsync(IReadOnlyDictionary<string, string?> changes, string? userId, CancellationToken cancellationToken = default);
}
