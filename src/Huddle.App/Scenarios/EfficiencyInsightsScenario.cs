using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// Researches the live web for ways the user could work more efficiently within
/// dev workflow &amp; tooling, then surfaces ONE concrete, actionable improvement
/// grounded in external best practice.
///
/// Unlike the trail-only scenarios, this one runs in two phases: phase 1 enables
/// the web search server tool and gathers findings as text (no JSON format, since
/// web search emits citations which are incompatible with structured output);
/// phase 2 makes a second, tool-free call that turns the findings into the shared
/// <see cref="NudgeDraft"/> JSON. Storage and the NudgeCard are unchanged.
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
    public override Model ModelId => Model.ClaudeOpus4_8;

    private const string ResearchSystemPrompt = """
        You are Huddle's Efficiency Insights scenario — phase 1 of 2: RESEARCH.

        You see the user's recent moments — the trail of what they've been doing.
        The user is a software engineer. Your job in this phase is NOT to give
        advice yet. It is to (a) infer how they currently work, and (b) research
        the current external best practices that apply, so a second pass can turn
        your findings into one sharp recommendation.

        Scope is strictly DEV WORKFLOW & TOOLING — how they build software:
        - Spec-driven / spec-first development practices and what's proven to help.
        - Testing strategy — frameworks and approaches proven to work well for
          agentic / AI-assisted development, instead of manual back-and-forth.
        - Libraries, tools, CLIs, or workflows in their stack that they appear NOT
          to be using but that address something they keep doing the hard way.

        Steps:
        1. From the trail, infer their stack, the kind of work, and — importantly —
           the REPETITIVE or MANUAL patterns ("they keep hand-fixing X", "they
           iterate manually where a test/spec would catch it", "they do Y by hand").
        2. Use web search to find the CURRENT, credible state of practice for those
           specific patterns. Prefer recent and specific sources. Community signal
           (Reddit, Hacker News, maintainer threads, changelogs) is in scope when it
           reflects real adoption, not just opinion.
        3. Write up what you found and why it is relevant to THIS user — name the
           specific tools/practices, what problem each solves, and cite the sources
           inline (with URLs). Do not yet pick a single recommendation; gather the
           material the synthesis step will choose from.

        Boundary from Huddle's other scenarios: Achievements covers WHAT GOT DONE,
        Learnings covers HOW UNDERSTANDING CHANGED. You cover HOW THE USER COULD
        WORK BETTER based on external best practice they may not know about.

        No emojis. No motivational framing. Hedge when the trail is thin or the
        evidence is weak.
        """;

    private const string SynthesisSystemPrompt = """
        You are Huddle's Efficiency Insights scenario — phase 2 of 2: SYNTHESIS.

        You are given (a) the user's recent moment trail and (b) research findings
        gathered in phase 1 about current dev workflow & tooling best practices.
        Your job: name ONE concrete, actionable efficiency improvement for THIS
        user, or stay silent.

        The shape of a good insight: "You do X this way → the proven better way is
        Y, because Z." It must be anchored in how this specific user actually works
        (per the trail), not generic advice. Name the tool/practice and the source.

        - Title field: the improvement in one line.
        - Body field: 1-2 sentences — the proven better approach, why it helps, and
          a named source/tool (a URL or project/library name from the findings).
        - Sources: the moment IDs from the trail that show the current way of
          working you're improving on. (Web citations live in the body prose, not
          here.)

        Stay silent — return {"emit": false} with a concrete one-sentence `reason`
        — when the findings are only generic advice the user is almost certainly
        already following ("write tests", "use version control"), when nothing in
        the trail reveals a real inefficiency, or when an equivalent recommendation
        appears in the "Previously recommended" block.

        Boundary: Achievements = what got done; Learnings = how understanding
        changed; Efficiency = how the user could work better via external best
        practice. No emojis. No motivational framing. Hedge when ambiguous.
        """;

    protected override async Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct)
    {
        var client = new AnthropicClient();
        var now = DateTimeOffset.UtcNow;

        // ---- Phase 1: research with the web search server tool (no JSON format) ----
        string researchUserText = BuildResearchUserText(trail, priorNudges, now);

        var researchParams = new MessageCreateParams
        {
            Model = ModelId,
            // Adaptive thinking shares this budget; leave headroom above the
            // findings write-up plus the server-tool round trips.
            MaxTokens = 8000,
            System = ResearchSystemPrompt,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = Effort.High },
            // MaxUses bounds the server-side search loop to a single response,
            // so we never need to handle pause_turn round-trips (design D3).
            Tools = new List<ToolUnion>
            {
                new WebSearchTool20260209 { MaxUses = 5 },
            },
            Messages = new List<MessageParam>
            {
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = researchUserText },
                    },
                },
            },
        };

        Message researchResponse = await client.Messages.Create(researchParams, cancellationToken: ct).ConfigureAwait(false);

        string findings = string.Join(
            "\n",
            researchResponse.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text));

        ScenarioDiagnostics.LogRun(
            Key,
            ModelId.ToString(),
            ResearchSystemPrompt,
            researchUserText + $"\n[stopReason={researchResponse.StopReason}]",
            findings,
            researchResponse.Usage?.InputTokens,
            researchResponse.Usage?.OutputTokens);

        if (string.IsNullOrWhiteSpace(findings))
        {
            return new ScenarioResult(null, "Web research returned no findings to synthesize");
        }

        // ---- Phase 2: synthesize findings into a NudgeDraft (no tools, JSON format) ----
        string synthesisUserText = BuildSynthesisUserText(trail, findings, now);

        var synthesisParams = new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 4000,
            System = SynthesisSystemPrompt,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High,
                Format = new JsonOutputFormat { Schema = ScenarioPromptHelpers.BuildNudgeDraftSchema() },
            },
            Messages = new List<MessageParam>
            {
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = synthesisUserText },
                    },
                },
            },
        };

        Message synthesisResponse = await client.Messages.Create(synthesisParams, cancellationToken: ct).ConfigureAwait(false);

        string? text = synthesisResponse.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault();

        ScenarioDiagnostics.LogRun(
            Key + ":synthesis",
            ModelId.ToString(),
            SynthesisSystemPrompt,
            synthesisUserText,
            text,
            synthesisResponse.Usage?.InputTokens,
            synthesisResponse.Usage?.OutputTokens);

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

    private string BuildResearchUserText(IReadOnlyList<Moment> trail, IReadOnlyList<Nudge> priorNudges, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously recommended (do not repeat)");
        ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize);

        sb.Append("Infer how this user currently works and what they keep doing manually, ");
        sb.Append("then research the current best practices in dev workflow & tooling that apply. ");
        sb.Append("Summarize your findings with named tools/practices and cited sources, per the system prompt. ");
        sb.Append("Do not pick a single recommendation yet.");
        return sb.ToString();
    }

    private string BuildSynthesisUserText(IReadOnlyList<Moment> trail, string findings, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize);

        sb.AppendLine("Research findings from phase 1 (named tools/practices and sources):");
        sb.AppendLine(findings);
        sb.AppendLine();
        sb.Append("From these findings, name ONE concrete efficiency improvement specific to how this ");
        sb.Append("user works, or stay silent per the system prompt. ");
        sb.Append("Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }
}
