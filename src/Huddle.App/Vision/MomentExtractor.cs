using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using Huddle.Capture;
using Huddle.Models;
using Huddle.Scenarios;

namespace Huddle.Vision;

/// <summary>
/// The result of one vision call: the intent summary (always free of sensitive values)
/// plus whether the frame showed sensitive content.
/// </summary>
internal readonly record struct MomentVision(string Summary, bool Sensitive);

/// <summary>
/// Sends a captured frame + foreground context to the configured CLI provider and
/// returns the 1-2 sentence moment summary plus a sensitivity flag. The screenshot is
/// written to a temporary file for the CLI to attach and deleted immediately after the
/// call — only the summary text is ever persisted (and only when policy allows).
/// </summary>
internal static class MomentExtractor
{
    private const string SystemPrompt = """
        You are Huddle's eye. You see one screenshot of what the user is currently looking
        at (their active window, or their full screen), the foreground app and window title,
        and brief summaries of the user's recent moments (prior captures, newest first).

        In a 1-2 sentence summary, infer what the user is currently trying to
        accomplish. Read the trail of recent moments for trajectory — what they've been
        doing the last several minutes tells you more about purpose than the one frame
        does.

        Voice:
        - Dry. Observant. Specific. Second-person.
        - Hedged when the trail doesn't pin it down ("you're likely...", "you seem to be...",
          "it looks like you're trying to..."). Confident when the trajectory is unambiguous.
        - Anchor in concrete details — name files, branches, tickets, specific UI states.
        - No greetings. No "I see". No "looks like" as a tic.
        - If nothing intentional seems to be happening (idle, browsing, between tasks), say
          so plainly — a single hedged sentence is fine.

        Frame the summary as intent ("you're trying to X" / "you're verifying X" / "you're
        likely shipping X") rather than description ("you're looking at X"). Do not propose
        what to do about it. Do not greet, summarize, or meta-comment.

        Sensitive content:
        - NEVER write specific sensitive values in the summary — no salaries, pay, bonuses,
          dollar amounts, account or card numbers, passwords, API keys, medical values or
          results, or personal identifiers (SSN, date of birth, home address). Describe the
          KIND of thing ("a compensation letter", "a bank statement", "a health portal"),
          never the values themselves.
        - Judge whether the frame shows sensitive personal, financial, health, credential,
          or PII content. When it does — or when you are unsure — mark it sensitive.

        Reply with ONLY a JSON object and nothing else (no prose, no markdown, no code
        fences):
        {"summary": "<the 1-2 sentence intent summary>", "sensitive": true or false}
        """;

    private static readonly ICliProvider s_provider = CliProviderFactory.Resolve();

    public static async Task<MomentVision> ExtractAsync(
        byte[] jpegBytes,
        ForegroundInfo foreground,
        IReadOnlyList<Moment> recent,
        CancellationToken ct = default)
    {
        // The screenshot is ephemeral: written for the CLI to attach, deleted in the
        // finally whether the call succeeds or fails. Only the summary is stored.
        string tempPath = Path.Combine(
            Path.GetTempPath(), $"huddle-frame-{Guid.NewGuid():N}.jpg");
        try
        {
            await File.WriteAllBytesAsync(tempPath, jpegBytes, ct).ConfigureAwait(false);

            string prompt = SystemPrompt + "\n\n"
                + BuildContextText(foreground, recent, DateTimeOffset.UtcNow);

            string? text = await s_provider.DescribeImageAsync(tempPath, prompt, ct)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new VisionCallException("CLI returned no summary text.");
            }
            return ParseVision(text);
        }
        catch (VisionCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VisionCallException($"{ex.GetType().Name}: {ex.Message}", ex);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Parse the model's JSON reply into a <see cref="MomentVision"/>. Isolates the first
    /// balanced JSON object (in case of stray prose) and reads <c>summary</c>/<c>sensitive</c>.
    /// If the reply is not JSON, the whole text is taken as the summary and treated as
    /// non-sensitive — the "never write values" prompt rule still protected that text.
    /// </summary>
    private static MomentVision ParseVision(string text)
    {
        string trimmed = text.Trim();
        int start = trimmed.IndexOf('{');
        int end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed.Substring(start, end - start + 1));
                var root = doc.RootElement;
                string? summary = root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
                    ? s.GetString()
                    : null;
                bool sensitive = false;
                if (root.TryGetProperty("sensitive", out var v))
                {
                    sensitive = v.ValueKind == JsonValueKind.True
                        || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b);
                }
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    return new MomentVision(summary.Trim(), sensitive);
                }
            }
            catch (JsonException) { /* fall through to raw-text fallback */ }
        }
        // Not JSON (or no summary field) — keep the moment, treat as non-sensitive.
        return new MomentVision(trimmed, false);
    }

    /// <summary>
    /// Build the context text: optional "Recent moments" trail (up to 6, newest
    /// first), then the current foreground line.
    /// </summary>
    private static string BuildContextText(
        ForegroundInfo foreground,
        IReadOnlyList<Moment> recent,
        DateTimeOffset now)
    {
        var sb = new StringBuilder();
        if (recent.Count > 0)
        {
            sb.AppendLine("Recent moments (newest first):");
            int count = Math.Min(recent.Count, 6);
            for (int i = 0; i < count; i++)
            {
                var m = recent[i];
                sb.Append("- ")
                  .Append(FormatRelativeTime(m.Ts, now))
                  .Append(", ")
                  .Append(m.App)
                  .Append(" (\"")
                  .Append(AbbreviateTitle(m.WindowTitle, 80))
                  .Append("\"): ")
                  .AppendLine(NormalizeWhitespace(m.Summary));
            }
            sb.AppendLine();
        }
        sb.Append("Foreground app: ").AppendLine(foreground.App);
        sb.Append("Window title: ").Append(foreground.WindowTitle);
        return sb.ToString();
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
        // Prefer breaking on a space near the limit.
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

internal sealed class VisionCallException : Exception
{
    public VisionCallException(string message) : base(message) { }
    public VisionCallException(string message, Exception inner) : base(message, inner) { }
}
