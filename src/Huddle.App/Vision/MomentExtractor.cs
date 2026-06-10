using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using Huddle.Capture;

namespace Huddle.Vision;

/// <summary>
/// Sends a captured frame + foreground context to Claude and returns the
/// 1-2 sentence moment summary it produces.
/// </summary>
internal static class MomentExtractor
{
    private const string SystemPrompt = """
        You are Huddle's eye. You see one screenshot of the user's screen plus the name
        of the app and window in the foreground. Write a single 1-2 sentence observation
        about what the user is doing right now.

        Voice:
        - Dry. Observant. Specific. Second-person.
        - No greetings, no "I see", no "looks like".
        - Anchor in concrete details from the screen — not generic statements.
        - If nothing useful is happening, say so plainly in one sentence.

        Do not propose what to do about it. Just describe what's happening.
        """;

    private static AnthropicClient? s_client;

    public static async Task<string> ExtractAsync(
        byte[] jpegBytes,
        ForegroundInfo foreground,
        CancellationToken ct = default)
    {
        try
        {
            var client = GetOrCreateClient();
            string base64Image = Convert.ToBase64String(jpegBytes);

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
                            new TextBlockParam
                            {
                                Text = $"Foreground app: {foreground.App}\nWindow title: {foreground.WindowTitle}",
                            },
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
