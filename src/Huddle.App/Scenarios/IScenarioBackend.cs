using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Models.Messages;

namespace Huddle.Scenarios;

/// <summary>
/// One scenario Claude call: a system prompt plus a single text user message,
/// returning text whose content is a <see cref="NudgeDraft"/> JSON object. Two
/// implementations — <see cref="ApiBackend"/> (metered Anthropic SDK) and
/// <see cref="CliBackend"/> (the user's subscription, via the local `claude`
/// CLI). The vision path does not use this seam.
/// </summary>
internal interface IScenarioBackend
{
    Task<BackendResult> CompleteAsync(ScenarioRequest request, CancellationToken ct);
}

/// <summary>
/// The inputs for one scenario completion. <see cref="JsonSchema"/> is required —
/// every scenario asks the model for a <see cref="NudgeDraft"/> object.
/// <see cref="Effort"/> is the optional high-reasoning knob: null is a plain call;
/// when set, the API backend applies both the effort level and adaptive thinking,
/// and the CLI backend passes <c>--effort</c>. <see cref="WebSearch"/> lets a
/// scenario ask the CLI backend to ground its answer in a live web search
/// (off-meter, agentic); the API backend does not honor it.
/// </summary>
internal sealed record ScenarioRequest(
    Model Model,
    int MaxTokens,
    string SystemPrompt,
    string UserText,
    Dictionary<string, JsonElement> JsonSchema,
    Effort? Effort = null,
    bool WebSearch = false);

/// <summary>
/// The assistant text plus token usage. Token counts are null on the CLI backend
/// (plain-text output carries no usage); <see cref="ScenarioDiagnostics"/> already
/// tolerates nulls.
/// </summary>
internal readonly record struct BackendResult(string? Text, long? InputTokens, long? OutputTokens);
