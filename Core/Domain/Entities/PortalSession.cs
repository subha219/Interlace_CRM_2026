using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// One customer-portal sign-in. The customer asks for a link by e-mail (LoginTokenHash, 30 minutes, once);
/// opening it turns it into a session (SessionTokenHash, 8 hours). Only hashes are stored.
/// </summary>
public class PortalSession : BaseEntity
{
    public string? CustomerId { get; set; }
    public string? Email { get; set; }
    public string? LoginTokenHash { get; set; }
    public DateTime? LoginExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public string? SessionTokenHash { get; set; }
    public DateTime? SessionExpiresAtUtc { get; set; }
}
