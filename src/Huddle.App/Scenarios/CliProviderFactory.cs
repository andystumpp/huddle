using Huddle.Config;

namespace Huddle.Scenarios;

/// <summary>
/// Resolves the one CLI provider named in <see cref="HuddleConfig"/>. Both vision
/// and scenarios use the same provider, so a machine runs entirely on one CLI.
/// <c>agency</c> reuses <see cref="CopilotCliProvider"/> with the configured command.
/// </summary>
internal static class CliProviderFactory
{
    public static ICliProvider Resolve()
    {
        var config = HuddleConfig.Current;
        return config.Provider switch
        {
            CliProviderKind.Copilot => new CopilotCliProvider(config.Command, config.Model),
            CliProviderKind.Agency => new CopilotCliProvider(config.Command, config.Model),
            _ => new ClaudeCliProvider(),
        };
    }
}
