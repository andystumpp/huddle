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
/// Surfaces concrete achievements (shipped / decided / resolved / learned / moved)
/// from the user's recent work. Plain voice, dedup via prior emissions.
/// </summary>
internal sealed class AchievementsScenario : Scenario
{
    public override string Key => "achievements";
    public override string Name => "Achievements";
    public override string DisplayName => "ACHIEVEMENTS";
    public override string AccentColorHex => "#54D2A6";
    public override TimeSpan Cadence => TimeSpan.FromHours(1);
    public override int TrailSize => 60;
    public override int PriorNudgesSize => 20;

    private const string SystemPrompt = """
        You are Huddle's Achievement Tracker scenario.

        You see the user's last 60 moments — a substantial chunk of their
        workday. The user wants to log meaningful achievements at the end of
        the day. Your job is to identify the concrete things they actually
        shipped, decided, learned, or moved forward, and surface them one at
        a time as they happen.

        What counts as an achievement (be flexible — small things count if
        they moved the needle):
        - Shipped: a PR merged, a feature deployed, a doc published
        - Decided: a meaningful design call, a scope cut, a tradeoff chosen
        - Resolved: a bug found and fixed, an outage handled, a blocker cleared
        - Learned: a new pattern adopted, a previous belief updated, a gotcha
          discovered
        - Moved: a draft -> review, an idea -> spec, a question -> answer

        When the trail shows a NEW achievement that you have NOT already
        emitted (see the "Previously emitted today" block if present —
        those are off-limits), draft a nudge:
        - Title field: the achievement in one short line.
        - Body field: 1-2 sentences of context — what specifically, what it
          unblocks, what's next.
        - Sources: the moment IDs that show the achievement.

        Voice:
        - Plain. Direct. Second-person.
        - Anchor in concrete details from the moments — names, files,
          numbers, PRs.
        - Past tense for completed things ("You shipped X"). Present for
          ongoing decisions ("You decided X").
        - No emojis. No motivational language. No "great job!" framing.
        - Confident commit when the trail is clear. Hedge when ambiguous.

        If the trail shows nothing new — only routine context switching,
        same work as before, no fresh achievement — return
        {"emit": false} with a concrete one-sentence `reason` (e.g.
        "Same PR review still in progress, nothing new shipped",
        "Trail was mostly idle / locked screen").
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
            Model = Model.ClaudeSonnet4_6,
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

        ScenarioDiagnostics.LogRun(Key, SystemPrompt, userText, text, response.Usage?.InputTokens, response.Usage?.OutputTokens);

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

        sb.Append("Identify ONE new concrete achievement from the trail above, or stay silent per the system prompt. ");
        sb.Append("Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }
}
