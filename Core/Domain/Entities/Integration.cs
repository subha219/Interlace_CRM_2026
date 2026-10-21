using Domain.Common;

namespace Domain.Entities;

/// <summary>Key for the public lead-capture endpoint. Only the SHA-256 hash is stored.</summary>
public class ApiKey : BaseEntity
{
    public string? Name { get; set; }
    public string? KeyHash { get; set; }

    /// <summary>First characters of the key, shown so people can tell keys apart.</summary>
    public string? Prefix { get; set; }

    /// <summary>New leads from this key are owned by this user.</summary>
    public string? OwnerUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public class WebhookEndpoint : BaseEntity
{
    public string? Name { get; set; }
    public string? Url { get; set; }

    /// <summary>Signing secret for the X-Interlace-Signature header.</summary>
    public string? Secret { get; set; }

    /// <summary>Comma separated event names, or * for all.</summary>
    public string? Events { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WebhookDelivery : BaseEntity
{
    public string? EndpointId { get; set; }
    public string? EndpointName { get; set; }
    public string? EventName { get; set; }
    public bool Success { get; set; }
    public int? HttpStatus { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTime DeliveredAtUtc { get; set; }
}
