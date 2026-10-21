using System.Net;
using System.Text.RegularExpressions;

namespace Application.Features.EmailManager;

public static class EmailRenderer
{
    private static readonly Regex Token = new(@"\{\{\s*([a-zA-Z]+)\s*\}\}", RegexOptions.Compiled);

    /// <summary>
    /// Replaces {{field}} tokens (unknown ones become empty). Plain text is turned into safe HTML,
    /// HTML input keeps its markup and only the merged values are encoded.
    /// </summary>
    public static string Render(string? template, IDictionary<string, string?> values)
    {
        template ??= string.Empty;
        var isHtml = template.Contains('<') && template.Contains('>');

        string Lookup(Match m)
        {
            values.TryGetValue(m.Groups[1].Value.ToLowerInvariant(), out var v);
            return isHtml ? WebUtility.HtmlEncode(v ?? string.Empty) : v ?? string.Empty;
        }

        var merged = Token.Replace(template, Lookup);
        if (isHtml) return merged;
        return WebUtility.HtmlEncode(merged).Replace("\r\n", "\n").Replace("\n", "<br>");
    }

    /// <summary>Subjects are plain text: tokens are replaced, nothing is encoded.</summary>
    public static string RenderSubject(string? template, IDictionary<string, string?> values) =>
        Token.Replace(template ?? string.Empty, m =>
        {
            values.TryGetValue(m.Groups[1].Value.ToLowerInvariant(), out var v);
            return v ?? string.Empty;
        });
}
