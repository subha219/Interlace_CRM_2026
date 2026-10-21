using System.Text.Json;
using System.Text.RegularExpressions;

namespace Application.Features.WorkflowManager;

public record WorkflowCondition(string Field, string Op, string? Value);

public static class WorkflowModel
{
    public static readonly string[] Events = { "lead.created", "lead.stage_changed", "lead.won", "lead.lost", "customer.created", "case.created" };
    public static readonly string[] Operators = { "equals", "not_equals", "contains", "gt", "lt", "is_empty", "is_not_empty" };
    public static readonly string[] ActionTypes = { "create_activity", "notify", "set_owner", "add_tag", "send_email" };
    public static readonly string[] ActivityTypes = { "Task", "Call", "Meeting", "Email", "Note" };

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string EntityOf(string? eventName) => (eventName ?? "").Split('.')[0] switch
    {
        "lead" => "Lead",
        "customer" => "Customer",
        "case" => "SupportCase",
        _ => ""
    };

    public static List<WorkflowCondition> ParseConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        return JsonSerializer.Deserialize<List<WorkflowCondition>>(json, Options) ?? new();
    }

    public static List<Dictionary<string, JsonElement>> ParseActions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        return JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, Options) ?? new();
    }

    /// <summary>Returns an error message, or null when the rule definition is usable.</summary>
    public static string? Validate(string? trigger, string? conditionsJson, string? actionsJson)
    {
        if (!Events.Contains(trigger ?? "")) return "Choose a valid trigger.";
        try
        {
            foreach (var c in ParseConditions(conditionsJson))
            {
                if (string.IsNullOrWhiteSpace(c.Field)) return "A condition needs a field.";
                if (!Operators.Contains(c.Op)) return $"Unknown condition operator: {c.Op}";
            }

            var actions = ParseActions(actionsJson);
            if (actions.Count == 0) return "Add at least one action.";
            if (actions.Count > 5) return "A rule can have at most 5 actions.";
            var entity = EntityOf(trigger);
            foreach (var a in actions)
            {
                var type = Str(a, "type");
                if (!ActionTypes.Contains(type ?? "")) return $"Unknown action: {type}";
                switch (type)
                {
                    case "create_activity":
                        if (string.IsNullOrWhiteSpace(Str(a, "subject"))) return "An activity action needs a subject.";
                        if (!ActivityTypes.Contains(Str(a, "activityType") ?? "Task")) return "Unknown activity type.";
                        break;
                    case "notify":
                        if (string.IsNullOrWhiteSpace(Str(a, "message"))) return "A notification needs a message.";
                        break;
                    case "set_owner":
                        if (string.IsNullOrWhiteSpace(Str(a, "userId"))) return "Choose the new owner.";
                        break;
                    case "add_tag":
                        if (entity == "SupportCase") return "Tags are available for leads and customers only.";
                        if (string.IsNullOrWhiteSpace(Str(a, "tag"))) return "Enter the tag.";
                        break;
                    case "send_email":
                        if (string.IsNullOrWhiteSpace(Str(a, "subject")) || string.IsNullOrWhiteSpace(Str(a, "body"))) return "An e-mail action needs a subject and a body.";
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return "The rule could not be read.";
        }
        return null;
    }

    public static string? Str(Dictionary<string, JsonElement> a, string key) =>
        a.TryGetValue(key, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()) : null;

    public static bool Matches(IEnumerable<WorkflowCondition> conditions, IReadOnlyDictionary<string, string?> payload)
    {
        foreach (var c in conditions)
        {
            payload.TryGetValue(c.Field, out var actual);
            var expected = c.Value ?? "";
            var ok = c.Op switch
            {
                "equals" => string.Equals(actual ?? "", expected, StringComparison.OrdinalIgnoreCase),
                "not_equals" => !string.Equals(actual ?? "", expected, StringComparison.OrdinalIgnoreCase),
                "contains" => (actual ?? "").Contains(expected, StringComparison.OrdinalIgnoreCase),
                "gt" => Num(actual, out var a1) && Num(expected, out var e1) && a1 > e1,
                "lt" => Num(actual, out var a2) && Num(expected, out var e2) && a2 < e2,
                "is_empty" => string.IsNullOrWhiteSpace(actual),
                "is_not_empty" => !string.IsNullOrWhiteSpace(actual),
                _ => false
            };
            if (!ok) return false;
        }
        return true;
    }

    private static bool Num(string? s, out double d) =>
        double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);

    /// <summary>Replaces {field} placeholders with values of the record.</summary>
    public static string Render(string? template, IReadOnlyDictionary<string, string?> payload) =>
        Regex.Replace(template ?? "", @"\{(\w+)\}", m => payload.TryGetValue(m.Groups[1].Value, out var v) ? v ?? "" : m.Value);
}
