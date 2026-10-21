using Domain.Common;

namespace Domain.Entities;

/// <summary>Knowledge base article. Published articles are visible on the public help centre.</summary>
public class KbArticle : BaseEntity
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string? CategoryId { get; set; }
    public string? Tags { get; set; }
    public bool IsPublished { get; set; }
    public int ViewCount { get; set; }
    public int HelpfulYes { get; set; }
    public int HelpfulNo { get; set; }
}
