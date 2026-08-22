using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Huddle.Config;
using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// A scenario whose metadata and prompt come from a <see cref="ScenarioDef"/> in
/// <c>huddle.config.json</c> rather than code. It runs the same trail → completion →
/// <see cref="NudgeDraft"/> pipeline as the built-in trail scenarios; the only difference
/// is that its system prompt is authored in config. The pipeline still enforces the
/// NudgeDraft JSON shape, so the config prompt only describes when to emit and the voice.
/// </summary>
internal sealed class ConfiguredScenario : Scenario
{
    private readonly ScenarioDef _def;
    private readonly Effort? _effort;

    public ConfiguredScenario(ScenarioDef def, Effort? effort)
    {
        _def = def;
        _effort = effort;
    }

    public override string Key => _def.Key;
    public override string DisplayName => _def.DisplayName;
    public override string AccentColorHex => _def.AccentColorHex;
    public override TimeSpan Cadence => TimeSpan.FromHours(_def.CadenceHours);
    public override int TrailSize => _def.TrailSize;
    public override int PriorNudgesSize => _def.PriorNudgesSize;
    public override string ModelId => _def.Model;

    protected override async Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct)
    {
        string userText = BuildUserText(trail, priorNudges, DateTimeOffset.UtcNow);

        var request = new ScenarioRequest(
            Model: ModelId,
            MaxTokens: 4000,
            SystemPrompt: _def.SystemPrompt,
            UserText: userText,
            JsonSchema: ScenarioPromptHelpers.BuildNudgeDraftSchema(),
            Effort: _effort,
            WebSearch: _def.WebSearch);

        BackendResult result = await Provider.CompleteAsync(request, ct).ConfigureAwait(false);
        string? text = result.Text;

        ScenarioDiagnostics.LogRun(Key, ModelId, _def.SystemPrompt, userText, text, result.InputTokens, result.OutputTokens);

        if (string.IsNullOrWhiteSpace(text)) return new ScenarioResult(null, null);

        NudgeDraft? draft = JsonSerializer.Deserialize<NudgeDraft>(text);
        if (draft is null) return new ScenarioResult(null, null);

        if (!draft.Emit)
        {
            return new ScenarioResult(null, ScenarioPromptHelpers.NormalizeWhitespace(draft.Reason ?? ""));
        }

        if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.Body))
        {
            return new ScenarioResult(null, "Model emitted but title/body was empty");
        }

        var nudge = new Nudge(
            Id: Huddle.Vision.UlidGenerator.Generate(),
            Ts: DateTimeOffset.UtcNow,
            Scenario: Key,
            Title: draft.Title.Trim(),
            Body: draft.Body.Trim(),
            Sources: draft.Sources ?? Array.Empty<string>());
        return new ScenarioResult(nudge, null);
    }

    private string BuildUserText(IReadOnlyList<Moment> trail, IReadOnlyList<Nudge> priorNudges, DateTimeOffset now)
    {
        var sb = new StringBuilder();
        ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously emitted by this scenario");
        ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize);
        sb.Append("Follow the system prompt: emit one nudge or stay silent. ");
        sb.Append("Sources should reference moment IDs from the trail (e.g. \"01KTQ...\").");
        return sb.ToString();
    }
}
