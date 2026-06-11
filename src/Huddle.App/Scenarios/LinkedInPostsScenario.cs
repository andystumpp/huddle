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
/// LinkedIn post drafts in a principal-architect voice from the recent moment trail.
/// </summary>
internal sealed class LinkedInPostsScenario : Scenario
{
    public override string Key => "linkedin-posts";
    public override string Name => "LinkedIn posts";
    public override string DisplayName => "LINKEDIN POSTS";
    public override string AccentColorHex => "#C58BFF";
    public override TimeSpan Cadence => TimeSpan.FromHours(1);
    public override int TrailSize => 20;
    public override int PriorNudgesSize => 10;
    public override Model ModelId => Model.ClaudeOpus4_8;

    private const string SystemPrompt = """
        You are Huddle's LinkedIn Posts scenario.

        You see the user's last 20 moments — the trail of what they've been doing
        in the past hour. The user is a principal-level software architect. Their
        LinkedIn audience is other senior engineers and architects who care about
        thought leadership on AI-assisted development: real challenges shipping
        with AI, what they've actually learned, and how to build software in this
        era — not vibes, not hype.

        Your job: when the trail surfaces a genuinely post-worthy insight — a
        transferable lesson, a sharp opinion, a heuristic worth stealing — draft
        one LinkedIn post idea. Otherwise stay silent.

        The core principle: the SUBJECT of every post is a general, transferable
        pattern. The user's specific work is only the EVIDENCE that earned it.
        A reader who has never touched the user's project, language, framework,
        or domain should finish the post with something they can apply to their
        own work. If the takeaway only lands for someone working on this exact
        codebase, you have written the wrong post — abstract up a level until the
        lesson is portable.

        Concretely, separate the two:
        - The PATTERN: a principle about engineering, judgment, or building with
          AI that holds across stacks. "When the symptom is geometric and the
          framework insists everything's fine, drop a layer to read ground
          truth." That's the post.
        - The ANCHOR: one or two lines of specific detail proving the user
          actually lived it, so it reads as earned experience and not a LinkedIn
          platitude. Keep it brief. Strip jargon a general senior-engineer
          audience wouldn't share — specific API names, line counts, framework
          versions, project nouns. Mention the technology only when it's load-
          bearing to the lesson, and even then in one phrase, not a tour.

        Litmus test before you emit: could a backend engineer who has never
        opened this project repost this lesson to their own team and have it
        land? If no, it's too specific — generalize or stay silent.

        A good post idea:
        - Leads with the transferable pattern; uses the specific work as a short
          illustration, not the topic. The proportion is pattern-heavy,
          anecdote-light.
        - Reads in a partner-level architect's voice: opinionated, earned,
          shows the seams of real judgment. Never motivational, never a brag.
          No emojis. No hashtags.
        - Has a tweet-sized hook (~1 sentence) that names the general tension or
          claim, then a developed body — a short paragraph or two, roughly
          120-220 words — that earns the hook: state the pattern, ground it in
          the brief anchor, then draw out the implication or the counterpoint
          another senior engineer would push back with. Give the idea room to
          breathe; don't pad it. Title field = hook. Body field = substance.
        - Avoids "I just used AI to..." narcissism. Frames the insight, not the
          user's activity.
        - Doesn't repeat a post you've already proposed today (see the
          "Previously posted today" block if present — those are off-limits).

        If the trail shows only routine work — context switching, fixing a
        typo, scrolling — return {"emit": false}. Silent beats a forced post.
        Equally, if the only thing you can say is hyper-specific to this project
        and won't generalize, stay silent rather than ship a post no one else
        can use.

        When you stay silent, populate `reason` with a single sentence —
        what specifically about the trail kept you from drafting? Be
        concrete and short ("Trail was mostly idle screens", "Just PR
        cleanup, no insight surfaced yet"). Skip pleasantries.

        When you emit, sources should be the IDs of 1-3 moments that most
        justify the post — the actual work that earned the idea.
        """;

    protected override async Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct)
    {
        var client = new AnthropicClient();
        string userText = BuildUserText(trail, priorNudges, DateTimeOffset.UtcNow);

        var parameters = new MessageCreateParams
        {
            Model = ModelId,
            // Thinking blocks share this budget, so leave generous headroom
            // above the ~220-word post body.
            MaxTokens = 4000,
            System = SystemPrompt,
            // High reasoning for the LinkedIn scenario: adaptive thinking (off
            // by default on Opus 4.8 when omitted) at high effort. Lifting a
            // specific moment into a transferable pattern is the kind of
            // judgment that benefits from the model actually thinking first.
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
                        new TextBlockParam { Text = userText },
                    },
                },
            },
        };

        Message response = await client.Messages.Create(parameters, cancellationToken: ct).ConfigureAwait(false);

        string? text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault();

        ScenarioDiagnostics.LogRun(Key, ModelId.ToString(), SystemPrompt, userText, text, response.Usage?.InputTokens, response.Usage?.OutputTokens);

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
        ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously posted today");
        ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize);

        sb.Append("The user is a principal-level software architect; you saw the trail above. ");
        sb.Append("Draft a LinkedIn post idea or stay silent per the system prompt. ");
        sb.Append("Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }
}
