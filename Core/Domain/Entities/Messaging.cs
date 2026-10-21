using Domain.Common;

namespace Domain.Entities;

/// <summary>One WhatsApp or SMS message, sent or received. Address is the phone number as digits with country code.</summary>
public class ChannelMessage : BaseEntity
{
    /// <summary>WhatsApp or SMS.</summary>
    public string? Channel { get; set; }

    /// <summary>Out or In.</summary>
    public string? Direction { get; set; }
    public string? Address { get; set; }
    public string? ContactName { get; set; }
    public string? Body { get; set; }
    public string? TemplateName { get; set; }

    /// <summary>Queued, Sent, Delivered, Read, Failed or Received.</summary>
    public string? Status { get; set; }
    public string? Error { get; set; }
    public string? ExternalId { get; set; }
    public bool IsMarketing { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public string? RelatedName { get; set; }
    public string? SentByUserId { get; set; }
    public string? SentByName { get; set; }
}

/// <summary>A message template. For WhatsApp it must be approved by Meta first; for SMS it is the DLT-registered template.</summary>
public class MessageTemplate : BaseEntity
{
    public string? Name { get; set; }
    public string? Channel { get; set; }

    /// <summary>WhatsApp: the approved template name. SMS: the provider's template / flow id.</summary>
    public string? ProviderTemplateId { get; set; }
    public string? Language { get; set; }

    /// <summary>Text with {{variable}} placeholders, used for the preview and the message log.</summary>
    public string? Body { get; set; }

    /// <summary>Variable names in the order the provider expects, comma separated.</summary>
    public string? Variables { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A number that asked to stop receiving marketing messages.</summary>
public class ChannelOptOut : BaseEntity
{
    public string? Channel { get; set; }
    public string? Address { get; set; }
    public string? Reason { get; set; }
}
