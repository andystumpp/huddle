using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Huddle.Scenarios;

/// <summary>
/// The GitHub Copilot CLI provider (also used for Agency, which wraps Copilot with
/// the same input — the command name comes from config). Copilot has no system-prompt
/// flag and takes its prompt as an argument, so a large scenario prompt (Learnings'
/// ~64K trail) can't ride on <c>-p</c>. Instead the assembled prompt is written to a
/// temporary <c>.md</c> file and Copilot reads it under a narrow read-only tool grant
/// (verified); the file is deleted afterward, like the vision screenshot. Vision uses
/// <c>--attachment</c>. Copilot authenticates through its own Entra-backed login — no
/// API keys.
/// </summary>
internal sealed class CopilotCliProvider : ICliProvider
{
    private readonly string _command;
    private readonly string _model;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan WebSearchTimeout = TimeSpan.FromSeconds(360);
    private static readonly TimeSpan VisionTimeout = TimeSpan.FromSeconds(120);

    public CopilotCliProvider(string command, string model)
    {
        _command = command;
        _model = model;
    }

    public async Task<BackendResult> CompleteAsync(ScenarioRequest request, CancellationToken ct)
    {
        // Copilot has no --system flag, so system + schema directive + user text are
        // one document. It exceeds the command-line limit for large trails, so it goes
        // to a temp .md that Copilot reads via --allow-tool=read.
        string prompt = request.SystemPrompt
            + ScenarioPromptHelpers.BuildSchemaDirective(request.JsonSchema)
            + "\n\n"
            + request.UserText;

        string tempDir = Path.GetTempPath();
        string tempFile = Path.Combine(tempDir, $"huddle-prompt-{Guid.NewGuid():N}.md");
        try
        {
            await File.WriteAllTextAsync(tempFile, prompt, new UTF8Encoding(false), ct).ConfigureAwait(false);

            var psi = NewStartInfo();
            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add($"Read the file at {tempFile} and follow its instructions. Output only what it asks for, nothing else.");
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_model);
            psi.ArgumentList.Add("--no-ask-user");
            psi.ArgumentList.Add("--allow-tool=read");
            psi.ArgumentList.Add("--add-dir");
            psi.ArgumentList.Add(tempDir);
            if (request.Effort is { } effort)
            {
                // Copilot exposes the same reasoning-effort knob as Claude (--effort,
                // levels low..max); our Effort enum is a subset it accepts.
                psi.ArgumentList.Add("--effort");
                psi.ArgumentList.Add(effort.ToString().ToLowerInvariant());
            }
            if (request.WebSearch)
            {
                // Narrow url grant (fetch), never --allow-all-tools. Whether Copilot's
                // url tool truly *searches* vs only fetches is verified on the work
                // machine; the scenario never fakes a citation regardless.
                psi.ArgumentList.Add("--allow-tool=url");
            }

            TimeSpan budget = request.WebSearch ? WebSearchTimeout : Timeout;
            string? stdout = await RunAsync(psi, budget, ct).ConfigureAwait(false);
            // Even with -s, Copilot prefaces the read-tool run with prose ("I'll read
            // the file.") before the JSON, so isolate the first balanced JSON object
            // rather than hand the scenario the whole transcript to deserialize.
            return new BackendResult(ExtractJsonObject(stdout), null, null);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }

    public async Task<string?> DescribeImageAsync(string imagePath, string prompt, CancellationToken ct)
    {
        var psi = NewStartInfo();
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);
        psi.ArgumentList.Add("--attachment");
        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(_model);
        psi.ArgumentList.Add("--no-ask-user");

        string? stdout = await RunAsync(psi, VisionTimeout, ct).ConfigureAwait(false);
        return stdout?.Trim();
    }

    /// <summary>
    /// Return the first balanced JSON object in <paramref name="text"/> (Copilot wraps
    /// the response in conversational prose the schema directive can't fully suppress).
    /// Brace-counts while respecting string literals so a brace inside a body string
    /// doesn't end the scan early. Returns the trimmed input if no object is found, so
    /// the failure surfaces at deserialization rather than here.
    /// </summary>
    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        int start = text.IndexOf('{');
        if (start < 0) return text.Trim();

        int depth = 0;
        bool inString = false, escape = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escape) { escape = false; continue; }
            if (c == '\\') { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return text.Substring(start, i - start + 1);
        }
        return text.Trim();
    }

    private ProcessStartInfo NewStartInfo()
    {
        var psi = new ProcessStartInfo
        {
            FileName = _command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        return psi;
    }

    private async Task<string?> RunAsync(ProcessStartInfo psi, TimeSpan budget, CancellationToken ct)
    {
        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] CopilotCliProvider could not start {_command}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(budget);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            Debug.WriteLine("[Huddle] CopilotCliProvider timed out / cancelled");
            return null;
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            Debug.WriteLine($"[Huddle] CopilotCliProvider exit {process.ExitCode}: {stderr.Trim()}");
            return null;
        }
        return stdout;
    }
}
