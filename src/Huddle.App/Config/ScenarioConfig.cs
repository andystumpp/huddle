using System;
using System.Collections.Generic;

namespace Huddle.Config;

/// <summary>
/// The optional <c>scenarios</c> section of <c>huddle.config.json</c>: which built-in
/// scenarios to turn off on this machine, and any custom scenarios to add. Both default
/// empty, so omitting the section leaves the built-in set unchanged.
/// </summary>
internal sealed class ScenarioConfig
{
    /// <summary>Built-in scenario keys to remove (e.g. <c>linkedin-posts</c>).</summary>
    public IReadOnlyList<string> Disabled { get; init; } = Array.Empty<string>();

    /// <summary>Custom scenarios defined inline.</summary>
    public IReadOnlyList<CustomScenarioDef> Custom { get; init; } = Array.Empty<CustomScenarioDef>();
}

/// <summary>
/// One config-authored scenario. Only <see cref="Key"/> and <see cref="SystemPrompt"/>
/// are required; every other field defaults. <see cref="Effort"/> is kept as the raw
/// string and validated/parsed where the scenario set is composed.
/// </summary>
internal sealed class CustomScenarioDef
{
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AccentColorHex { get; init; } = "#6BA6FF";
    public double CadenceHours { get; init; } = 6;
    public int TrailSize { get; init; } = 60;
    public int PriorNudgesSize { get; init; } = 10;
    public string Model { get; init; } = "sonnet";
    public string? Effort { get; init; }
    public bool WebSearch { get; init; }
    public string SystemPrompt { get; init; } = "";
}
