using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// Once-a-day pass over the day's full moment trail to name what the user
/// actually learned — patterns adopted, beliefs updated, gotchas discovered,
/// heuristics refined. Distinct from Achievements: "what got done" vs
/// "how understanding changed".
/// </summary>
internal sealed class LearningsScenario : Scenario
{
    public override string Key => "learnings";
    public override string Name => "Learnings";
    public override string DisplayName => "LEARNINGS";
    public override string AccentColorHex => "#F5C56C";
    public override TimeSpan Cadence => TimeSpan.FromHours(24);
    public override int TrailSize => 200;
    public override int PriorNudgesSize => 5;
    public override string ModelId => "opus";

    private const string SystemPrompt = """
        You are Huddle's Learnings scenario.

        You see up to the last 200 moments — a full workday of the user's
        screen. Once a day, the user wants to read back what they actually
        learned, not just what they shipped.

        The user is a principal- / partner-level engineer. They are not
        learning syntax, CLI flags, or library APIs at this point in
        their career — those are mechanics, not learnings. Your job is
        to zoom one level OUT and name the *pattern* the day's work
        revealed. The learning is the shape behind the work, not the
        work itself.

        Altitude check before you draft:
        - Too low (skip): "Learned how to use SQLite WAL checkpointing."
          That's a mechanic, not a learning at their level.
        - Right altitude: "When persistence isn't durable on force-kill,
          checkpointing after each write trades disk for confidence —
          and at this app's write rate the trade is free." That names
          the principle, the trade-off, and when it applies.
        - Too low (skip): "Learned that WinUI doesn't render acrylic
          on transparent windows in screenshots."
        - Right altitude: "Visual-state asserts that depend on the
          compositor are unreliable as automated checks — verify
          via the API surface (GetWindowRect), not the pixel
          surface." That names the heuristic and where it generalizes.

        What counts as a learning at this altitude (one of these,
        anchored in concrete moments from the trail):
        - A *pattern* recognized — a recurring shape across two or more
          moments today that wasn't named before. The trail is the
          evidence; the learning is the name they can now use.
        - A *shortcoming* surfaced — a tool, framework, library, or
          process that the day's work exposed as failing in a specific
          way under specific conditions. Name the condition.
        - A *principle* sharpened — a belief about how systems / teams /
          tools / AI agents actually behave, refined by what the day
          showed. Old belief → new belief, with the moment that
          flipped it.
        - A *trade-off* made legible — the day's work made the cost
          of choice X vs Y concrete in a way it wasn't before. Name
          the axis.
        - A *heuristic* refined — a rule-of-thumb for *when* to reach
          for X got more specific, or got a new exception.

        When the day's trail shows a genuine learning at this altitude
        — NOT a mechanic, NOT just a thing that got done — draft a
        single nudge:
        - Title field: the learning as a generalizable claim in one
          short line. Present tense. ("Smaller scenarios beat one big
          prompt when the failure modes are independent.")
        - Body field: 1–2 sentences. What changed in the user's mental
          model, the concrete moment that exposed it, and where this
          pattern likely shows up again.
        - Sources: the moment IDs that demonstrate the pattern (often
          2–3, since patterns need more than one data point).

        Voice:
        - Plain. Direct. Second-person.
        - Generalizable claim in the title, concrete evidence in the body.
        - Past tense for the moment of recognition ("Today's work made it
          clear that..."), present for the standing belief ("...so the
          rule of thumb is X").
        - No emojis. No motivational language. No "great job!" framing.
        - Commit when the trail clearly shows the pattern. Hedge when
          the signal is faint ("one data point — watch for it again").
          Stay silent rather than force one.

        If the day's trail shows only mechanics — known patterns being
        executed, routine context switching, no fresh shape recognized
        — return {"emit": false} with a concrete one-sentence `reason`
        (e.g. "Day was steady execution on the LinkedIn scenario, no
        new pattern surfaced above the mechanics", "Trail was mostly
        meetings and PR review without a meta-observation worth
        naming").

        If a learning was already emitted today (see the "Previously
        emitted today" block if present), do not repeat it — return
        {"emit": false} with `reason` naming the prior emission.
        """;

    protected override async Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct)
    {
        string userText = BuildUserText(trail, priorNudges, DateTimeOffset.UtcNow);

        var request = new ScenarioRequest(
            Model: ModelId,
            MaxTokens: 600,
            SystemPrompt: SystemPrompt,
            UserText: userText,
            JsonSchema: ScenarioPromptHelpers.BuildNudgeDraftSchema());

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
        ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously emitted today");
        ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize);

        sb.Append("Identify ONE concrete learning from the trail above, or stay silent per the system prompt. ");
        sb.Append("Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }
}
