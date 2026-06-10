using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using Huddle.Models;
using Huddle.Vision;

namespace Huddle.Scenarios;

/// <summary>
/// One scenario invocation result. <see cref="Nudge"/> is non-null on emit,
/// <see cref="SilentReason"/> is the model's one-sentence justification for
/// staying silent (may be null on errors or older runs).
/// </summary>
internal sealed record ScenarioResult(Nudge? Nudge, string? SilentReason);

/// <summary>
/// First scenario: drafts LinkedIn post ideas in a principal-architect voice
/// from the user's last 20 moments. Throttled at one run per hour (in-memory).
/// </summary>
internal static class LinkedInPostsScenario
{
    public const string Key = "linkedin-posts";
    public const string Name = "LinkedIn posts";
    public static readonly TimeSpan Cadence = TimeSpan.FromHours(1);
    public const int TrailSize = 20;

    private static DateTimeOffset s_lastRun = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    public static bool IsDue(DateTimeOffset now) => now - s_lastRun >= Cadence;

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
        - Doesn't repeat a post you would have proposed from earlier moments
          in the trail (the user has seen those already).

        If the trail shows only routine work — context switching, fixing a
        typo, scrolling — return {"emit": false}. Silent beats a forced post.

        When you stay silent, populate `reason` with a single sentence —
        what specifically about the trail kept you from drafting? Be
        concrete and short ("Trail was mostly idle screens", "Just PR
        cleanup, no insight surfaced yet"). Skip pleasantries.

        When you emit, sources should be the IDs of 1-3 moments that most
        justify the post — the actual work that earned the idea.
        """;

    public static async Task<ScenarioResult> RunAsync(IReadOnlyList<Moment> trail, CancellationToken ct = default)
    {
        // Serialize concurrent attempts (the tick fires once per 3 min, so
        // contention is theoretical; this just keeps the throttle honest).
        await s_gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            s_lastRun = DateTimeOffset.UtcNow;

            var client = new AnthropicClient();
            string userText = BuildUserText(trail, DateTimeOffset.UtcNow);

            var parameters = new MessageCreateParams
            {
                Model = Model.ClaudeSonnet4_6,
                MaxTokens = 600,
                System = SystemPrompt,
                OutputConfig = new OutputConfig
                {
                    Format = new JsonOutputFormat { Schema = BuildSchema() },
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

            Message response = await client.Messages.Create(parameters, cancellationToken: ct)
                .ConfigureAwait(false);

            string? text = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();

            LogRun(SystemPrompt, userText, text, response.Usage?.InputTokens, response.Usage?.OutputTokens);

            if (string.IsNullOrWhiteSpace(text)) return new ScenarioResult(null, null);

            NudgeDraft? draft = JsonSerializer.Deserialize<NudgeDraft>(text);
            if (draft is null) return new ScenarioResult(null, null);

            if (!draft.Emit)
            {
                return new ScenarioResult(null, NormalizeWhitespace(draft.Reason ?? ""));
            }

            if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.Body))
            {
                return new ScenarioResult(null, "Model emitted but title/body was empty");
            }

            var nudge = new Nudge(
                Id: UlidGenerator.Generate(),
                Ts: DateTimeOffset.UtcNow,
                Scenario: Key,
                Title: draft.Title.Trim(),
                Body: draft.Body.Trim(),
                Sources: draft.Sources ?? Array.Empty<string>());
            return new ScenarioResult(nudge, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] LinkedIn scenario failed: {ex.GetType().Name}: {ex.Message}");
            return new ScenarioResult(null, null);
        }
        finally
        {
            s_gate.Release();
        }
    }

    /// <summary>
    /// Appends a verbatim record of one scenario run (system prompt, user
    /// message, raw response, usage) to %LOCALAPPDATA%\Huddle\scenarios.log.
    /// Diagnostic only; no truncation.
    /// </summary>
    private static void LogRun(string systemPrompt, string userText, string? response, long? inputTokens, long? outputTokens)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Huddle");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "scenarios.log");

            var sb = new StringBuilder();
            sb.AppendLine("=================================================================");
            sb.AppendLine($"[{DateTimeOffset.UtcNow:o}] scenario={Key} model=claude-sonnet-4-6");
            sb.AppendLine($"usage: input={inputTokens?.ToString() ?? "?"} output={outputTokens?.ToString() ?? "?"}");
            sb.AppendLine("--- system prompt ---");
            sb.AppendLine(systemPrompt);
            sb.AppendLine("--- user message ---");
            sb.AppendLine(userText);
            sb.AppendLine("--- raw response ---");
            sb.AppendLine(response ?? "(null)");
            sb.AppendLine();

            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // Diagnostic log is best-effort.
        }
    }

    private static string BuildUserText(IReadOnlyList<Moment> trail, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Recent moments (newest first):");
        int count = Math.Min(trail.Count, TrailSize);
        for (int i = 0; i < count; i++)
        {
            var m = trail[i];
            sb.Append("- ")
              .Append(FormatRelativeTime(m.Ts, now))
              .Append(", ")
              .Append(m.App)
              .Append(" (\"")
              .Append(AbbreviateTitle(m.WindowTitle, 80))
              .Append("\"), id=")
              .Append(m.Id)
              .Append(": ")
              .AppendLine(NormalizeWhitespace(m.Summary));
        }
        sb.AppendLine();
        sb.Append("The user is a principal-level software architect; you saw the trail above. ");
        sb.Append("Draft a LinkedIn post idea or stay silent per the system prompt. ");
        sb.Append("Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }

    private static Dictionary<string, JsonElement> BuildSchema()
    {
        // JSON schema for the structured output. emit is required; the rest are
        // expected when emit=true, but we let the model omit them on silent.
        var properties = new
        {
            emit = new { type = "boolean" },
            reason = new { type = "string" },
            title = new { type = "string" },
            body = new { type = "string" },
            sources = new { type = "array", items = new { type = "string" } },
        };
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["properties"] = JsonSerializer.SerializeToElement(properties),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "emit" }),
        };
    }

    private static string FormatRelativeTime(DateTimeOffset ts, DateTimeOffset now)
    {
        double minutes = (now - ts).TotalMinutes;
        if (minutes < 1) return "just now";
        if (minutes < 60) return $"{(int)minutes} min ago";
        int hours = (int)(minutes / 60);
        return $"{hours} h ago";
    }

    private static string AbbreviateTitle(string title, int max)
    {
        title = NormalizeWhitespace(title);
        if (title.Length <= max) return title;
        int spaceBreak = title.LastIndexOf(' ', max);
        if (spaceBreak > max / 2) return title.Substring(0, spaceBreak) + "…";
        return title.Substring(0, max) + "…";
    }

    private static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        bool prevSpace = false;
        foreach (char c in s)
        {
            if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }
}
