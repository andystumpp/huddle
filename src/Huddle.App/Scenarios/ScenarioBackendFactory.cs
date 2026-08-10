using System;
using Huddle.Config;

namespace Huddle.Scenarios;

/// <summary>
/// Chooses the scenario backend from the <c>HUDDLE_SCENARIO_BACKEND</c> flag
/// (resolved via <see cref="EnvConfig"/>). <c>cli</c> selects the subscription
/// path; anything else — including unset, empty, or unrecognized — falls back to
/// the always-available metered API, so behavior is unchanged until the user opts in.
/// </summary>
internal static class ScenarioBackendFactory
{
    public static IScenarioBackend Resolve()
    {
        var flag = EnvConfig.Resolve("HUDDLE_SCENARIO_BACKEND");
        if (string.Equals(flag?.Trim(), "cli", StringComparison.OrdinalIgnoreCase))
        {
            return new CliBackend();
        }
        return new ApiBackend();
    }
}
