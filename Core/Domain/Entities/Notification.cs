using Domain.Common;

namespace Domain.Entities;

public class Notification : BaseEntity
{
    public string? Title { get; set; }
    public string? Message { get; set; }

    /// <summary>Info, Warning or Danger. Used for the colour of the badge.</summary>
    public string? Type { get; set; }
    public string? Url { get; set; }

    /// <summary>Target user. Null means the notification is visible to every user.</summary>
    public string? UserId { get; set; }
    public bool IsRead { get; set; }

    /// <summary>Stops the background job from creating the same alert twice.</summary>
    public string? DedupKey { get; set; }
}
