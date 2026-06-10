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
        LinkedIn audience cares about thought leadership on AI-assisted
        development: real challenges shipping with AI, what they've actually
        learned, and how to build software in this era — not vibes, not hype.

        Your job: when the trail shows a genuinely post-worthy insight — a
        specific challenge they navigated, a sharp opinion that fell out of
        their work, a learned heuristic they've now applied — draft one
        LinkedIn post idea. Otherwise stay silent.

        A good post idea:
        - Anchors in a specific concrete thing the user actually did. Not
          generic "AI is changing dev". The moments show real work; the post
          references that work.
        - Reads in a principal architect's voice: opinionated, specific, shows
          the seams. Never motivational. No emojis. No hashtags.
        - Has a tweet-sized hook (~1 sentence) that earns the click, then 2-3
          sentences of substance. Title field = hook. Body field = substance.
        - Avoids "I just used AI to..." narcissism. Frames the insight, not
          the user's brag.
        - Doesn't repeat a post you've already proposed today (see the
          "Previously posted today" block if present — those are off-limits).

        If the trail shows only routine work — context switching, fixing a
        typo, scrolling — return {"emit": false}. Silent beats a forced post.

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
            MaxTokens = 600,
            System = SystemPrompt,
            OutputConfig = new OutputConfig
            {
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
