using System.Security.Cryptography;
using System.Text;

namespace Application.Features.CalendarManager;

/// <summary>An activity as the calendar sync sees it.</summary>
public record CalendarActivity(string Id, string? Subject, string? Description, DateTime? Start, DateTime? End, bool IsOpen, bool IsDeleted, bool BelongsToUser);

public record CalendarLink(string LinkId, string ActivityId, string EventId, string? Hash);
public record CalendarUpsert(CalendarActivity Activity, string? ExistingEventId, string? ExistingLinkId, string Hash);
public record CalendarDelete(string LinkId, string EventId);
public record CalendarPlan(List<CalendarUpsert> Upserts, List<CalendarDelete> Deletes);

public static class CalendarRules
{
    public const int MaxUpsertsPerRun = 50;
    public const int DefaultMinutes = 30;

    public static string Hash(CalendarActivity a)
    {
        var text = $"{a.Subject}|{a.Description}|{a.Start:o}|{a.End:o}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24];
    }

    public static DateTime EndOf(CalendarActivity a) => a.End != null && a.End > a.Start ? a.End.Value : a.Start!.Value.AddMinutes(DefaultMinutes);

    /// <summary>
    /// What to send. Events whose activity is gone, finished, deleted or no longer this user's are removed;
    /// open activities with a start time are created, or updated when their content changed.
    /// </summary>
    public static CalendarPlan Plan(IEnumerable<CalendarActivity> inWindow, IEnumerable<CalendarActivity> linkedActivities, IEnumerable<CalendarLink> links)
    {
        var plan = new CalendarPlan(new(), new());
        var byId = linkedActivities.ToDictionary(x => x.Id);
        var linkByActivity = new Dictionary<string, CalendarLink>();

        foreach (var l in links)
        {
            if (!byId.TryGetValue(l.ActivityId, out var a) || a.IsDeleted || !a.IsOpen || !a.BelongsToUser || a.Start == null)
            {
                plan.Deletes.Add(new CalendarDelete(l.LinkId, l.EventId));
                continue;
            }
            linkByActivity[l.ActivityId] = l;
        }

        foreach (var a in inWindow)
        {
            if (a.IsDeleted || !a.IsOpen || !a.BelongsToUser || a.Start == null) continue;
            var h = Hash(a);
            linkByActivity.TryGetValue(a.Id, out var link);
            if (link != null && link.Hash == h) continue;
            if (plan.Upserts.Count >= MaxUpsertsPerRun) break;
            plan.Upserts.Add(new CalendarUpsert(a, link?.EventId, link?.LinkId, h));
        }
        return plan;
    }

    /// <summary>The event body Microsoft Graph expects (Outlook). Times are sent in UTC.</summary>
    public static Dictionary<string, object?> BuildOutlookEvent(CalendarActivity a)
    {
        string Utc(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Unspecified)).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss");
        return new Dictionary<string, object?>
        {
            ["subject"] = string.IsNullOrWhiteSpace(a.Subject) ? "(no title)" : a.Subject,
            ["body"] = new Dictionary<string, object?> { ["contentType"] = "text", ["content"] = string.IsNullOrWhiteSpace(a.Description) ? "From the CRM" : a.Description + "\n\n(from the CRM)" },
            ["start"] = new Dictionary<string, object?> { ["dateTime"] = Utc(a.Start!.Value), ["timeZone"] = "UTC" },
            ["end"] = new Dictionary<string, object?> { ["dateTime"] = Utc(EndOf(a)), ["timeZone"] = "UTC" },
            ["isReminderOn"] = true
        };
    }

    /// <summary>The event body Google expects. Times carry the server's UTC offset, so no time-zone name is needed.</summary>
    public static Dictionary<string, object?> BuildEvent(CalendarActivity a)
    {
        string Rfc(DateTime d) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Unspecified)).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        return new Dictionary<string, object?>
        {
            ["summary"] = string.IsNullOrWhiteSpace(a.Subject) ? "(no title)" : a.Subject,
            ["description"] = string.IsNullOrWhiteSpace(a.Description) ? "From the CRM" : a.Description + "\n\n(from the CRM)",
            ["start"] = new Dictionary<string, object?> { ["dateTime"] = Rfc(a.Start!.Value) },
            ["end"] = new Dictionary<string, object?> { ["dateTime"] = Rfc(EndOf(a)) },
            ["reminders"] = new Dictionary<string, object?> { ["useDefault"] = true }
        };
    }
}
