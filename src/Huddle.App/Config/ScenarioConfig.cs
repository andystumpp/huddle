namespace Huddle.Config;

/// <summary>
/// One scenario, defined in the <c>scenarios</c> array of <c>huddle.config.json</c>.
/// Only <see cref="Key"/> and <see cref="SystemPrompt"/> are required; every other field
/// defaults. <see cref="Effort"/> is kept as the raw string and validated/parsed where the
/// scenario set is composed. There are no built-in scenarios — the config array is the
/// full set (see <c>huddle.config.example.json</c> for the defaults).
/// </summary>
internal sealed class ScenarioDef
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
