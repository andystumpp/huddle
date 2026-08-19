using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Huddle.Models;

namespace Huddle.Scenarios;

/// <summary>
/// Shared helpers for building scenario user messages and JSON schemas.
/// </summary>
internal static class ScenarioPromptHelpers
{
    /// <summary>The structured-output schema for nudge drafts.</summary>
    public static Dictionary<string, JsonElement> BuildNudgeDraftSchema()
    {
        var properties = new
        {
            emit = new { type = "boolean" },
            reason = new { type = "string" },
            title = new { type = "string" },
            body = new { type = "string" },
            sources = new { type = "array", items = new { type = "string" } },
        };
        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["properties"] = JsonSerializer.SerializeToElement(properties),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "emit" }),
        };
    }

    /// <summary>
    /// The "respond with JSON matching this schema" directive appended to a prompt
    /// when the provider has no structured-output flag (both CLI providers).
    /// </summary>
    public static string BuildSchemaDirective(Dictionary<string, JsonElement> schema)
    {
        string json = JsonSerializer.Serialize(schema);
        return "\n\nRespond with a single JSON object matching this schema and nothing else "
             + "— no prose, no markdown, no code fences:\n" + json;
    }

    /// <summary>"Previously emitted by this scenario (newest first):" block.</summary>
    public static void AppendPriorNudges(StringBuilder sb, IReadOnlyList<Nudge> priorNudges, DateTimeOffset now, string heading)
    {
        if (priorNudges.Count == 0) return;
        sb.Append(heading).AppendLine(" (newest first):");
        foreach (var n in priorNudges)
        {
            sb.Append("- ")
              .Append(FormatRelativeTime(n.Ts, now))
              .Append(": \"")
              .Append(NormalizeWhitespace(n.Title))
              .Append("\" — ")
              .AppendLine(NormalizeWhitespace(n.Body));
        }
        sb.AppendLine();
    }

    /// <summary>"Recent moments (newest first):" block.</summary>
    public static void AppendRecentMoments(StringBuilder sb, IReadOnlyList<Moment> trail, DateTimeOffset now, int max)
    {
        if (trail.Count == 0) return;
        sb.AppendLine("Recent moments (newest first):");
        int count = Math.Min(trail.Count, max);
        for (int i = 0; i < count; i++)
        {
            var m = trail[i];
            sb.Append("- ")
              .Append(FormatRelativeTime(m.Ts, now))
              .Append(", ")
              .Append(m.App)
              .Append(" (\"")
              .Append(AbbreviateTitle(m.WindowTitle, 80))
              .Append("\"), id=")
              .Append(m.Id)
              .Append(": ")
              .AppendLine(NormalizeWhitespace(m.Summary));
        }
        sb.AppendLine();
    }

    public static string FormatRelativeTime(DateTimeOffset ts, DateTimeOffset now)
    {
        double minutes = (now - ts).TotalMinutes;
        if (minutes < 1) return "just now";
        if (minutes < 60) return $"{(int)minutes} min ago";
        int hours = (int)(minutes / 60);
        return $"{hours} h ago";
    }

    public static string AbbreviateTitle(string title, int max)
    {
        title = NormalizeWhitespace(title);
        if (title.Length <= max) return title;
        int spaceBreak = title.LastIndexOf(' ', max);
        if (spaceBreak > max / 2) return title.Substring(0, spaceBreak) + "…";
        return title.Substring(0, max) + "…";
    }

    public static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        bool prevSpace = false;
        foreach (char c in s)
        {
            if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim();
    }
}
