using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Huddle.Scenarios;

/// <summary>
/// One local CLI that Huddle drives for both vision and scenarios. A machine runs
/// entirely on one configured provider (Claude, Copilot, or Agency); there is no
/// API/SDK path. <see cref="CompleteAsync"/> serves scenarios (text in, NudgeDraft
/// JSON out); <see cref="DescribeImageAsync"/> serves the vision tick (a screenshot
/// path + prompt in, an intent summary out).
/// </summary>
internal interface ICliProvider
{
    Task<BackendResult> CompleteAsync(ScenarioRequest request, CancellationToken ct);

    /// <summary>
    /// Attach the screenshot at <paramref name="imagePath"/> to a single non-interactive
    /// prompt and return the model's text (null on failure). The caller owns the temp
    /// file's lifetime; this method only reads it.
    /// </summary>
    Task<string?> DescribeImageAsync(string imagePath, string prompt, CancellationToken ct);
}

/// <summary>The high-reasoning knob. Maps to Claude's <c>--effort</c>; Copilot ignores it.</summary>
internal enum Effort { Low, Medium, High, XHigh, Max }

/// <summary>
/// The inputs for one scenario completion. <see cref="JsonSchema"/> is required —
/// every scenario asks the model for a <see cref="NudgeDraft"/> object.
/// <see cref="Model"/> is a provider-native model-name string (e.g. an alias the
/// Claude CLI understands); the Copilot provider uses its configured model instead.
/// <see cref="Effort"/> is the optional high-reasoning knob (Claude only).
/// <see cref="WebSearch"/> asks a capable provider to ground its answer in a live
/// search; a provider without that capability runs ungrounded or emits nothing.
/// </summary>
internal sealed record ScenarioRequest(
    string Model,
    int MaxTokens,
    string SystemPrompt,
    string UserText,
    Dictionary<string, JsonElement> JsonSchema,
    Effort? Effort = null,
    bool WebSearch = false);

/// <summary>
/// The assistant text plus token usage. Token counts are null on CLI providers
/// (plain-text output carries no usage); <see cref="ScenarioDiagnostics"/> already
/// tolerates nulls.
/// </summary>
internal readonly record struct BackendResult(string? Text, long? InputTokens, long? OutputTokens);
