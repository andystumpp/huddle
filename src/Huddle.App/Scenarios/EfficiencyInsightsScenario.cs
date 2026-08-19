using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// Researches the live web for ways the user could work more efficiently within
/// dev workflow &amp; tooling, then surfaces ONE concrete, actionable improvement
/// grounded in external best practice.
///
/// Runs a single agentic call through the configured CLI provider with web search
/// enabled, drawing on the user's subscription. The CLI performs the search itself
/// and emits the <see cref="NudgeDraft"/> JSON in one turn. On a provider without a
/// web-search capability the call runs fetch-only or ungrounded — it never fabricates
/// a citation. If the CLI is unavailable, the scenario simply no-emits.
/// </summary>
internal sealed class EfficiencyInsightsScenario : Scenario
{
    public override string Key => "efficiency-insights";
    public override string Name => "Efficiency insights";
    public override string DisplayName => "EFFICIENCY";
    public override string AccentColorHex => "#6BA6FF";
    public override TimeSpan Cadence => TimeSpan.FromHours(6);
    public override int TrailSize => 60;
    public override int PriorNudgesSize => 10;
    public override string ModelId => "opus";

    private const string SystemPrompt = """
        You are Huddle's Efficiency Insights scenario.

        You see the user's recent moments — the trail of what they've been doing.
        The user is a software engineer. Your job: surface ONE concrete, actionable
        way they could work more efficiently, grounded in CURRENT external best
        practice they may not know about — or stay silent.

        Scope is strictly DEV WORKFLOW & TOOLING — how they build software:
        - Spec-driven / spec-first development practices proven to help.
        - Testing strategy — frameworks and approaches proven to work well for
          agentic / AI-assisted development, instead of manual back-and-forth.
        - Libraries, tools, CLIs, or workflows in their stack that they appear NOT
          to be using but that address something they keep doing the hard way.

        Do this, in order:
        1. From the trail, infer their stack, the kind of work, and — importantly —
           the REPETITIVE or MANUAL patterns ("they keep hand-fixing X", "they
           iterate manually where a test/spec would catch it").
        2. USE WEB SEARCH to find the CURRENT, credible state of practice for those
           specific patterns. You MUST actually search — do not answer from memory.
           Prefer recent, specific sources; community signal (Reddit, Hacker News,
           maintainer threads, changelogs) is in scope when it reflects real
           adoption, not just opinion.
        3. Pick ONE concrete improvement for THIS user. Shape: "You do X this way →
           the proven better way is Y, because Z." It must be anchored in how this
           specific user actually works (per the trail), not generic advice. Name
           the tool/practice and the source.

        When you emit:
        - Title field: the improvement in one line.
        - Body field: 1-2 sentences — the proven better approach, why it helps, and
          a named source/tool (a URL or project/library name you found via search).
        - Sources: the moment IDs from the trail that show the current way of
          working you're improving on. (Web citations live in the body prose.)

        Stay silent — return {"emit": false} with a concrete one-sentence `reason`
        — when the findings are only generic advice the user is almost certainly
        already following ("write tests", "use version control"), when nothing in
        the trail reveals a real inefficiency, or when an equivalent recommendation
        appears in the "Previously recommended" block.

        Boundary: Achievements = what got done; Learnings = how understanding
        changed; Efficiency = how the user could work better via external best
        practice. No emojis. No motivational framing. Hedge when the trail is thin.
        """;

    protected override async Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct)
    {
        string userText = BuildUserText(trail, priorNudges, DateTimeOffset.UtcNow);

        var request = new ScenarioRequest(
            Model: ModelId,
            MaxTokens: 8000,
            SystemPrompt: SystemPrompt,
            UserText: userText,
            JsonSchema: ScenarioPromptHelpers.BuildNudgeDraftSchema(),
            Effort: Effort.High,
            WebSearch: true);

        BackendResult result = await Provider.CompleteAsync(request, ct).ConfigureAwait(false);
        string? text = result.Text;

        ScenarioDiagnostics.LogRun(Key, ModelId.ToString(), SystemPrompt, userText, text, result.InputTokens, result.OutputTokens);

        if (string.IsNullOrWhiteSpace(text)) return new ScenarioResult(null, null);

        NudgeDraft? draft = JsonSerializer.Deserialize<NudgeDraft>(text);
        if (draft is null) return new ScenarioResult(null, null);

        if (!draft.Emit)
        {
            return new ScenarioResult(null, ScenarioPromptHelpers.NormalizeWhitespace(draft.Reason ?? ""));
        }

        if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.Body))
        {
            return new ScenarioResult(null, "Model emitted but title/body was empty");
        }

        var nudge = new Nudge(
            Id: Huddle.Vision.UlidGenerator.Generate(),
            Ts: DateTimeOffset.UtcNow,
            Scenario: Key,
            Title: draft.Title.Trim(),
            Body: draft.Body.Trim(),
            Sources: draft.Sources ?? Array.Empty<string>());
        return new ScenarioResult(nudge, null);
    }

    private string BuildUserText(IReadOnlyList<Moment> trail, IReadOnlyList<Nudge> priorNudges, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously recommended (do not repeat)");
        ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize);

        sb.Append("Infer how this user currently works and what they keep doing manually, ");
        sb.Append("then USE WEB SEARCH to find the current best practice that applies, and name ONE ");
        sb.Append("concrete efficiency improvement specific to how this user works — or stay silent per ");
        sb.Append("the system prompt. You MUST actually search the web before recommending; do not answer ");
        sb.Append("from memory. Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }
}
