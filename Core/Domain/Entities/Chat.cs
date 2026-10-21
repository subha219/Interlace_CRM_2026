using Domain.Common;

namespace Domain.Entities;

/// <summary>A live chat started by a website visitor. The visitor holds a secret token; only its hash is stored.</summary>
public class ChatSession : BaseEntity
{
    public string? TokenHash { get; set; }
    public string? VisitorName { get; set; }
    public string? VisitorEmail { get; set; }

    /// <summary>Open or Closed.</summary>
    public string? Status { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }

    /// <summary>True while the visitor has written something no agent has seen yet.</summary>
    public bool AgentUnread { get; set; }

    /// <summary>Support case made from this chat, if any.</summary>
    public string? CaseId { get; set; }
    public string? CaseNumber { get; set; }
}

public class ChatMessage : BaseEntity
{
    public string? SessionId { get; set; }
    public bool FromVisitor { get; set; }
    public string? SenderName { get; set; }
    public string? Body { get; set; }
}
