using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Huddle.Storage;
using ModelContextProtocol.Server;

namespace Huddle.Mcp;

/// <summary>
/// Read-only MCP tools over the Huddle database. Every method queries via
/// <c>Huddle.Core</c>'s read-only store methods and returns compact JSON.
/// </summary>
[McpServerToolType]
public static class HuddleTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    [McpServerTool(Name = "list_nudges"),
     Description("List Huddle nudges — curated suggestions Huddle emits about the user's work. " +
                "Newest first. Optionally isolate one scenario and limit the recent-day window. " +
                "Scenario keys: achievements, learnings, linkedin-posts, efficiency-insights.")]
    public static async Task<string> ListNudges(
        [Description("Scenario key to isolate (achievements | learnings | linkedin-posts | efficiency-insights). Omit for all scenarios.")]
        string? scenario = null,
        [Description("How many days back to include. Default 7.")]
        int sinceDays = 7,
        [Description("Maximum number of nudges to return. Default 50.")]
        int limit = 50)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(Math.Max(1, sinceDays));
        var nudges = await NudgeStore.SinceByScenarioAsync(
            string.IsNullOrWhiteSpace(scenario) ? null : scenario.Trim(),
            cutoff,
            Math.Clamp(limit, 1, 500));

        var shaped = nudges.Select(n => new
        {
            ts = n.Ts,
            scenario = n.Scenario,
            title = n.Title,
            body = n.Body,
            sources = n.Sources,
        });
        return JsonSerializer.Serialize(shaped, Json);
    }

    [McpServerTool(Name = "search_moments"),
     Description("Search Huddle moments — raw per-tick screen observations — whose summary, app, " +
                "or window title contain the query text. Newest first. Use to ground a claim in " +
                "what the user was actually doing.")]
    public static async Task<string> SearchMoments(
        [Description("Text to match against the moment summary, app name, or window title.")]
        string query,
        [Description("Optional: how many days back to include. Omit for all history.")]
        int? sinceDays = null,
        [Description("Maximum number of moments to return. Default 50.")]
        int limit = 50)
    {
        DateTimeOffset? cutoff = sinceDays is int d
            ? DateTimeOffset.UtcNow - TimeSpan.FromDays(Math.Max(1, d))
            : null;

        var moments = await MomentStore.SearchAsync(query ?? string.Empty, cutoff, Math.Clamp(limit, 1, 500));
        var shaped = moments.Select(m => new
        {
            ts = m.Ts,
            app = m.App,
            windowTitle = m.WindowTitle,
            summary = m.Summary,
        });
        return JsonSerializer.Serialize(shaped, Json);
    }

    [McpServerTool(Name = "get_day"),
     Description("Get all Huddle moments and nudges for one LOCAL calendar day. Pass date as " +
                "YYYY-MM-DD, or OMIT it to get today — the machine's local day, which is the " +
                "correct choice for 'today's summary'. (The user's local date can differ from " +
                "UTC, so prefer omitting date over computing 'today' yourself.) " +
                "The digest primitive: everything observed and surfaced on that day.")]
    public static async Task<string> GetDay(
        [Description("Local calendar day, YYYY-MM-DD. Omit for today (the machine's local day).")]
        string? date = null)
    {
        DateTime day = string.IsNullOrWhiteSpace(date)
            ? DateTimeOffset.Now.Date
            : DateTime.ParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

        var startUtc = new DateTimeOffset(day).ToUniversalTime();
        var endUtc = new DateTimeOffset(day.AddDays(1)).ToUniversalTime();

        var moments = await MomentStore.BetweenAsync(startUtc, endUtc);
        var nudges = await NudgeStore.BetweenAsync(startUtc, endUtc);

        var shaped = new
        {
            date = day.ToString("yyyy-MM-dd"),   // the resolved local day actually returned
            moments = moments.Select(m => new { ts = m.Ts, app = m.App, windowTitle = m.WindowTitle, summary = m.Summary }),
            nudges = nudges.Select(n => new { ts = n.Ts, scenario = n.Scenario, title = n.Title, body = n.Body, sources = n.Sources }),
        };
        return JsonSerializer.Serialize(shaped, Json);
    }
}
