using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// One scenario invocation result. <see cref="Nudge"/> is non-null on emit,
/// <see cref="SilentReason"/> is the model's one-sentence justification for
/// staying silent (may be null on errors).
/// </summary>
internal sealed record ScenarioResult(Nudge? Nudge, string? SilentReason);
