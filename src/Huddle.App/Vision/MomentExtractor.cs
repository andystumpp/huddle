using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using Huddle.Capture;
using Huddle.Models;

namespace Huddle.Vision;

/// <summary>
/// Sends a captured frame + foreground context to Claude and returns the
/// 1-2 sentence moment summary it produces.
/// </summary>
internal static class MomentExtractor
{
    private const string SystemPrompt = """
        You are Huddle's eye. You see one screenshot of the user's current screen, the
        foreground app and window title, and brief summaries of the user's recent moments
        (prior captures, newest first).

        In a single 1-2 sentence response, infer what the user is currently trying to
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

        Frame the response as intent ("you're trying to X" / "you're verifying X" / "you're
        likely shipping X") rather than description ("you're looking at X"). Do not propose
        what to do about it. Do not greet, summarize, or meta-comment.
        """;

    private static AnthropicClient? s_client;

    public static async Task<string> ExtractAsync(
        byte[] jpegBytes,
        ForegroundInfo foreground,
        IReadOnlyList<Moment> recent,
        CancellationToken ct = default)
    {
        try
        {
            var client = GetOrCreateClient();
            string base64Image = Convert.ToBase64String(jpegBytes);

            string contextText = BuildContextText(foreground, recent, DateTimeOffset.UtcNow);

            var parameters = new MessageCreateParams
            {
                Model = Model.ClaudeSonnet4_6,
                MaxTokens = 250,
                System = SystemPrompt,
                Messages = new List<MessageParam>
                {
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new ImageBlockParam
                            {
                                Source = new Base64ImageSource
                                {
                                    Data = base64Image,
                                    MediaType = "image/jpeg",
                                },
                            },
                            new TextBlockParam { Text = contextText },
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

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new VisionCallException("Response contained no text.");
            }
            return text.Trim();
        }
        catch (VisionCallException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new VisionCallException($"{ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Build the user-message text block: optional "Recent moments" trail (up to 6,
    /// newest first), then the current foreground line.
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

    private static AnthropicClient GetOrCreateClient()
    {
        if (s_client is not null) return s_client;

        var key = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new VisionCallException("ANTHROPIC_API_KEY not configured");
        }
        // Promote into process env so the SDK's automatic lookup finds it.
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key);
        s_client = new AnthropicClient();
        return s_client;
    }

    /// <summary>
    /// Resolves the API key from (in order): process env, User registry env target,
    /// `huddle.env` next to the exe, `huddle.env` at %LOCALAPPDATA%\Huddle\.
    /// </summary>
    private static string? ResolveApiKey()
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(key)) return key;

        try
        {
            key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(key)) return key;
        }
        catch { /* registry can throw in restricted contexts */ }

        foreach (var path in EnvFileCandidates())
        {
            key = ReadKeyFromEnvFile(path, "ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) return key;
        }
        return null;
    }

    private static IEnumerable<string> EnvFileCandidates()
    {
        var exeDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            yield return Path.Combine(exeDir, "huddle.env");
        }
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApp))
        {
            yield return Path.Combine(localApp, "Huddle", "huddle.env");
        }
    }

    private static string? ReadKeyFromEnvFile(string path, string name)
    {
        try
        {
            if (!File.Exists(path)) return null;
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var k = line.Substring(0, eq).Trim();
                if (!string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) continue;
                var v = line.Substring(eq + 1).Trim();
                if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
                {
                    v = v.Substring(1, v.Length - 2);
                }
                return v;
            }
        }
        catch { /* unreadable env file — ignore */ }
        return null;
    }
}

internal sealed class VisionCallException : Exception
{
    public VisionCallException(string message) : base(message) { }
    public VisionCallException(string message, Exception inner) : base(message, inner) { }
}
