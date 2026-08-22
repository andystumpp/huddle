using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Huddle.Config;

namespace Huddle.Scenarios;

/// <summary>
/// The active scenarios, built once from the <c>scenarios</c> array in
/// <c>huddle.config.json</c> — there are no built-in scenarios, so an empty config yields
/// no scenarios (moments are still captured, but no nudges). Invalid definitions are
/// skipped with a warning so one typo never takes down capture or the others. The panel
/// iterates this list per tick; the nudge card reads display info via <see cref="GetByKey"/>.
/// </summary>
internal static class ScenarioRegistry
{
    private static readonly Lazy<IReadOnlyList<Scenario>> s_all = new(Compose);

    public static IReadOnlyList<Scenario> All => s_all.Value;

    public static Scenario? GetByKey(string key) =>
        All.FirstOrDefault(s => s.Key == key);

    private static IReadOnlyList<Scenario> Compose()
    {
        var result = new List<Scenario>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in HuddleConfig.Current.Scenarios)
        {
            string? error = Validate(def, seen, out Effort? effort);
            if (error is not null)
            {
                Debug.WriteLine($"[Huddle] scenario config: skipping '{def.Key}' — {error}");
                continue;
            }
            seen.Add(def.Key);
            result.Add(new ConfiguredScenario(def, effort));
        }
        return result;
    }

    /// <summary>Null when the definition is usable; otherwise the reason it was skipped.</summary>
    private static string? Validate(ScenarioDef def, HashSet<string> seen, out Effort? effort)
    {
        effort = null;
        if (string.IsNullOrWhiteSpace(def.Key)) return "missing 'key'";
        if (string.IsNullOrWhiteSpace(def.SystemPrompt)) return "missing 'systemPrompt'";
        if (seen.Contains(def.Key)) return $"duplicate 'key' '{def.Key}'";

        if (!string.IsNullOrWhiteSpace(def.Effort))
        {
            if (!Enum.TryParse<Effort>(def.Effort, ignoreCase: true, out var e))
                return $"unrecognized 'effort' value '{def.Effort}'";
            effort = e;
        }

        // Only the Claude provider maps the model to a CLI alias; Copilot/Agency use their
        // own model name (passed through, or the top-level model for bare aliases).
        if (HuddleConfig.Current.Provider == CliProviderKind.Claude && !HasClaudeAlias(def.Model))
            return $"'model' '{def.Model}' has no Claude CLI alias (use opus/sonnet/haiku)";

        return null;
    }

    private static bool HasClaudeAlias(string model)
    {
        string m = (model ?? string.Empty).ToLowerInvariant();
        return m.Contains("opus") || m.Contains("sonnet") || m.Contains("haiku");
    }
}
