using System;
using System.Collections.Generic;
using System.IO;

namespace Huddle.Config;

/// <summary>
/// Resolves a configuration value from (in order): the process environment, the
/// User registry environment target, then a <c>huddle.env</c> file next to the
/// exe and at <c>%LOCALAPPDATA%\Huddle\</c>. Shared by the API-key lookup and the
/// scenario-backend flag so the precedence lives in one place.
/// </summary>
internal static class EnvConfig
{
    /// <summary>Returns the resolved value, or null if the name is set nowhere.</summary>
    public static string? Resolve(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value)) return value;

        try
        {
            value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        catch { /* registry can throw in restricted contexts */ }

        foreach (var path in EnvFileCandidates())
        {
            value = ReadFromEnvFile(path, name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static IEnumerable<string> EnvFileCandidates()
    {
        var exeDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            yield return Path.Combine(exeDir, "huddle.env");
        }
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApp))
        {
            yield return Path.Combine(localApp, "Huddle", "huddle.env");
        }
    }

    private static string? ReadFromEnvFile(string path, string name)
    {
        try
        {
            if (!File.Exists(path)) return null;
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var k = line.Substring(0, eq).Trim();
                if (!string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) continue;
                var v = line.Substring(eq + 1).Trim();
                if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
                {
                    v = v.Substring(1, v.Length - 2);
                }
                return v;
            }
        }
        catch { /* unreadable env file — ignore */ }
        return null;
    }
}
