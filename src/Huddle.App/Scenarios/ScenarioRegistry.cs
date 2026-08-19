using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Huddle.Config;

namespace Huddle.Scenarios;

/// <summary>
/// Single source of truth for the active scenarios. The set is composed once from the
/// built-in scenarios and the <c>scenarios</c> section of <c>huddle.config.json</c>:
/// built-ins whose key is not disabled, followed by one <see cref="ConfiguredScenario"/>
/// per valid custom definition. Invalid custom definitions are skipped with a warning so
/// one typo never takes down capture or the other scenarios. The panel iterates this list
/// per tick; the nudge card reads display info via <see cref="GetByKey"/>.
/// </summary>
internal static class ScenarioRegistry
{
    private static readonly Lazy<IReadOnlyList<Scenario>> s_all = new(Compose);

    public static IReadOnlyList<Scenario> All => s_all.Value;

    public static Scenario? GetByKey(string key) =>
        All.FirstOrDefault(s => s.Key == key);

    private static IReadOnlyList<Scenario> Compose()
    {
        var builtins = new Scenario[]
        {
            new LinkedInPostsScenario(),
            new AchievementsScenario(),
            new LearningsScenario(),
            new EfficiencyInsightsScenario(),
        };

        var cfg = HuddleConfig.Current.Scenarios;
        var disabled = new HashSet<string>(cfg.Disabled, StringComparer.OrdinalIgnoreCase);
        var builtinKeys = new HashSet<string>(builtins.Select(b => b.Key), StringComparer.OrdinalIgnoreCase);

        var result = new List<Scenario>();
        foreach (var b in builtins)
        {
            if (!disabled.Contains(b.Key)) result.Add(b);
        }

        var customKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in cfg.Custom)
        {
            string? error = Validate(def, builtinKeys, customKeys, out Effort? effort);
            if (error is not null)
            {
                Debug.WriteLine($"[Huddle] scenario config: skipping custom '{def.Key}' — {error}");
                continue;
            }
            customKeys.Add(def.Key);
            result.Add(new ConfiguredScenario(def, effort));
        }

        return result;
    }

    /// <summary>Null when the definition is usable; otherwise the reason it was skipped.</summary>
    private static string? Validate(
        CustomScenarioDef def, HashSet<string> builtinKeys, HashSet<string> customKeys, out Effort? effort)
    {
        effort = null;
        if (string.IsNullOrWhiteSpace(def.Key)) return "missing 'key'";
        if (string.IsNullOrWhiteSpace(def.SystemPrompt)) return "missing 'systemPrompt'";
        if (builtinKeys.Contains(def.Key)) return $"'key' collides with built-in scenario '{def.Key}'";
        if (customKeys.Contains(def.Key)) return $"duplicate custom 'key' '{def.Key}'";

        if (!string.IsNullOrWhiteSpace(def.Effort))
        {
            if (!Enum.TryParse<Effort>(def.Effort, ignoreCase: true, out var e))
                return $"unrecognized 'effort' value '{def.Effort}'";
            effort = e;
        }

        // Only the Claude provider maps the model to a CLI alias; Copilot/Agency use their
        // own configured model and ignore this one, so any string is acceptable there.
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
