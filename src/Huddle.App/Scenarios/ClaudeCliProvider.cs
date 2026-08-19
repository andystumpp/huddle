using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Huddle.Scenarios;

/// <summary>
/// The <c>claude</c> CLI provider. Scenarios run in print mode with the prompt on
/// stdin (a large trail blows the Windows command-line length limit as an argument);
/// vision passes an <c>@&lt;path&gt;</c> image reference in the prompt. There is no
/// structured-output flag, so the NudgeDraft schema is requested via the system
/// prompt and stdout is that JSON. ANTHROPIC_API_KEY is scrubbed from the child
/// environment so Claude Code authenticates against the user's subscription.
/// </summary>
internal sealed class ClaudeCliProvider : ICliProvider
{
    private const string Command = "claude";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(180);
    // The agentic web-search loop (search → read → synthesize) runs longer than a
    // plain completion, so give it more headroom.
    private static readonly TimeSpan WebSearchTimeout = TimeSpan.FromSeconds(360);
    private static readonly TimeSpan VisionTimeout = TimeSpan.FromSeconds(120);

    public async Task<BackendResult> CompleteAsync(ScenarioRequest request, CancellationToken ct)
    {
        string system = request.SystemPrompt + ScenarioPromptHelpers.BuildSchemaDirective(request.JsonSchema);

        var psi = NewStartInfo(redirectStdin: true);
        // The prompt goes on stdin, NOT as a `-p <text>` argument: a large trail
        // (Learnings sends ~200 moments ≈ 60K chars) blows the Windows command-line
        // length limit (~32K), which made Process.Start throw and the call return null.
        psi.ArgumentList.Add("-p");
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

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] ClaudeCliProvider could not start claude: {ex.GetType().Name}: {ex.Message}");
            return new BackendResult(null, null, null);
        }

        // Drain stdout/stderr before writing stdin so a large prompt or response
        // can't deadlock the pipes.
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        // Feed the prompt on stdin, then close to signal EOF.
        try
        {
            await process.StandardInput.WriteAsync(request.UserText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] ClaudeCliProvider stdin write failed: {ex.Message}");
        }
        finally
        {
            try { process.StandardInput.Close(); } catch { /* already gone */ }
        }

        TimeSpan budget = request.WebSearch ? WebSearchTimeout : Timeout;
        string? stdout = await WaitForOutputAsync(process, stdoutTask, stderrTask, budget, ct).ConfigureAwait(false);
        return new BackendResult(stdout?.Trim(), null, null);
    }

    public async Task<string?> DescribeImageAsync(string imagePath, string prompt, CancellationToken ct)
    {
        var psi = NewStartInfo(redirectStdin: false);
        // The vision prompt is small, so it rides on -p as an argument; the @<path>
        // reference attaches the image (verified: no tools needed).
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add($"{prompt} @{imagePath}");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add("sonnet");

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] ClaudeCliProvider vision could not start claude: {ex.GetType().Name}: {ex.Message}");
            return null;
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        try { process.StandardInput.Close(); } catch { /* not redirected */ }

        string? stdout = await WaitForOutputAsync(process, stdoutTask, stderrTask, VisionTimeout, ct).ConfigureAwait(false);
        return stdout?.Trim();
    }

    private static ProcessStartInfo NewStartInfo(bool redirectStdin)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Command,
            RedirectStandardInput = redirectStdin,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // claude emits UTF-8; without this .NET decodes stdout with the console's
            // default codepage and mangles non-ASCII (em dashes, curly quotes, accents)
            // into mojibake that persists into the nudge / summary.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (redirectStdin) psi.StandardInputEncoding = Encoding.UTF8;
        // Force the subscription session: Claude Code prefers ANTHROPIC_API_KEY;
        // inheriting one would silently bill the metered API and defeat this provider.
        psi.Environment.Remove("ANTHROPIC_API_KEY");
        return psi;
    }

    private static async Task<string?> WaitForOutputAsync(
        Process process, Task<string> stdoutTask, Task<string> stderrTask, TimeSpan budget, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(budget);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            Debug.WriteLine("[Huddle] ClaudeCliProvider timed out / cancelled");
            return null;
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            Debug.WriteLine($"[Huddle] ClaudeCliProvider exit {process.ExitCode}: {stderr.Trim()}");
            return null;
        }
        return stdout;
    }

    /// <summary>Maps a model-name string to a CLI alias. Throws rather than guess.</summary>
    private static string ModelAlias(string model)
    {
        string s = model.ToLowerInvariant();
        if (s.Contains("opus")) return "opus";
        if (s.Contains("sonnet")) return "sonnet";
        if (s.Contains("haiku")) return "haiku";
        throw new NotSupportedException($"No CLI model alias for '{model}'");
    }
}
