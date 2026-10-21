using System.Net;
using System.Text.RegularExpressions;

namespace Application.Features.MailSyncManager;

public static class MailRules
{
    public const int MaxMessagesPerRun = 300;
    public const int FirstSyncDays = 30;
    private static readonly string[] SkipLabels = { "SPAM", "TRASH", "DRAFT", "CATEGORY_PROMOTIONS", "CATEGORY_SOCIAL" };
    private static readonly Regex Address = new(@"[A-Za-z0-9._%+\-']+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    /// <summary>Every address in a header such as "Asha &lt;asha@x.com&gt;, bob@y.com", lower case, no duplicates.</summary>
    public static List<string> ParseAddresses(string? header) =>
        Address.Matches(header ?? "").Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();

    public static bool IsSkippable(IEnumerable<string> labels) => labels.Any(l => SkipLabels.Contains(l));

    public static string Direction(string? from, string mine) =>
        ParseAddresses(from).Contains((mine ?? "").ToLowerInvariant()) ? "Out" : "In";

    /// <summary>The people on the other side: recipients of a sent mail, or the sender of a received one. Never the mailbox owner.</summary>
    public static List<string> Counterparties(string direction, string? from, string? to, string? cc, string mine)
    {
        var me = (mine ?? "").ToLowerInvariant();
        var list = direction == "Out" ? ParseAddresses(to).Concat(ParseAddresses(cc)) : ParseAddresses(from);
        return list.Where(a => a != me).Distinct().ToList();
    }

    public static string Clip(string? text, int max)
    {
        var s = WebUtility.HtmlDecode(text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return s.Length > max ? s[..max] : s;
    }

    public static string ActivitySubject(string direction, string? name, string? subject)
    {
        var s = $"{(direction == "Out" ? "Email to" : "Email from")} {name ?? "contact"}: {Clip(subject, 150)}".Trim().TrimEnd(':');
        return s.Length > 200 ? s[..200] : s;
    }

    public static DateTime? FromInternalDate(string? ms) =>
        long.TryParse(ms, out var v) && v > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(v).UtcDateTime : null;

    /// <summary>Gmail's "after:" search takes whole seconds; go back a little so nothing in the gap is missed.</summary>
    public static long QueryAfterSeconds(long? cursorMs, DateTime nowUtc)
    {
        var ms = cursorMs ?? new DateTimeOffset(nowUtc.AddDays(-FirstSyncDays)).ToUnixTimeMilliseconds();
        return Math.Max(0, ms / 1000 - 300);
    }
}
