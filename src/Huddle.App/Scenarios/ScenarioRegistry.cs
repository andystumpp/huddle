using System.Collections.Generic;
using System.Linq;

namespace Huddle.Scenarios;

/// <summary>
/// Single source of truth for the enabled scenarios. The panel iterates this
/// list per tick; the nudge card reads display info via <see cref="GetByKey"/>.
/// Hardcoded for now — when the .md plugin loader lands, replace the initializer.
/// </summary>
internal static class ScenarioRegistry
{
    public static IReadOnlyList<Scenario> All { get; } = new Scenario[]
    {
        new LinkedInPostsScenario(),
        new AchievementsScenario(),
        new LearningsScenario(),
    };

    public static Scenario? GetByKey(string key) =>
        All.FirstOrDefault(s => s.Key == key);
}
