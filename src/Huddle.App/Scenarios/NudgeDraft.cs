using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Huddle.Scenarios;

/// <summary>
/// Structured-output target for scenario Claude calls. The model returns either
/// <c>{ "emit": false, "reason": "..." }</c> or the full nudge fields with
/// <c>emit: true</c>.
/// </summary>
internal sealed record NudgeDraft(
    [property: JsonPropertyName("emit")] bool Emit,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("sources")] IReadOnlyList<string>? Sources);
