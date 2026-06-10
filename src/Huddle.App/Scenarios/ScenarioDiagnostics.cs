using System;
using System.IO;
using System.Text;

namespace Huddle.Scenarios;

/// <summary>
/// Appends verbatim records of scenario runs to
/// <c>%LOCALAPPDATA%\Huddle\scenarios.log</c>. Best-effort, no truncation.
/// </summary>
internal static class ScenarioDiagnostics
{
    public static void LogRun(
        string scenarioKey,
        string systemPrompt,
        string userText,
        string? response,
        long? inputTokens,
        long? outputTokens)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Huddle");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "scenarios.log");

            var sb = new StringBuilder();
            sb.AppendLine("=================================================================");
            sb.AppendLine($"[{DateTimeOffset.UtcNow:o}] scenario={scenarioKey} model=claude-sonnet-4-6");
            sb.AppendLine($"usage: input={inputTokens?.ToString() ?? "?"} output={outputTokens?.ToString() ?? "?"}");
            sb.AppendLine("--- system prompt ---");
            sb.AppendLine(systemPrompt);
            sb.AppendLine("--- user message ---");
            sb.AppendLine(userText);
            sb.AppendLine("--- raw response ---");
            sb.AppendLine(response ?? "(null)");
            sb.AppendLine();

            File.AppendAllText(path, sb.ToString());
        }
        catch
        {
            // Diagnostic log is best-effort.
        }
    }
}
