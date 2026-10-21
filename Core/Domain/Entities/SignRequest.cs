using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// A request for someone outside the company to sign a document online. The signer gets a link, confirms with a
/// one-time code sent to the same e-mail, and the evidence (time, IP, browser, document fingerprint) is kept here.
/// </summary>
public class SignRequest : BaseEntity
{
    public string? Number { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? Title { get; set; }

    /// <summary>The exact text the signer saw. It is frozen when the request is made.</summary>
    public string? DocumentText { get; set; }

    /// <summary>SHA-256 of DocumentText, checked again at signing.</summary>
    public string? DocumentHash { get; set; }
    public string? SignerName { get; set; }
    public string? SignerEmail { get; set; }
    public string? TokenHash { get; set; }

    /// <summary>Sent, Viewed, Signed, Declined, Cancelled. Expired is worked out from ExpiresAtUtc.</summary>
    public string? Status { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public string? OtpHash { get; set; }
    public DateTime? OtpExpiresAtUtc { get; set; }
    public int OtpAttempts { get; set; }
    public int OtpSent { get; set; }

    public DateTime? ViewedAtUtc { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public string? SignedName { get; set; }
    public string? SignedIp { get; set; }
    public string? SignedUserAgent { get; set; }
    public string? DeclineReason { get; set; }
    public string? RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
}
