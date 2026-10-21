using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// One connection setting (an API key, client id, token ...) saved by an administrator. Secret values are stored encrypted
/// with the application's data-protection keys, never as plain text.
/// </summary>
public class IntegrationSetting : BaseEntity
{
    public string? Key { get; set; }
    public string? Value { get; set; }
    public bool IsSecret { get; set; }
}
