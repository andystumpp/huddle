using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Models.Messages;

namespace Huddle.Scenarios;

/// <summary>
/// The subscription path: runs the local <c>claude</c> CLI in print mode. There is
/// no structured-output flag on the CLI, so the NudgeDraft schema is requested via
/// the system prompt; stdout is that JSON object, returned as-is for the scenario
/// to deserialize. ANTHROPIC_API_KEY is scrubbed from the child environment so
/// Claude Code authenticates against the user's subscription rather than the
/// metered key (which MomentExtractor promotes into the process env).
/// </summary>
internal sealed class CliBackend : IScenarioBackend
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(180);
    // The agentic web-search loop (search → read → synthesize) runs longer than a
    // plain completion, so give it more headroom.
    private static readonly TimeSpan WebSearchTimeout = TimeSpan.FromSeconds(360);

    public async Task<BackendResult> CompleteAsync(ScenarioRequest request, CancellationToken ct)
    {
        string system = request.SystemPrompt + BuildSchemaDirective(request.JsonSchema);

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // claude emits UTF-8; without this .NET decodes stdout with the
            // console's default codepage and mangles non-ASCII (em dashes,
            // curly quotes, accents) into mojibake that persists into the nudge.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(request.UserText);
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(ModelAlias(request.Model));
        psi.ArgumentList.Add("--append-system-prompt");
        psi.ArgumentList.Add(system);
        if (request.Effort is { } effort)
        {
            psi.ArgumentList.Add("--effort");
            psi.ArgumentList.Add(effort.ToString().ToLowerInvariant());
        }
        if (request.WebSearch)
        {
            // Limit tool AVAILABILITY to read-only search, then bypass permission
            // prompts (an allow-list alone does not run tools in headless -p mode).
            // With only WebSearch/WebFetch available, the bypass can't reach shell
            // or file-writing tools. The CLI's WebSearch is client-side, so this
            // draws on the subscription, not the metered API.
            psi.ArgumentList.Add("--tools");
            psi.ArgumentList.Add("WebSearch");
            psi.ArgumentList.Add("WebFetch");
            psi.ArgumentList.Add("--dangerously-skip-permissions");
        }
        // Force the subscription session: Claude Code prefers ANTHROPIC_API_KEY,
        // which MomentExtractor promotes into the process env — inheriting it here
        // would silently bill the metered API and defeat this backend.
        psi.Environment.Remove("ANTHROPIC_API_KEY");

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] CliBackend could not start claude: {ex.GetType().Name}: {ex.Message}");
            return new BackendResult(null, null, null);
        }

        // The prompt is passed via -p, so there is no stdin payload. Close it
        // immediately to signal EOF — a GUI parent has no console stdin, and
        // otherwise `claude` waits on it ("no stdin data received in 3s…").
        try { process.StandardInput.Close(); } catch { /* stream already gone */ }

        // Read both streams before waiting so a large payload can't deadlock the pipe.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(request.WebSearch ? WebSearchTimeout : Timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            Debug.WriteLine("[Huddle] CliBackend timed out / cancelled");
            return new BackendResult(null, null, null);
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            Debug.WriteLine($"[Huddle] CliBackend exit {process.ExitCode}: {stderr.Trim()}");
            return new BackendResult(null, null, null);
        }

        return new BackendResult(stdout.Trim(), null, null);
    }

    private static string BuildSchemaDirective(Dictionary<string, JsonElement> schema)
    {
        string json = JsonSerializer.Serialize(schema);
        return "\n\nRespond with a single JSON object matching this schema and nothing else "
             + "— no prose, no markdown, no code fences:\n" + json;
    }

    /// <summary>Maps the SDK model to a CLI alias. Throws rather than guess.</summary>
    private static string ModelAlias(Model model)
    {
        string s = model.ToString().ToLowerInvariant();
        if (s.Contains("opus")) return "opus";
        if (s.Contains("sonnet")) return "sonnet";
        if (s.Contains("haiku")) return "haiku";
        throw new NotSupportedException($"No CLI model alias for '{model}'");
    }
}
