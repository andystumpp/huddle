using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Huddle.Models;
using Huddle.Scenarios;
using Huddle.Storage;

namespace Huddle.Memory;

/// <summary>
/// The daily reflection: distils the recent moment trail into <see cref="ProfileStore"/>'s
/// evolving <c>profile.md</c>. Separate from scenarios — it writes durable state and emits
/// no nudge. Runs at model <c>opus</c>, extra-high effort (once a day on a flat-rate
/// subscription, so deep reasoning for the dedup/merge judgment is warranted).
/// </summary>
internal static class ReflectionJob
{
    private static readonly TimeSpan Cadence = TimeSpan.FromHours(24);
    private const int TrailSize = 400;

    private static readonly ICliProvider s_provider = CliProviderFactory.Resolve();

    public static bool IsDue() => ProfileStore.IsDue(Cadence);

    public static async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<Moment> moments = await MomentStore.RecentAsync(TrailSize).ConfigureAwait(false);
            if (moments.Count == 0) return; // nothing to reflect on yet

            string current = ProfileStore.Read();
            string userText = BuildUserText(current, moments);

            var request = new ScenarioRequest(
                Model: "opus",
                MaxTokens: 4000,
                SystemPrompt: SystemPrompt,
                UserText: userText,
                JsonSchema: BuildProfileSchema(),
                Effort: Effort.XHigh,
                WebSearch: false);

            BackendResult result = await s_provider.CompleteAsync(request, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.Text)) return;

            string? profile = ExtractProfile(result.Text);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                ProfileStore.Write(profile.Trim());
                Debug.WriteLine("[Huddle] reflection updated profile.md");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] reflection failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private const string SystemPrompt = """
        You maintain Huddle's evolving profile of the user — a durable, at-a-glance markdown
        document capturing who they are and what they are working on, so Huddle's scenarios
        do not have to re-learn it from scratch each time.

        You are given the CURRENT profile and the user's RECENT screen moments (newest
        first). Produce the UPDATED profile.

        Rules:
        - Preserve everything in the current profile unless the recent moments clearly
          supersede or contradict it — then update it (recency wins). Do NOT drop durable
          facts just because they are not in the recent trail.
        - Only ADD a fact when it is a stable, repeated signal — not a one-off. A single
          screenshot is not a durable fact.
        - Organize into short markdown sections, in this order:
          ## Projects, ## People & context, ## Decisions & adopted,
          ## Preferences & working style, ## Current focus.
        - Keep it TIGHT — at most about one page. Prune stale or superseded items. This is a
          profile, not a log.
        - NEVER include specific sensitive values — no salaries, dollar amounts, account or
          card numbers, passwords, medical values, or personal identifiers. Describe the
          kind of thing, not the values.
        - Plain markdown. Second person ("You are working on...") is fine. No preamble.

        Reply with ONLY a JSON object and nothing else:
        {"profile": "<the full updated markdown profile>"}
        """;

    private static string BuildUserText(string current, IReadOnlyList<Moment> moments)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CURRENT PROFILE:");
        sb.AppendLine(string.IsNullOrWhiteSpace(current) ? "(empty — this is the first reflection)" : current.Trim());
        sb.AppendLine();
        ScenarioPromptHelpers.AppendRecentMoments(sb, moments, DateTimeOffset.UtcNow, moments.Count);
        sb.Append("Produce the updated profile per the system prompt.");
        return sb.ToString();
    }

    private static Dictionary<string, JsonElement> BuildProfileSchema()
    {
        var properties = new { profile = new { type = "string" } };
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["properties"] = JsonSerializer.SerializeToElement(properties),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "profile" }),
        };
    }

    /// <summary>Isolate the first balanced JSON object and read its <c>profile</c> field.</summary>
    private static string? ExtractProfile(string text)
    {
        string t = text.Trim();
        int start = t.IndexOf('{');
        int end = t.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try
        {
            using var doc = JsonDocument.Parse(t.Substring(start, end - start + 1));
            if (doc.RootElement.TryGetProperty("profile", out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        catch (JsonException) { /* not the shape we asked for */ }
        return null;
    }
}
