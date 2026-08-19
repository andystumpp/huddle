using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Huddle.Config;

internal enum CliProviderKind { Claude, Copilot, Agency }

/// <summary>
/// Non-secret runtime configuration from <c>huddle.config.json</c> (resolved with
/// the same precedence as other config). Selects the CLI provider that handles
/// both vision and scenarios; every field except <see cref="Provider"/> has a
/// default, so <c>{ "provider": "copilot" }</c> is a complete config. No secrets —
/// each CLI authenticates through its own login.
/// </summary>
internal sealed class HuddleConfig
{
    public CliProviderKind Provider { get; init; } = CliProviderKind.Claude;

    /// <summary>The executable to run. Defaults to the provider's conventional binary.</summary>
    public string Command { get; init; } = "claude";

    /// <summary>Model for Copilot/Agency (Claude uses its per-scenario alias). Default `claude-opus-5`.</summary>
    public string Model { get; init; } = "claude-opus-5";

    /// <summary>Foreground app/title substrings that suppress a capture tick.</summary>
    public IReadOnlyList<string> CaptureDenylist { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True captures only the active window (via PrintWindow); false (default) captures
    /// the full primary display. Active-window scope makes the denylist an exact guarantee
    /// — only the focused window is ever sent — at the cost of peripheral-window context.
    /// Config key <c>captureScope</c>: <c>"activeWindow"</c> or <c>"fullScreen"</c>.
    /// </summary>
    public bool CaptureActiveWindowOnly { get; init; }

    private static HuddleConfig? s_cached;

    public static HuddleConfig Current => s_cached ??= Load();

    private static HuddleConfig Load()
    {
        foreach (var path in ConfigFileCandidates())
        {
            try
            {
                if (!File.Exists(path)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;

                CliProviderKind provider = root.TryGetProperty("provider", out var p)
                    ? ParseProvider(p.GetString())
                    : CliProviderKind.Claude;

                string command = root.TryGetProperty("command", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString()!
                    : DefaultCommand(provider);

                string model = root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString()!
                    : "claude-opus-5";

                var denylist = new List<string>();
                if (root.TryGetProperty("captureDenylist", out var d) && d.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in d.EnumerateArray())
                    {
                        if (e.ValueKind == JsonValueKind.String) denylist.Add(e.GetString()!);
                    }
                }

                bool activeWindowOnly = root.TryGetProperty("captureScope", out var s) && s.ValueKind == JsonValueKind.String
                    && s.GetString()!.Trim().ToLowerInvariant() is "activewindow" or "active" or "window";

                return new HuddleConfig
                {
                    Provider = provider,
                    Command = command,
                    Model = model,
                    CaptureDenylist = denylist,
                    CaptureActiveWindowOnly = activeWindowOnly,
                };
            }
            catch { /* malformed config — fall through to the default */ }
        }
        return new HuddleConfig(); // no file → Claude provider, empty denylist
    }

    private static CliProviderKind ParseProvider(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "copilot" => CliProviderKind.Copilot,
        "agency" => CliProviderKind.Agency,
        _ => CliProviderKind.Claude,
    };

    private static string DefaultCommand(CliProviderKind p) => p switch
    {
        CliProviderKind.Copilot => "copilot",
        CliProviderKind.Agency => "agency",
        _ => "claude",
    };

    private static IEnumerable<string> ConfigFileCandidates()
    {
        var exeDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(exeDir))
            yield return Path.Combine(exeDir, "huddle.config.json");
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApp))
            yield return Path.Combine(localApp, "Huddle", "huddle.config.json");
    }
}
